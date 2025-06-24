using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface ICurrentUserService
    {
        string UserId { get; }

        string GetUserIdByClaimPrincipal(ClaimsPrincipal user);
    }
}
