using System;
using System.Collections.Generic;

namespace ArquivoMate2.Application.Features.Processors.LabResults.Models
{
    // Mapping document to quickly find which dates are present for a given DocumentId
    public sealed class LabPivotDateIndex
    {
        // Use DocumentId as document Id to make lookups by id cheap
        public Guid Id { get; set; }

        // Distinct dates present in the document's LabResult entries
        public List<DateOnly> Dates { get; set; } = new();
    }
}
