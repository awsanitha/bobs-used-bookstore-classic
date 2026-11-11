using Bookstore.Domain.Customers;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bookstore.Web.Helpers
{
    /// <summary>
    /// ASP.NET Core middleware that provides local (non-Cognito) authentication for development.
    /// Automatically signs in a fixed test user and persists via a cookie.
    /// </summary>
    public class LocalAuthenticationMiddleware
    {
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";
        private const string CookieName = "LocalAuthentication";
        private readonly RequestDelegate _next;

        public LocalAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICustomerService customerService)
        {
            if (context.Request.Path.StartsWithSegments("/Authentication/Login"))
            {
                SetLocalUser(context);
                await SaveCustomerDetailsAsync(context, customerService);

                context.Response.Cookies.Append(CookieName, "true", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(1),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax
                });

                context.Response.Redirect("/");
                return;
            }
            else if (context.Request.Cookies.ContainsKey(CookieName))
            {
                SetLocalUser(context);
                await SaveCustomerDetailsAsync(context, customerService);
            }

            await _next(context);
        }

        private static void SetLocalUser(HttpContext context)
        {
            var identity = new ClaimsIdentity("LocalAuthentication");
            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));

            context.User = new ClaimsPrincipal(identity);
        }

        private static async Task SaveCustomerDetailsAsync(HttpContext context, ICustomerService customerService)
        {
            var identity = context.User.Identity as ClaimsIdentity;
            if (identity == null) return;

            var dto = new CreateOrUpdateCustomerDto(
                identity.FindFirst("nameidentifier")?.Value ?? UserId,
                identity.Name,
                identity.FindFirst("given_name")?.Value ?? "Bookstore",
                identity.FindFirst("family_name")?.Value ?? "User");

            await customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
