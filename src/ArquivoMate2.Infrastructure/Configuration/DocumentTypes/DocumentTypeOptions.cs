using System.Collections.Generic;

namespace ArquivoMate2.Infrastructure.Configuration.DocumentTypes
{
    /// <summary>
    /// Configuration options for document type seeding and behaviour.
    /// </summary>
    public class DocumentTypeOptions
    {
        /// <summary>
        /// Initial list of document type names that are seeded into Marten.
        /// </summary>
        public List<string> Seed { get; set; } = new();
    }
}
