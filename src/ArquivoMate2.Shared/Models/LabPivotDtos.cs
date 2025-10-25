using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    public sealed class PivotRowDto
    {
        public string Parameter { get; init; } = string.Empty;
        public string? Unit { get; init; }
        public List<decimal?> ValuesByCol { get; init; } = new();
        public List<string?> QualitativeByCol { get; init; } = new();

        public List<decimal?> ReferenceFromByCol { get; init; } = new();
        public List<decimal?> ReferenceToByCol { get; init; } = new();
        public List<string?> ReferenceComparatorByCol { get; init; } = new();
    }

    public sealed class LabPivotTableDto
    {
        public string OwnerId { get; init; } = string.Empty;
        public List<DateOnly> ColumnsDesc { get; init; } = new();
        public List<PivotRowDto> Rows { get; init; } = new();
    }
}
