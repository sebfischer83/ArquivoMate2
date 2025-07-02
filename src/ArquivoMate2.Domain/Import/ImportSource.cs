namespace ArquivoMate2.Domain.Import
{
    /// <summary>
    /// Represents the source origin of a document import
    /// </summary>
    public enum ImportSource
    {
        /// <summary>
        /// Import initiated by user through the UI
        /// </summary>
        User = 0,

        /// <summary>
        /// Import initiated from email attachment processing
        /// </summary>
        Email = 1
    }
}