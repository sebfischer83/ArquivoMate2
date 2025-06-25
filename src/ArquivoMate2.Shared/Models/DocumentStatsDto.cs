using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Shared.Models
{
    public class DocumentStatsDto : BaseDto
    {
        public int Characters { get; set; }
        public int NotAccepted { get; set; }
        public int Documents { get; set; }
        public required Dictionary<string, int> Facets { get; set; }
    }
}
