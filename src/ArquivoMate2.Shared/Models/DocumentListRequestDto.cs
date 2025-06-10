using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentListRequestDto
    {
        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;
    }
}
