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
/// Infrastructure implementation of hierarchical document grouping.
/// </summary>
public sealed class DocumentGroupingService : IDocumentGroupingService
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    { "Collection","Year","Month","Type","Language" };
    private const string NoneKey = "(none)";
    private const string UnknownKey = "(unknown)";

    private readonly IQuerySession _query;

    public DocumentGroupingService(IQuerySession query) => _query = query;

    public async Task<IReadOnlyList<DocumentGroupingNode>> GroupAsync(string ownerUserId, DocumentGroupingRequest request, CancellationToken ct)
    {
        Validate(request);

        var docs = await _query.Query<DocumentView>()
            .Where(d => d.UserId == ownerUserId && !d.Deleted)
            .Select(d => new MinDoc
            {
                Id = d.Id,
                Date = d.Date,
                UploadedAt = d.UploadedAt,
                Type = d.Type,
                Language = d.Language
            })
            .ToListAsync(ct); // List<MinDoc>
        if (docs.Count == 0) return Array.Empty<DocumentGroupingNode>();

        var memberships = await _query.Query<DocumentCollectionMembership>()
            .Where(m => m.OwnerUserId == ownerUserId)
            .Select(m => new { m.CollectionId, m.DocumentId })
            .ToListAsync(ct);
        var collections = await _query.Query<DocumentCollection>()
            .Where(c => c.OwnerUserId == ownerUserId)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        // Build dictionary manually to avoid generic inference issues
        var collectionNameMap = new Dictionary<Guid, string>(collections.Count, EqualityComparer<Guid>.Default);
        foreach (var c in collections)
            collectionNameMap[c.Id] = c.Name;

        var docToCollections = new Dictionary<Guid, List<Guid>>();
        foreach (var group in memberships.GroupBy(m => m.DocumentId))
            docToCollections[group.Key] = group.Select(x => x.CollectionId).ToList();

        foreach (var d in docs)
        {
            var eff = d.Date ?? d.UploadedAt;
            d.Year = eff.Year;
            d.Month = eff.Month;
        }

        foreach (var seg in request.Path)
        {
            docs = FilterBySegment(docs, seg, docToCollections);
            if (docs.Count == 0) return Array.Empty<DocumentGroupingNode>();
        }

        var level = request.Path.Count;
        if (level >= request.Groups.Count) return Array.Empty<DocumentGroupingNode>();
        var nextDim = request.Groups[level];

        return nextDim.ToLowerInvariant() switch
        {
            "collection" => GroupByCollection(docs, docToCollections, collectionNameMap, level, request.Groups.Count),
            "year" => GroupByYear(docs, level, request.Groups.Count),
            "month" => GroupByMonth(docs, level, request.Groups.Count),
            "type" => GroupByString(docs, level, request.Groups.Count, d => d.Type, "Type"),
            "language" => GroupByString(docs, level, request.Groups.Count, d => d.Language, "Language"),
            _ => throw new InvalidOperationException("Unsupported grouping dimension.")
        };
    }

    private static List<MinDoc> FilterBySegment(IReadOnlyList<MinDoc> docs, DocumentGroupingPathSegment seg, Dictionary<Guid, List<Guid>> docToCollections)
    {
        IEnumerable<MinDoc> q = docs;
        switch (seg.Dimension.ToLowerInvariant())
        {
            case "collection":
                if (seg.Key == NoneKey)
                    q = q.Where(d => !docToCollections.ContainsKey(d.Id) || docToCollections[d.Id].Count == 0);
                else if (Guid.TryParse(seg.Key, out var colId))
                    q = q.Where(d => docToCollections.TryGetValue(d.Id, out var list) && list.Contains(colId));
                else
                    return new List<MinDoc>();
                break;
            case "year":
                if (seg.Key == UnknownKey) q = q.Where(d => d.Year == 0); else if (int.TryParse(seg.Key, out var y)) q = q.Where(d => d.Year == y); else return new List<MinDoc>();
                break;
            case "month":
                if (seg.Key == UnknownKey) q = q.Where(d => d.Month == 0); else if (int.TryParse(seg.Key, out var m)) q = q.Where(d => d.Month == m); else return new List<MinDoc>();
                break;
            case "type":
                q = seg.Key == NoneKey ? q.Where(d => string.IsNullOrEmpty(d.Type)) : q.Where(d => string.Equals(d.Type, seg.Key, StringComparison.Ordinal));
                break;
            case "language":
                q = seg.Key == NoneKey ? q.Where(d => string.IsNullOrEmpty(d.Language)) : q.Where(d => string.Equals(d.Language, seg.Key, StringComparison.Ordinal));
                break;
            default:
                return new List<MinDoc>();
        }
        return q.ToList();
    }

    private static IReadOnlyList<DocumentGroupingNode> GroupByCollection(IReadOnlyList<MinDoc> docs, Dictionary<Guid, List<Guid>> docToCollections, Dictionary<Guid, string> nameMap, int level, int totalLevels)
    {
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var d in docs)
        {
            if (docToCollections.TryGetValue(d.Id, out var list) && list.Count > 0)
            {
                foreach (var cid in list)
                {
                    var k = cid.ToString();
                    if (!buckets.TryAdd(k, 1)) buckets[k]++;
                }
            }
            else if (!buckets.TryAdd(NoneKey, 1)) buckets[NoneKey]++;
        }
        var hasChildren = level + 1 < totalLevels;
        return buckets.Select(b => new DocumentGroupingNode
        {
            Dimension = "Collection",
            Key = b.Key,
            Label = b.Key == NoneKey ? "(None)" : (Guid.TryParse(b.Key, out var gid) && nameMap.TryGetValue(gid, out var nm) ? nm : b.Key),
            Count = b.Value,
            HasChildren = hasChildren
        })
        .OrderBy(n => n.Key == NoneKey ? 0 : 1)
        .ThenBy(n => n.Label, StringComparer.CurrentCultureIgnoreCase)
        .ToList();
    }

    private static IReadOnlyList<DocumentGroupingNode> GroupByYear(IReadOnlyList<MinDoc> docs, int level, int totalLevels)
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

    private static IReadOnlyList<DocumentGroupingNode> GroupByMonth(IReadOnlyList<MinDoc> docs, int level, int totalLevels)
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

    private static IReadOnlyList<DocumentGroupingNode> GroupByString(IReadOnlyList<MinDoc> docs, int level, int totalLevels, Func<MinDoc, string?> selector, string dimension)
    {
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var d in docs)
        {
            var raw = selector(d) ?? string.Empty;
            var key = string.IsNullOrEmpty(raw) ? NoneKey : raw;
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
    }
}
