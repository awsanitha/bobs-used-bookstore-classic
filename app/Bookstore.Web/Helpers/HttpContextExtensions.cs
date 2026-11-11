using Microsoft.AspNetCore.Http;
using System;

namespace Bookstore.Web.Helpers
{
    public static class HttpContextExtensions
    {
        public static string GetShoppingCartCorrelationId(this HttpContext context)
        {
            const string cookieKey = "ShoppingCartId";

            context.Request.Cookies.TryGetValue(cookieKey, out var shoppingCartClientId);

            if (string.IsNullOrWhiteSpace(shoppingCartClientId))
            {
                shoppingCartClientId = context.User?.Identity?.IsAuthenticated == true
                    ? context.User.GetSub()
                    : Guid.NewGuid().ToString();
            }

            context.Response.Cookies.Append(cookieKey, shoppingCartClientId, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/",
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

            return shoppingCartClientId;
        }
    }
}
