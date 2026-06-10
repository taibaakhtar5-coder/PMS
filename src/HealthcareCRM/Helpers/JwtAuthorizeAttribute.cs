using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;

namespace HealthcareCRM.Helpers
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class JwtAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var token = context.HttpContext.Request.Cookies["jwt_token"];
            if (string.IsNullOrEmpty(token))
            {
                // No token cookie, redirect to login
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET") 
                                ?? "FriendswareHealthcareCRMSuperSecretKey2026ForTask2";

                if (secretKey.Length < 32)
                {
                    secretKey = secretKey.PadRight(32, '0');
                }

                var key = Encoding.UTF8.GetBytes(secretKey);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "HealthcareCRM",
                    ValidateAudience = true,
                    ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "HealthcareCRMUsers",
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                // Token is valid! Set context user claims if needed
                var jwtToken = (JwtSecurityToken)validatedToken;
                var claimsIdentity = new ClaimsIdentity(jwtToken.Claims, "jwt");
                context.HttpContext.User = new ClaimsPrincipal(claimsIdentity);
            }
            catch
            {
                // Invalid token, clear cookie and redirect to login
                context.HttpContext.Response.Cookies.Delete("jwt_token");
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }
        }
    }
}
