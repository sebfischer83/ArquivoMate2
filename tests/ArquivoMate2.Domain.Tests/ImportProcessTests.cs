using ArquivoMate2.Domain.Import;
using ArquivoMate2.Shared.Models;

namespace ArquivoMate2.Domain.Tests;

public class ImportProcessTests
{
    [Fact]
    public void Apply_Sequence_Updates_State_As_Expected()
    {
        // arrange
        var importId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var userId = "user-123";
        var now = DateTime.UtcNow;
        var sut = new ImportProcess();

        // act
        sut.Apply(new InitDocumentImport(importId, userId, "file.pdf", now, ImportSource.Email));
        sut.Apply(new StartDocumentImport(importId, now.AddSeconds(1)));
        sut.Apply(new MarkSucceededDocumentImport(importId, documentId, now.AddSeconds(2)));
        sut.Apply(new HideDocumentImport(importId, now.AddSeconds(3)));

        // assert
        Assert.Equal(importId, sut.Id);
        Assert.Equal(userId, sut.UserId);
        Assert.Equal("file.pdf", sut.FileName);
        Assert.Equal(ImportSource.Email, sut.Source);
        Assert.Equal(DocumentProcessingStatus.Completed, sut.Status);
        Assert.Equal(documentId, sut.DocumentId);
        Assert.True(sut.IsHidden);
        Assert.NotNull(sut.CompletedAt);
    }

    [Fact]
    public void Apply_Failure_Sets_Error_And_Status()
    {
        var importId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var sut = new ImportProcess();

        sut.Apply(new InitDocumentImport(importId, "user", "file.pdf", now));
        sut.Apply(new StartDocumentImport(importId, now.AddSeconds(1)));
        sut.Apply(new MarkFailedDocumentImport(importId, "boom", now.AddSeconds(2)));

        Assert.Equal(DocumentProcessingStatus.Failed, sut.Status);
        Assert.Equal("boom", sut.ErrorMessage);
        Assert.NotNull(sut.CompletedAt);
    }
}