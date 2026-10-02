using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace TaskManager.Api.Infrastructure;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The user id always comes from the validated token, never from the request body or route,
    /// so a client cannot act on behalf of another user.
    /// </summary>
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal has no valid 'sub' claim.");
    }
}
