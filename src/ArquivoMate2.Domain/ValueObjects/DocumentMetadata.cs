using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Domain.ValueObjects
{
    public record DocumentMetadata(
        Guid DocumentId,
        string UserId,
        string OriginalFileName,
        string ContentType,
        string Extension,
        long Size,
        DateTime UploadedAt,
        string[] Languages,
        string FileHash
    );

}
