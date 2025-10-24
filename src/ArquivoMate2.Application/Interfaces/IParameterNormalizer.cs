namespace ArquivoMate2.Application.Interfaces
{
    public interface IParameterNormalizer
    {
        // Normalize parameter names for consistent comparison and storage
        string Normalize(string parameter);
    }
}
