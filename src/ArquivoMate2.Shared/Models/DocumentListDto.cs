using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentListDto
    {
        public long TotalCount { get; set; }

        public long PageCount { get; set; }

        public bool IsLastPage { get; set; }

        public bool IsFirstPage { get; set; }

        public bool HasNextPage { get; set; }

        public bool HasPreviousPage { get; set; }   

        public int CurrentPage { get; set; }    

        public IList<DocumentListItemDto> Documents { get; set; } = new List<DocumentListItemDto>();
    }
}
