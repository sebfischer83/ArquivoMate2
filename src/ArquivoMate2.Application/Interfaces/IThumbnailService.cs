﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IThumbnailService
    {
        byte[] GenerateThumbnail(Stream inputFile);
    }
}
