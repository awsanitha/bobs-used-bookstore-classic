using System;
using BobsBookstoreClassic.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Bookstore.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        public IActionResult Login(string? redirectUri = null)
        {
            if (string.IsNullOrWhiteSpace(redirectUri)) return RedirectToAction("Index", "Home");

            return Redirect(redirectUri);
        }

        public IActionResult LogOut()
        {
            return BookstoreConfiguration.GetSetting("Services/Authentication") == "aws"
                ? CognitoSignOut()
                : LocalSignOut();
        }

        private IActionResult LocalSignOut()
        {
            // Delete the local authentication cookie
            Response.Cookies.Delete("LocalAuthentication");

            return RedirectToAction("Index", "Home");
        }

        private IActionResult CognitoSignOut()
        {
            // Delete the ASP.NET Core auth cookie
            Response.Cookies.Delete(".AspNetCore.Cookies");

            var domain = BookstoreConfiguration.GetSetting("Authentication/Cognito/CognitoDomain");
            var clientId = BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
            var logoutUri = $"{Request.Scheme}://{Request.Host}/";

            return Redirect($"{domain}/logout?client_id={clientId}&logout_uri={logoutUri}");
        }
    }
}
