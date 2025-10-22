using System;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Features.Processors.LabResults.Domain
{
    public sealed class LabResult
    {
        public Guid Id { get; set; }

        public string Patient { get; set; } = default!;
        public string LabName { get; set; } = default!;
        public DateOnly Date { get; set; }                          

        public List<LabResultPoint> Points { get; set; } = new();      // Werte über die Zeit (eine Spalte = ein Punkt)
    }
}
