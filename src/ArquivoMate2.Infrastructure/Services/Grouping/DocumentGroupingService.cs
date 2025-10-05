using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Interfaces.Grouping;
using ArquivoMate2.Domain.Collections;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Shared.Models.Grouping;
using Marten;

namespace ArquivoMate2.Infrastructure.Services.Grouping;

/// <summary>
/// Infrastructure implementation of hierarchical document grouping including owned + shared documents.
/// </summary>
public sealed class DocumentGroupingService : IDocumentGroupingService
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    { "Collection","Year","Month","Type","Language" };
    private const string NoneKey = "(none)";
    private const string UnknownKey = "(unknown)";
    private const string SharedKey = "(shared)";

    private readonly IQuerySession _query;
    public DocumentGroupingService(IQuerySession query) => _query = query;

    public async Task<IReadOnlyList<DocumentGroupingNode>> GroupAsync(string userId, DocumentGroupingRequest request, CancellationToken ct)
    {
        Validate(request);

        var owned = await _query.Query<DocumentView>()
            .Where(d => d.UserId == userId && !d.Deleted)
            .Select(d => new MinDoc
            {
                Id = d.Id,
                Date = d.Date,
                UploadedAt = d.UploadedAt,
                Type = d.Type,
                Language = d.Language,
                IsShared = false
            })
            .ToListAsync(ct);

        var sharedIds = await _query.Query<DocumentAccessView>()
            .Where(a => a.EffectiveUserIds.Contains(userId) && a.OwnerUserId != userId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var shared = sharedIds.Count == 0
            ? new List<MinDoc>()
            : await _query.Query<DocumentView>()
                .Where(d => sharedIds.Contains(d.Id) && !d.Deleted && d.Processed)
                .Select(d => new MinDoc
                {
                    Id = d.Id,
                    Date = d.Date,
                    UploadedAt = d.UploadedAt,
                    Type = d.Type,
                    Language = d.Language,
                    IsShared = true
                })
                .ToListAsync(ct);

        if (owned.Count == 0 && shared.Count == 0)
            return Array.Empty<DocumentGroupingNode>();

        var map = new Dictionary<Guid, MinDoc>(owned.Count + shared.Count);
        foreach (var d in owned) map[d.Id] = d;
        foreach (var d in shared) map[d.Id] = d;
        var docs = map.Values.ToList(); // concrete List<MinDoc>

        var memberships = await _query.Query<DocumentCollectionMembership>()
            .Where(m => m.OwnerUserId == userId)
            .Select(m => new { m.CollectionId, m.DocumentId })
            .ToListAsync(ct);
        var collections = await _query.Query<DocumentCollection>()
            .Where(c => c.OwnerUserId == userId)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        var collectionNameMap = collections.ToDictionary(c => c.Id, c => c.Name);
        var docToCollections = memberships
            .GroupBy(m => m.DocumentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CollectionId).ToList());

        foreach (var d in docs)
        {
            var eff = d.Date ?? d.UploadedAt;
            d.Year = eff.Year;
            d.Month = eff.Month;
        }

        // Path filtering keeps docs as List<MinDoc>
        if (request.Path.Count > 0)
        {
            foreach (var seg in request.Path)
            {
                var filtered = FilterBySegment(docs, seg, docToCollections);
                if (filtered.Count == 0) return Array.Empty<DocumentGroupingNode>();
                docs.Clear();
                docs.AddRange(filtered);
            }
        }

        var level = request.Path.Count;
        if (level >= request.Groups.Count) return Array.Empty<DocumentGroupingNode>();
        var next = request.Groups[level].ToLowerInvariant();

        return next switch
        {
            "collection" => GroupByCollection(docs, docToCollections, collectionNameMap, level, request.Groups.Count),
            "year" => GroupByYear(docs, level, request.Groups.Count),
            "month" => GroupByMonth(docs, level, request.Groups.Count),
            "type" => GroupByString(docs, level, request.Groups.Count, d => d.Type, "Type"),
            "language" => GroupByString(docs, level, request.Groups.Count, d => d.Language, "Language"),
            _ => throw new InvalidOperationException("Unsupported grouping dimension.")
        };
    }

    private static List<MinDoc> FilterBySegment(List<MinDoc> source, DocumentGroupingPathSegment seg, Dictionary<Guid, List<Guid>> docToCollections)
    {
        IEnumerable<MinDoc> result = seg.Dimension.ToLowerInvariant() switch
        {
            "collection" => FilterCollection(source, seg.Key, docToCollections),
            "year" => seg.Key == UnknownKey ? source.Where(d => d.Year == 0) : (int.TryParse(seg.Key, out var y) ? source.Where(d => d.Year == y) : Enumerable.Empty<MinDoc>()),
            "month" => seg.Key == UnknownKey ? source.Where(d => d.Month == 0) : (int.TryParse(seg.Key, out var m) ? source.Where(d => d.Month == m) : Enumerable.Empty<MinDoc>()),
            "type" => seg.Key == NoneKey ? source.Where(d => string.IsNullOrEmpty(d.Type)) : source.Where(d => string.Equals(d.Type, seg.Key, StringComparison.Ordinal)),
            "language" => seg.Key == NoneKey ? source.Where(d => string.IsNullOrEmpty(d.Language)) : source.Where(d => string.Equals(d.Language, seg.Key, StringComparison.Ordinal)),
            _ => Enumerable.Empty<MinDoc>()
        };
        return result.ToList();
    }

    private static IEnumerable<MinDoc> FilterCollection(IEnumerable<MinDoc> docs, string key, Dictionary<Guid, List<Guid>> docToCollections) => key switch
    {
        NoneKey => docs.Where(d => !d.IsShared && (!docToCollections.ContainsKey(d.Id) || docToCollections[d.Id].Count == 0)),
        SharedKey => docs.Where(d => d.IsShared),
        _ => Guid.TryParse(key, out var cid) ? docs.Where(d => !d.IsShared && docToCollections.TryGetValue(d.Id, out var list) && list.Contains(cid)) : Enumerable.Empty<MinDoc>()
    };

    private static IReadOnlyList<DocumentGroupingNode> GroupByCollection(List<MinDoc> docs, Dictionary<Guid, List<Guid>> docToCollections, Dictionary<Guid, string> nameMap, int level, int totalLevels)
    {
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var d in docs)
        {
            if (d.IsShared)
            {
                if (!buckets.TryAdd(SharedKey, 1)) buckets[SharedKey]++;
                continue;
            }
            if (docToCollections.TryGetValue(d.Id, out var cols) && cols.Count > 0)
            {
                foreach (var c in cols)
                {
                    var key = c.ToString();
                    if (!buckets.TryAdd(key, 1)) buckets[key]++;
                }
            }
            else if (!buckets.TryAdd(NoneKey, 1)) buckets[NoneKey]++;
        }
        var hasChildren = level + 1 < totalLevels;
        return buckets.Select(b => new DocumentGroupingNode
        {
            Dimension = "Collection",
            Key = b.Key,
            Label = b.Key switch
            {
                NoneKey => "(None)",
                SharedKey => "(Shared)",
                _ => (Guid.TryParse(b.Key, out var gid) && nameMap.TryGetValue(gid, out var nm) ? nm : b.Key)
            },
            Count = b.Value,
            HasChildren = hasChildren
        })
        .OrderBy(n => n.Key == NoneKey ? 0 : n.Key == SharedKey ? 1 : 2)
        .ThenBy(n => n.Label, StringComparer.CurrentCultureIgnoreCase)
        .ToList();
    }

    private static IReadOnlyList<DocumentGroupingNode> GroupByYear(List<MinDoc> docs, int level, int totalLevels)
    {
        var buckets = docs.GroupBy(d => d.Year).ToDictionary(g => g.Key, g => g.Count());
        var hasChildren = level + 1 < totalLevels;
        return buckets.Select(b => new DocumentGroupingNode
        {
            Dimension = "Year",
            Key = b.Key == 0 ? UnknownKey : b.Key.ToString(CultureInfo.InvariantCulture),
            Label = b.Key == 0 ? "(Unknown)" : b.Key.ToString(CultureInfo.InvariantCulture),
            Count = b.Value,
            HasChildren = hasChildren
        })
        .OrderByDescending(n => n.Key == UnknownKey ? int.MinValue : int.Parse(n.Key == UnknownKey ? "0" : n.Key, CultureInfo.InvariantCulture))
        .ToList();
    }

    private static IReadOnlyList<DocumentGroupingNode> GroupByMonth(List<MinDoc> docs, int level, int totalLevels)
    {
        var buckets = docs.GroupBy(d => d.Month).ToDictionary(g => g.Key, g => g.Count());
        var hasChildren = level + 1 < totalLevels;
        return buckets.Select(b => new DocumentGroupingNode
        {
            Dimension = "Month",
            Key = b.Key == 0 ? UnknownKey : b.Key.ToString(CultureInfo.InvariantCulture),
            Label = b.Key == 0 ? "(Unknown)" : ($"{b.Key:00} - {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(b.Key)}"),
            Count = b.Value,
            HasChildren = hasChildren
        })
        .OrderByDescending(n => n.Key == UnknownKey ? int.MinValue : int.Parse(n.Key == UnknownKey ? "0" : n.Key, CultureInfo.InvariantCulture))
        .ToList();
    }

    private static IReadOnlyList<DocumentGroupingNode> GroupByString(List<MinDoc> docs, int level, int totalLevels, Func<MinDoc, string?> selector, string dimension)
    {
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var d in docs)
        {
            var value = selector(d) ?? string.Empty;
            var key = string.IsNullOrEmpty(value) ? NoneKey : value;
            if (!buckets.TryAdd(key, 1)) buckets[key]++;
        }
        var hasChildren = level + 1 < totalLevels;
        return buckets.Select(b => new DocumentGroupingNode
        {
            Dimension = dimension,
            Key = b.Key,
            Label = b.Key == NoneKey ? "(None)" : b.Key,
            Count = b.Value,
            HasChildren = hasChildren
        })
        .OrderBy(n => n.Key == NoneKey ? 0 : 1)
        .ThenBy(n => n.Label, StringComparer.CurrentCultureIgnoreCase)
        .ToList();
    }

    private static void Validate(DocumentGroupingRequest request)
    {
        if (request.Groups == null || request.Groups.Count == 0)
            throw new ArgumentException("At least one group dimension required.");
        var dup = request.Groups.GroupBy(g => g, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new ArgumentException("Duplicate group dimension: " + dup.Key);
        foreach (var g in request.Groups)
            if (!Allowed.Contains(g)) throw new ArgumentException($"Unsupported dimension '{g}'.");
        if (request.Path.Count > request.Groups.Count) throw new ArgumentException("Path longer than groups.");
        for (int i = 0; i < request.Path.Count; i++)
        {
            if (!string.Equals(request.Path[i].Dimension, request.Groups[i], StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path does not match group order.");
        }
    }

    private sealed class MinDoc
    {
        public Guid Id { get; set; }
        public DateTime? Date { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public bool IsShared { get; set; }
    }
}
