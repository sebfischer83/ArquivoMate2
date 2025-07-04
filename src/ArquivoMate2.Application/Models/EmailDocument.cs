using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Models
{
    public class EmailDocument
    {
        public required string Email { get; set; }

        public required string Subject { get; set; }

        public required byte[] File { get; set; }

        public required string FileName { get; set; }
    }
}
