using ArquivoMate2.Application.Models;

namespace ArquivoMate2.Application.Tests;

public class SampleApplicationTests
{
    [Fact]
    public void DocumentAnalysisResult_Has_Default_Values()
    {
        var result = new DocumentAnalysisResult();
        
        // String properties should have empty string defaults
        Assert.Equal(string.Empty, result.Date);
        Assert.Equal(string.Empty, result.DocumentType);
        Assert.Equal(string.Empty, result.CustomerNumber);
        Assert.Equal(string.Empty, result.InvoiceNumber);
        Assert.Equal(string.Empty, result.Title);
        Assert.Equal(string.Empty, result.Summary);
        
        // Object properties should be initialized
        Assert.NotNull(result.Sender);
        Assert.NotNull(result.Recipient);
        Assert.NotNull(result.Keywords);
        
        // Nullable decimal should be null
        Assert.Null(result.TotalPrice);
    }
}