using Bookstore.Domain.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bookstore.Web.Helpers
{
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
                var principal = CreateClaimsPrincipal();
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                await SaveCustomerDetailsAsync(customerService, principal);

                context.Response.Cookies.Append(CookieName, "1", new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(1)
                });

                context.Response.Redirect("/");
                return;
            }

            if (context.Request.Cookies.ContainsKey(CookieName))
            {
                var principal = CreateClaimsPrincipal();
                context.User = principal;
                await SaveCustomerDetailsAsync(customerService, principal);
            }

            await _next(context);
        }

        private static ClaimsPrincipal CreateClaimsPrincipal()
        {
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));
            return new ClaimsPrincipal(identity);
        }

        private static async Task SaveCustomerDetailsAsync(ICustomerService customerService, ClaimsPrincipal principal)
        {
            var identity = (ClaimsIdentity)principal.Identity;
            var dto = new CreateOrUpdateCustomerDto(
                identity.FindFirst("nameidentifier")?.Value,
                identity.Name,
                identity.FindFirst("given_name")?.Value,
                identity.FindFirst("family_name")?.Value);

            await customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
