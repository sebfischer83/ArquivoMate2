using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.StorageProvider
{
    public class S3StorageProvider : IStorageProvider
    {
        private readonly S3StorageProviderSettings   _settings;
        public S3StorageProvider(IOptions<S3StorageProviderSettings> opts)
            => _settings = opts.Value;
    }
}
