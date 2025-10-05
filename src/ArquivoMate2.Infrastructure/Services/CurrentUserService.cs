using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace ArquivoMate2.Infrastructure.Services
{
    internal class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Paths _paths;

        // Ordered list of claim types we consider to identify the user
        private static readonly string[] EmailClaimCandidates = new[]
        {
            ClaimTypes.Email,      // Mapped email claim (when MapInboundClaims = true)
            "email",              // Raw JWT email claim (MapInboundClaims = false)
            "emails",             // Some providers deliver as list/CSV
            "preferred_username", // OIDC fallback
            "upn",                // AAD / ADFS
            "mail",               // AD / Graph
            "sub"                 // Last fallback (stable but not an email)
        };

        public CurrentUserService(IHttpContextAccessor httpContextAccessor, Paths paths)
        {
            _httpContextAccessor = httpContextAccessor;
            _paths = paths;
        }

        public string GetUserIdByClaimPrincipal(ClaimsPrincipal user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            var raw = ResolveUserIdentifier(user);
            return HashIdentifier(raw);
        }

        public string UserId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User
                           ?? throw new InvalidOperationException("No current HttpContext user.");
                var raw = ResolveUserIdentifier(user);
                return HashIdentifier(raw);
            }
        }

        private string ResolveUserIdentifier(ClaimsPrincipal user)
        {
            foreach (var type in EmailClaimCandidates)
            {
                var claim = user.Claims.FirstOrDefault(c => c.Type == type);
                if (claim == null) continue;
                var value = claim.Value?.Trim();
                if (string.IsNullOrEmpty(value)) continue;

                if (type == "emails" && value.Contains(','))
                {
                    value = value.Split(',').Select(v => v.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
                    if (string.IsNullOrEmpty(value)) continue;
                }

                return value.ToLowerInvariant();
            }
            throw new InvalidOperationException("No suitable user identifier claim (email/sub) found.");
        }

        private string HashIdentifier(string normalized)
        {
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
