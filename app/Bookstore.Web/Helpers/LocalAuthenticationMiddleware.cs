using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Bookstore.Domain.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Bookstore.Web.Helpers
{
    public class LocalAuthenticationMiddleware : IMiddleware
    {
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";
        private const string LocalAuthCookie = "LocalAuthentication";

        private readonly ICustomerService _customerService;

        public LocalAuthenticationMiddleware(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Path.StartsWithSegments("/Authentication/Login"))
            {
                var principal = CreateClaimsPrincipal();

                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                await SaveCustomerDetailsAsync(principal);

                context.Response.Cookies.Append(LocalAuthCookie, "true", new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(1)
                });

                var returnUrl = context.Request.Query["redirectUri"].ToString();
                context.Response.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
                return;
            }

            if (context.Request.Cookies.ContainsKey(LocalAuthCookie))
            {
                var principal = CreateClaimsPrincipal();
                context.User = principal;

                await SaveCustomerDetailsAsync(principal);
            }

            await next(context);
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

        private async Task SaveCustomerDetailsAsync(ClaimsPrincipal principal)
        {
            var identity = (ClaimsIdentity?)principal.Identity;
            if (identity == null) return;

            var dto = new CreateOrUpdateCustomerDto(
                identity.FindFirst("nameidentifier")?.Value ?? string.Empty,
                identity.Name ?? string.Empty,
                identity.FindFirst("given_name")?.Value ?? string.Empty,
                identity.FindFirst("family_name")?.Value ?? string.Empty);

            await _customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
