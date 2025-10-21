using System.Collections.Generic;

namespace ArquivoMate2.Infrastructure.Configuration.DocumentTypes
{
    /// <summary>
    /// Configuration options for document type seeding and behaviour.
    /// </summary>
    public class DocumentTypeOptions
    {
        /// <summary>
        /// Initial list of document types (name + optional system feature) that are seeded into Marten.
        /// </summary>
        public List<DocumentTypeSeedOption> Seed { get; set; } = new();
    }
}
