﻿using ArquivoMate2.Domain.Document;
using MediatR;

namespace ArquivoMate2.Application.Commands
{
    public record ProcessDocumentCommand(Guid DocumentId, Guid ImportProcessId, string UserId) : IRequest<(Document? Document, string? TempFilePath)>;
}
