using ArquivoMate2.Application.Models;

namespace ArquivoMate2.Application.Tests;

public class SampleApplicationTests
{
    [Fact]
    public void DocumentAnalysisResult_Defaults_Are_Null()
    {
        var result = new DocumentAnalysisResult();
        Assert.Null(result.Sender);
        Assert.Null(result.Recipient);
        Assert.Null(result.DocumentType);
    }
}