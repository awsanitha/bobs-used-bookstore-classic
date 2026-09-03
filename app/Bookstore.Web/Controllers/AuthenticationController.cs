using Bookstore.Data;
using Microsoft.AspNetCore.Mvc;

namespace Bookstore.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        private readonly BookstoreConfiguration _bookstoreConfiguration;

        public AuthenticationController(BookstoreConfiguration bookstoreConfiguration)
        {
            _bookstoreConfiguration = bookstoreConfiguration;
        }

        public ActionResult Login(string? redirectUri = null)
        {
            if(string.IsNullOrWhiteSpace(redirectUri)) return RedirectToAction("Index", "Home");

            return Redirect(redirectUri);
        }

        public ActionResult LogOut()
        {
            return _bookstoreConfiguration.GetSetting("Services/Authentication") == "aws" ? CognitoSignOut() : LocalSignOut();
        }

        private ActionResult LocalSignOut()
        {
            if (HttpContext.Request.Cookies["LocalAuthentication"] != null)
            {
                HttpContext.Response.Cookies.Delete("LocalAuthentication");
            }

            return RedirectToAction("Index", "Home");
        }

        private ActionResult CognitoSignOut()
        {
            if (Request.Cookies[".AspNet.Cookies"] != null)
            {
                Response.Cookies.Delete(".AspNet.Cookies");
            }

            var domain = _bookstoreConfiguration.GetSetting("Authentication/Cognito/CognitoDomain");
            var clientId = _bookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");

            var port = Request.Host.Port;
            var logoutUri = port.HasValue
                ? $"{Request.Scheme}://{Request.Host.Host}:{port}/"
                : $"{Request.Scheme}://{Request.Host.Host}/";

            return Redirect($"{domain}/logout?client_id={clientId}&logout_uri={logoutUri}");
        }
    }
}
