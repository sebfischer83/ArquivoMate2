using ArquivoMate2.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services
{
    internal class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string UserId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                var idClaim = user?.FindFirst("uid")?.Value;

                if (string.IsNullOrEmpty(idClaim))
                {
                    throw new InvalidOperationException("User ID claim is missing or empty.");
                }

                return idClaim;
            }
        }
    }
}
