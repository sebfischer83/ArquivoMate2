using ArquivoMate2.Application.Features.Processors.LabResults.Domain;
using ArquivoMate2.Application.Features.Processors.LabResults.Models;
using ArquivoMate2.Application.Features.Processors.LabResults.Services;
using ArquivoMate2.Application.Interfaces;
using Marten;
using Marten.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Domain.ReadModels;

namespace ArquivoMate2.Infrastructure.Services.LabResults
{
    public class LabPivotUpdater : ILabPivotUpdater
    {
        private readonly IDocumentStore _store;
        public LabPivotUpdater(IDocumentStore store)
        {
            _store = store;
        }

        public async Task AddOrUpdateAsync(IDocumentSession session, LabResult report, IParameterNormalizer normalizer, CancellationToken cancellationToken = default)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (normalizer == null) throw new ArgumentNullException(nameof(normalizer));

            // Resolve owner/user id for the document
            var ownerId = await session.Query<DocumentView>()
                .Where(d => d.Id == report.DocumentId)
                .Select(d => d.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(ownerId))
            {
                // If owner not found, skip updating pivot
                return;
            }

            // Load existing pivot by OwnerId (one pivot per owner)
            var pivot = await session.Query<LabPivotTable>()
                .Where(p => p.OwnerId == ownerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (pivot == null)
            {
                pivot = new LabPivotTable
                {
                    Id = Guid.NewGuid(),
                    OwnerId = ownerId,
                    ColumnsDesc = new List<DateOnly>(),
                    Rows = new List<PivotRow>()
                };
            }

            var date = report.Date;

            // Ensure ColumnsDesc contains date (only one column per date)
            if (!pivot.ColumnsDesc.Contains(date))
            {
                pivot.ColumnsDesc.Add(date);
                // keep strictly descending and unique
                pivot.ColumnsDesc = pivot.ColumnsDesc.Distinct().OrderByDescending(d => d).ToList();
                // Expand all rows ValuesByCol to match new columns
                foreach (var r in pivot.Rows)
                {
                    while (r.ValuesByCol.Count < pivot.ColumnsDesc.Count)
                        r.ValuesByCol.Add(null);
                }
            }

            // Determine index of column for this date
            var colIndex = pivot.ColumnsDesc.IndexOf(date);

            // For each measurement point in report
            foreach (var p in report.Points)
            {
                var normalized = normalizer.Normalize(p.Parameter ?? string.Empty);
                var unitNorm = p.NormalizedUnit;

                // Find existing row by parameter (case-insensitive via normalized) and unit
                var row = pivot.Rows.FirstOrDefault(r => string.Equals(r.Parameter, normalized, StringComparison.OrdinalIgnoreCase) && string.Equals(r.Unit, unitNorm, StringComparison.Ordinal));
                if (row == null)
                {
                    row = new PivotRow { Parameter = normalized, Unit = unitNorm, ValuesByCol = new List<decimal?>() };
                    // Ensure length
                    while (row.ValuesByCol.Count < pivot.ColumnsDesc.Count)
                        row.ValuesByCol.Add(null);
                    pivot.Rows.Add(row);
                }

                // Ensure ValuesByCol length
                while (row.ValuesByCol.Count < pivot.ColumnsDesc.Count)
                    row.ValuesByCol.Add(null);

                // Set value (overwrite existing)
                row.ValuesByCol[colIndex] = p.NormalizedResult;
            }

            // Stable sort rows: parameter case-insensitive, then Unit
            pivot.Rows = pivot.Rows.OrderBy(r => r.Parameter, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Unit ?? string.Empty, StringComparer.Ordinal).ToList();

            // Store pivot in session; do NOT call SaveChanges here to allow caller to commit once transactionally
            session.Store(pivot);
        }

        public async Task RebuildForOwnerAsync(IDocumentStore store, string ownerId, IParameterNormalizer normalizer, CancellationToken cancellationToken = default)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (normalizer == null) throw new ArgumentNullException(nameof(normalizer));
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentNullException(nameof(ownerId));

            using var s = store.LightweightSession();

            // Find all documents owned by ownerId
            var docIds = await s.Query<DocumentView>()
                .Where(d => d.UserId == ownerId && !d.Deleted)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            if (docIds.Count == 0)
            {
                // No documents for owner: remove existing pivot if any
                var existing = await s.Query<LabPivotTable>().Where(p => p.OwnerId == ownerId).FirstOrDefaultAsync(cancellationToken);
                if (existing != null)
                {
                    s.Delete(existing);
                    await s.SaveChangesAsync(cancellationToken);
                }
                return;
            }

            // Load all LabResult for these documents
            var results = await s.Query<LabResult>().Where(r => docIds.Contains(r.DocumentId)).ToListAsync(cancellationToken);

            var pivot = new LabPivotTable { Id = Guid.NewGuid(), OwnerId = ownerId, ColumnsDesc = new List<DateOnly>(), Rows = new List<PivotRow>() };

            foreach (var report in results)
            {
                if (!pivot.ColumnsDesc.Contains(report.Date))
                {
                    pivot.ColumnsDesc.Add(report.Date);
                    pivot.ColumnsDesc = pivot.ColumnsDesc.Distinct().OrderByDescending(d => d).ToList();
                    foreach (var rr in pivot.Rows)
                    {
                        while (rr.ValuesByCol.Count < pivot.ColumnsDesc.Count) rr.ValuesByCol.Add(null);
                    }
                }

                var colIndex = pivot.ColumnsDesc.IndexOf(report.Date);
                foreach (var p in report.Points)
                {
                    var normalized = normalizer.Normalize(p.Parameter ?? string.Empty);
                    var unitNorm = p.NormalizedUnit;
                    var row = pivot.Rows.FirstOrDefault(r => string.Equals(r.Parameter, normalized, StringComparison.OrdinalIgnoreCase) && string.Equals(r.Unit, unitNorm, StringComparison.Ordinal));
                    if (row == null)
                    {
                        row = new PivotRow { Parameter = normalized, Unit = unitNorm, ValuesByCol = new List<decimal?>() };
                        while (row.ValuesByCol.Count < pivot.ColumnsDesc.Count) row.ValuesByCol.Add(null);
                        pivot.Rows.Add(row);
                    }
                    while (row.ValuesByCol.Count < pivot.ColumnsDesc.Count) row.ValuesByCol.Add(null);
                    row.ValuesByCol[colIndex] = p.NormalizedResult;
                }

                pivot.Rows = pivot.Rows.OrderBy(r => r.Parameter, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Unit ?? string.Empty, StringComparer.Ordinal).ToList();
            }

            // Upsert pivot (replace existing for owner)
            var existingPivot = await s.Query<LabPivotTable>().Where(p => p.OwnerId == ownerId).FirstOrDefaultAsync(cancellationToken);
            if (existingPivot != null)
            {
                existingPivot.ColumnsDesc = pivot.ColumnsDesc;
                existingPivot.Rows = pivot.Rows;
                s.Store(existingPivot);
            }
            else
            {
                // Keep Id new
                s.Store(pivot);
            }

            await s.SaveChangesAsync(cancellationToken);
        }
    }
}
