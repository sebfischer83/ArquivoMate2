using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.ApiModels
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public IDictionary<string, string[]>? Errors { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public ApiResponse() { }

        public ApiResponse(bool success, string? message = null)
        {
            Success = success;
            Message = message;
        }
    }

    public class ApiResponse<T> : ApiResponse
    {
        public T? Data { get; set; }

        public ApiResponse() { }

        public ApiResponse(T? data, bool success = true, string? message = null)
            : base(success, message)
        {
            Data = data;
        }
    }
}
