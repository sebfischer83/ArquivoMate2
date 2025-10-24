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
    }
}
