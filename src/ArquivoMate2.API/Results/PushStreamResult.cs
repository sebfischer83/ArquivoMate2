using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

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

        public override Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var response = context.HttpContext.Response;
            response.ContentType = ContentType;

            return _writeToResponse(response.Body, context.HttpContext.RequestAborted);
        }
    }
}
