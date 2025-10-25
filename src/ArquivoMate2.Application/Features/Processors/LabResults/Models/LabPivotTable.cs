using System;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Features.Processors.LabResults.Models
{
    public sealed class LabPivotTable
    {
        // Id == OwnerId
        public Guid Id { get; set; }
        public string OwnerId { get; set; } = string.Empty;

        // Strictly descending unique dates (columns)
        public List<DateOnly> ColumnsDesc { get; set; } = new();

        public List<PivotRow> Rows { get; set; } = new();
    }

    public sealed class PivotRow
    {
        // Parameter normalized via IParameterNormalizer
        public string Parameter { get; set; } = string.Empty;
        public string? Unit { get; set; }

        // Values aligned with ColumnsDesc in LabPivotTable; length must match
        public List<decimal?> ValuesByCol { get; set; } = new();

        // Qualitative textual values per column when numeric result is not available
        public List<string?> QualitativeByCol { get; set; } = new();

        // Reference ranges per column (normalized numeric bounds)
        public List<decimal?> ReferenceFromByCol { get; set; } = new();
        public List<decimal?> ReferenceToByCol { get; set; } = new();

        // Optional comparator or raw reference string per column
        public List<string?> ReferenceComparatorByCol { get; set; } = new();

        // Track per-column units
        public List<string?> UnitsByCol { get; set; } = new();
    }
}
