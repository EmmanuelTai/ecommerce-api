using System.Security.Claims;
using ECommerce.Core.Interfaces;

namespace ECommerce.Api.Services;

// Lives in Api because it needs HttpContext; Core only sees the interface.
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public int UserId
    {
        get
        {
            // The "sub" claim, after JwtBearer has checked the token's signature and expiry.
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Only reachable if an endpoint that needs a user is missing [Authorize]: a bug, not a client error.
            return int.TryParse(value, out var userId)
                ? userId
                : throw new InvalidOperationException("No authenticated user. Is the endpoint missing [Authorize]?");
        }
    }
}
