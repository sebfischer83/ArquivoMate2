using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArquivoMate2.API.Results
{
    public class PushStreamResult : FileResult
    {
        private readonly Func<Stream, CancellationToken, Task> _writeToResponse;

        public PushStreamResult(string contentType, Func<Stream, CancellationToken, Task> writeToResponse)
            : base(contentType)
        {
            _writeToResponse = writeToResponse ?? throw new ArgumentNullException(nameof(writeToResponse));
        }

        protected override Task WriteFileAsync(HttpContext context, Stream responseStream)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return _writeToResponse(responseStream, context.RequestAborted);
        }
    }
}
