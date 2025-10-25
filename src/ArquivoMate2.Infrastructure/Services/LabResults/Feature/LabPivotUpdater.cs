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
        private readonly IUnitConverter _unitConverter;

        public LabPivotUpdater(IDocumentStore store, IUnitConverter unitConverter)
        {
            _store = store;
            _unitConverter = unitConverter;
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
                // Expand all rows ValuesByCol and QualitativeByCol and Reference lists to match new columns
                foreach (var r in pivot.Rows)
                {
                    while (r.ValuesByCol.Count < pivot.ColumnsDesc.Count)
                        r.ValuesByCol.Add(null);
                    while (r.QualitativeByCol.Count < pivot.ColumnsDesc.Count)
                        r.QualitativeByCol.Add(null);
                    while (r.ReferenceFromByCol.Count < pivot.ColumnsDesc.Count)
                        r.ReferenceFromByCol.Add(null);
                    while (r.ReferenceToByCol.Count < pivot.ColumnsDesc.Count)
                        r.ReferenceToByCol.Add(null);
                    while (r.ReferenceComparatorByCol.Count < pivot.ColumnsDesc.Count)
                        r.ReferenceComparatorByCol.Add(null);
                    while (r.UnitsByCol.Count < pivot.ColumnsDesc.Count)
                        r.UnitsByCol.Add(null);
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
                    row = new PivotRow { Parameter = normalized, Unit = unitNorm, ValuesByCol = new List<decimal?>(), QualitativeByCol = new List<string?>(), ReferenceFromByCol = new List<decimal?>(), ReferenceToByCol = new List<decimal?>(), ReferenceComparatorByCol = new List<string?>(), UnitsByCol = new List<string?>() };
                    // Ensure length
                    while (row.ValuesByCol.Count < pivot.ColumnsDesc.Count)
                        row.ValuesByCol.Add(null);
                    while (row.QualitativeByCol.Count < pivot.ColumnsDesc.Count)
                        row.QualitativeByCol.Add(null);
                    while (row.ReferenceFromByCol.Count < pivot.ColumnsDesc.Count)
                        row.ReferenceFromByCol.Add(null);
                    while (row.ReferenceToByCol.Count < pivot.ColumnsDesc.Count)
                        row.ReferenceToByCol.Add(null);
                    while (row.ReferenceComparatorByCol.Count < pivot.ColumnsDesc.Count)
                        row.ReferenceComparatorByCol.Add(null);
                    while (row.UnitsByCol.Count < pivot.ColumnsDesc.Count)
                        row.UnitsByCol.Add(null);

                    pivot.Rows.Add(row);
                }

                // Ensure ValuesByCol and QualitativeByCol and Reference lists length
                while (row.ValuesByCol.Count < pivot.ColumnsDesc.Count)
                    row.ValuesByCol.Add(null);
                while (row.QualitativeByCol.Count < pivot.ColumnsDesc.Count)
                    row.QualitativeByCol.Add(null);
                while (row.ReferenceFromByCol.Count < pivot.ColumnsDesc.Count)
                    row.ReferenceFromByCol.Add(null);
                while (row.ReferenceToByCol.Count < pivot.ColumnsDesc.Count)
                    row.ReferenceToByCol.Add(null);
                while (row.ReferenceComparatorByCol.Count < pivot.ColumnsDesc.Count)
                    row.ReferenceComparatorByCol.Add(null);
                while (row.UnitsByCol.Count < pivot.ColumnsDesc.Count)
                    row.UnitsByCol.Add(null);

                // Set value: prefer normalized numeric result; if missing, store qualitative raw
                if (p.NormalizedResult.HasValue)
                {
                    row.ValuesByCol[colIndex] = p.NormalizedResult;
                    // clear any qualitative value at this column
                    row.QualitativeByCol[colIndex] = null;
                }
                else
                {
                    row.ValuesByCol[colIndex] = null;
                    // store raw textual result if present
                    var raw = p.ResultRaw;
                    row.QualitativeByCol[colIndex] = string.IsNullOrWhiteSpace(raw) ? null : raw;
                }

                // store per-column unit
                row.UnitsByCol[colIndex] = p.NormalizedUnit ?? p.Unit;

                // Populate reference ranges if present (prefer normalized bounds)
                if (p.NormalizedReferenceFrom.HasValue || p.NormalizedReferenceTo.HasValue)
                {
                    row.ReferenceFromByCol[colIndex] = p.NormalizedReferenceFrom;
                    row.ReferenceToByCol[colIndex] = p.NormalizedReferenceTo;
                    row.ReferenceComparatorByCol[colIndex] = p.ReferenceComparator;
                }
                else if (p.ReferenceFrom.HasValue || p.ReferenceTo.HasValue)
                {
                    row.ReferenceFromByCol[colIndex] = p.ReferenceFrom;
                    row.ReferenceToByCol[colIndex] = p.ReferenceTo;
                    row.ReferenceComparatorByCol[colIndex] = p.ReferenceComparator;
                }
                else
                {
                    // keep existing nulls
                }
            }

            // Stable sort rows: parameter case-insensitive, then Unit
            pivot.Rows = pivot.Rows.OrderBy(r => r.Parameter, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Unit ?? string.Empty, StringComparer.Ordinal).ToList();

            // Attempt to consolidate units per parameter: if multiple units present, try converting to the most common one
            ConsolidateUnits(pivot);

            // Store pivot in session; do NOT call SaveChanges here to allow caller to commit once transactionally
            session.Store(pivot);
        }

        private void ConsolidateUnits(LabPivotTable pivot)
        {
            foreach (var row in pivot.Rows)
            {
                // collect non-empty unit occurrences across columns
                var unitCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < row.UnitsByCol.Count; i++)
                {
                    var unit = row.UnitsByCol[i] ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(unit)) continue;
                    if (!unitCounts.TryAdd(unit, 1)) unitCounts[unit]++;
                }

                if (unitCounts.Count <= 1) continue; // nothing to do

                // choose most common unit as target
                var target = unitCounts.OrderByDescending(kv => kv.Value).First().Key;

                // convert any numeric values not in target
                for (int col = 0; col < row.ValuesByCol.Count; col++)
                {
                    var currentUnit = row.UnitsByCol.Count > col ? row.UnitsByCol[col] ?? string.Empty : string.Empty;
                    var val = row.ValuesByCol[col];
                    if (val.HasValue && !string.Equals(currentUnit, target, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(currentUnit))
                    {
                        if (_unitConverter.TryConvert(val.Value, currentUnit, target, out var conv))
                        {
                            row.ValuesByCol[col] = conv;
                            row.UnitsByCol[col] = target;
                        }
                    }

                    // convert reference ranges as well
                    var refFrom = row.ReferenceFromByCol.Count > col ? row.ReferenceFromByCol[col] : null;
                    var refTo = row.ReferenceToByCol.Count > col ? row.ReferenceToByCol[col] : null;
                    if ((refFrom.HasValue || refTo.HasValue) && !string.Equals(currentUnit, target, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(currentUnit))
                    {
                        if (_unitConverter.TryConvertRange(refFrom, refTo, currentUnit, target, out var cFrom, out var cTo))
                        {
                            if (row.ReferenceFromByCol.Count > col) row.ReferenceFromByCol[col] = cFrom;
                            if (row.ReferenceToByCol.Count > col) row.ReferenceToByCol[col] = cTo;
                            row.ReferenceComparatorByCol[col] = row.ReferenceComparatorByCol.Count > col ? row.ReferenceComparatorByCol[col] : null;
                            row.UnitsByCol[col] = target;
                        }
                    }
                }

                // set row unit to consolidated target
                row.Unit = target;
            }
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
                        while (rr.QualitativeByCol.Count < pivot.ColumnsDesc.Count) rr.QualitativeByCol.Add(null);
                        while (rr.ReferenceFromByCol.Count < pivot.ColumnsDesc.Count) rr.ReferenceFromByCol.Add(null);
                        while (rr.ReferenceToByCol.Count < pivot.ColumnsDesc.Count) rr.ReferenceToByCol.Add(null);
                        while (rr.ReferenceComparatorByCol.Count < pivot.ColumnsDesc.Count) rr.ReferenceComparatorByCol.Add(null);
                        while (rr.UnitsByCol.Count < pivot.ColumnsDesc.Count) rr.UnitsByCol.Add(null);
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
                        row = new PivotRow { Parameter = normalized, Unit = unitNorm, ValuesByCol = new List<decimal?>(), QualitativeByCol = new List<string?>() };
                        row = new PivotRow { Parameter = normalized, Unit = unitNorm, ValuesByCol = new List<decimal?>(), QualitativeByCol = new List<string?>(), ReferenceFromByCol = new List<decimal?>(), ReferenceToByCol = new List<decimal?>(), ReferenceComparatorByCol = new List<string?>(), UnitsByCol = new List<string?>() };
                        while (row.ValuesByCol.Count < pivot.ColumnsDesc.Count) row.ValuesByCol.Add(null);
                        while (row.QualitativeByCol.Count < pivot.ColumnsDesc.Count) row.QualitativeByCol.Add(null);
                        while (row.ReferenceFromByCol.Count < pivot.ColumnsDesc.Count) row.ReferenceFromByCol.Add(null);
                        while (row.ReferenceToByCol.Count < pivot.ColumnsDesc.Count) row.ReferenceToByCol.Add(null);
                        while (row.ReferenceComparatorByCol.Count < pivot.ColumnsDesc.Count) row.ReferenceComparatorByCol.Add(null);
                        pivot.Rows.Add(row);
                    }
                    while (row.ValuesByCol.Count < pivot.ColumnsDesc.Count) row.ValuesByCol.Add(null);
                    while (row.QualitativeByCol.Count < pivot.ColumnsDesc.Count) row.QualitativeByCol.Add(null);
                    while (row.ReferenceFromByCol.Count < pivot.ColumnsDesc.Count) row.ReferenceFromByCol.Add(null);
                    while (row.ReferenceToByCol.Count < pivot.ColumnsDesc.Count) row.ReferenceToByCol.Add(null);
                    while (row.ReferenceComparatorByCol.Count < pivot.ColumnsDesc.Count) row.ReferenceComparatorByCol.Add(null);
                    while (row.UnitsByCol.Count < pivot.ColumnsDesc.Count) row.UnitsByCol.Add(null);

                    if (p.NormalizedResult.HasValue)
                    {
                        row.ValuesByCol[colIndex] = p.NormalizedResult;
                        row.QualitativeByCol[colIndex] = null;
                    }
                    else
                    {
                        row.ValuesByCol[colIndex] = null;
                        row.QualitativeByCol[colIndex] = string.IsNullOrWhiteSpace(p.ResultRaw) ? null : p.ResultRaw;
                    }

                    // store per-column unit
                    while (row.UnitsByCol.Count < pivot.ColumnsDesc.Count) row.UnitsByCol.Add(null);
                    row.UnitsByCol[colIndex] = p.NormalizedUnit ?? p.Unit;

                    if (p.NormalizedReferenceFrom.HasValue || p.NormalizedReferenceTo.HasValue)
                    {
                        row.ReferenceFromByCol[colIndex] = p.NormalizedReferenceFrom;
                        row.ReferenceToByCol[colIndex] = p.NormalizedReferenceTo;
                        row.ReferenceComparatorByCol[colIndex] = p.ReferenceComparator;
                    }
                    else if (p.ReferenceFrom.HasValue || p.ReferenceTo.HasValue)
                    {
                        row.ReferenceFromByCol[colIndex] = p.ReferenceFrom;
                        row.ReferenceToByCol[colIndex] = p.ReferenceTo;
                        row.ReferenceComparatorByCol[colIndex] = p.ReferenceComparator;
                    }
                }

                pivot.Rows = pivot.Rows.OrderBy(r => r.Parameter, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Unit ?? string.Empty, StringComparer.Ordinal).ToList();
            }

            // Consolidate units across built pivot
            ConsolidateUnits(pivot);

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
