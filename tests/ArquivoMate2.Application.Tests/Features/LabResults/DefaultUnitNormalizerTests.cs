using ArquivoMate2.Application.Features.Processors.LabResults.Services;
using ArquivoMate2.Application.Interfaces;
using Xunit;

namespace ArquivoMate2.Application.Tests.Features.LabResults
{
    public class DefaultUnitNormalizerTests
    {
        private readonly IUnitNormalizer _normalizer = new DefaultUnitNormalizer();

        [Theory]
        [InlineData("g/l", "g/L")]
        [InlineData("G/L", "g/L")]
        [InlineData(" g/L ", "g/L")]
        [InlineData("mmol/l", "mmol/L")]
        [InlineData("MMOL/L", "mmol/L")]
        [InlineData("ng/ml", "ng/mL")]
        [InlineData("ug/ml", "µg/mL")]
        [InlineData("mcg/ml", "µg/mL")]
        [InlineData("mg/dl", "mg/dL")]
        [InlineData("10e9/l", "10^9/L")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Normalize_ReturnsExpected(string input, string expected)
        {
            var outp = _normalizer.Normalize(input ?? string.Empty);
            Assert.Equal(expected, outp);
        }
    }
}
