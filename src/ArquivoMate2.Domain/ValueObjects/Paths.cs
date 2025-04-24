using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Domain.ValueObjects
{
    public record Paths(
        string Working)
    {
        public string Upload => Path.Combine(Working, "upload");
    }
}
