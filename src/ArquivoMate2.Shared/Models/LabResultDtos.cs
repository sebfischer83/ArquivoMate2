using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    public sealed class LabResultPointDto
    {
        public string Parameter { get; init; } = string.Empty;
        public string ResultRaw { get; init; } = string.Empty;
        public decimal? ResultNumeric { get; init; }
        public string? ResultComparator { get; init; }
        public string? Unit { get; init; }
        public string? Reference { get; init; }
        public string? ReferenceComparator { get; init; }
        public decimal? ReferenceFrom { get; init; }
        public decimal? ReferenceTo { get; init; }
        public decimal? NormalizedResult { get; init; }
        public string? NormalizedUnit { get; init; }
        public decimal? NormalizedReferenceFrom { get; init; }
        public decimal? NormalizedReferenceTo { get; init; }
    }

    public sealed class LabResultDto
    {
        public Guid Id { get; init; }
        public Guid DocumentId { get; init; }
        public string Patient { get; init; } = string.Empty;
        public string LabName { get; init; } = string.Empty;
        public DateOnly Date { get; init; }
        public List<LabResultPointDto> Points { get; init; } = new();
    }
}
