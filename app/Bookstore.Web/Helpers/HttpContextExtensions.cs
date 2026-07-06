using System;
using Microsoft.AspNetCore.Http;

namespace Bookstore.Web.Helpers
{
    public static class HttpContextExtensions
    {
        public static string GetShoppingCartCorrelationId(this HttpContext context)
        {
            const string CookieKey = "ShoppingCartId";

            string? shoppingCartClientId = context.Request.Cookies[CookieKey];

            if (string.IsNullOrWhiteSpace(shoppingCartClientId))
            {
                shoppingCartClientId = context.User.Identity?.IsAuthenticated == true
                    ? context.User.GetSub()
                    : Guid.NewGuid().ToString();
            }

            context.Response.Cookies.Append(CookieKey, shoppingCartClientId ?? Guid.NewGuid().ToString(), new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddYears(1),
                Path = "/",
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

            return shoppingCartClientId ?? Guid.NewGuid().ToString();
        }
    }
}
