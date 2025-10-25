namespace ArquivoMate2.Application.Interfaces
{
    public interface IUnitConverter
    {
        /// <summary>
        /// Try convert a numeric value from one unit to another. Returns true if conversion succeeded.
        /// </summary>
        bool TryConvert(decimal value, string fromUnit, string toUnit, out decimal converted);

        /// <summary>
        /// Try convert nullable range bounds from one unit to another. Returns true if at least one bound converted.
        /// </summary>
        bool TryConvertRange(decimal? fromValue, decimal? toValue, string fromUnit, string toUnit, out decimal? convertedFrom, out decimal? convertedTo);
    }
}
