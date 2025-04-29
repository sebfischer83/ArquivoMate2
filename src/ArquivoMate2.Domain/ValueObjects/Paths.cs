using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Domain.ValueObjects
{
    public class Paths
    {
        public string Working { get; set; } = string.Empty;

        public string PathBuilderSecret { get; set; } = string.Empty;
        public Paths()
        {
            
        }

        public Paths(string working)
        {
            Working = working ?? throw new ArgumentNullException(nameof(working));
        }

        public string Upload => Path.Combine(Working, "upload");
    }
}
