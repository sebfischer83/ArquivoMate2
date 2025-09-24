using ArquivoMate2.Domain.ValueObjects;
using ArquivoMate2.Infrastructure.Services;

namespace ArquivoMate2.Infrastructure.Tests;

public class PathServiceTests
{
    [Fact]
    public void GetStoragePath_Returns_Deterministic_Segments()
    {
        var paths = new Paths("/root");
        var sut = new PathService(paths);
        var userId = "user42";
        var docId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeffffffff");
        var fileName = "file.pdf";

        var segments = sut.GetStoragePath(userId, docId, fileName);

        Assert.Equal(6, segments.Length);
        Assert.Equal(userId, segments[0]);
        Assert.Equal(docId.ToString(), segments[4]);
        Assert.Equal(fileName, segments[5]);

        var again = sut.GetStoragePath(userId, docId, fileName);
        Assert.Equal(segments[1], again[1]);
        Assert.Equal(segments[2], again[2]);
        Assert.Equal(segments[3], again[3]);
    }

    [Fact]
    public void GetDocumentUploadPath_Uses_Working_Directory()
    {
        var paths = new Paths("/workdir");
        var sut = new PathService(paths);
        var result = sut.GetDocumentUploadPath("userX");
        Assert.Contains("userX", result);
        Assert.Contains("upload", result);
    }

    [Fact]
    public void GetUserPartFromPath_Works_For_Valid_Path()
    {
        var paths = new Paths("/root");
        var sut = new PathService(paths);
        var part = sut.GetUserPartFromPath("userX/aa/bb/cc/doc/file.pdf");
        Assert.Equal("userX/aa", part);
    }
}