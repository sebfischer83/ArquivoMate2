using System;
using System.Collections.Generic;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentEventDto
    {
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredOn { get; set; }
        public string? UserId { get; set; }
        public string? Data { get; set; }
    }
}
