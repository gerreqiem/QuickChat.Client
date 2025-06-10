using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace QuickChat.Client.Utilities
{
    public static class JwtUtils
    {
        public static string GetClaim(string jwtToken, string claimType)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(jwtToken);
            var claim = jwt.Claims.FirstOrDefault(c => c.Type == claimType);
            return claim?.Value ?? string.Empty;
        }
    }
}