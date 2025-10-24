namespace ArquivoMate2.Application.Interfaces
{
    /// <summary>
    /// Normalizes and maps unit strings to canonical representations.
    /// </summary>
    public interface IUnitNormalizer
    {
        /// <summary>
        /// Normalize a raw unit string to a canonical unit representation or return an empty string when normalization fails.
        /// </summary>
        string Normalize(string unit);
    }
}
