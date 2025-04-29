using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services
{
    internal class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Paths _paths;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, Paths paths)
        {
            _httpContextAccessor = httpContextAccessor;
            _paths = paths;
        }

        public string UserIdForPath
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                var idClaim = user?.FindFirst(ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(idClaim))
                {
                    throw new InvalidOperationException("User ID claim is missing or empty.");
                }

                var normalized = idClaim.Trim().ToLowerInvariant();
                var data = Encoding.UTF8.GetBytes(normalized);

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_paths.PathBuilderSecret));
                var hash = hmac.ComputeHash(data);

                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));

                return sb.ToString();
            }
        }
    }
}
