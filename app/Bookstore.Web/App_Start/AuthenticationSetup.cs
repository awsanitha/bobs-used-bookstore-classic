using BobsBookstoreClassic.Data;
using Bookstore.Domain.Customers;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Bookstore.Web
{
    public static class AuthenticationConfig
    {
        public static void ConfigureAuthentication(IServiceCollection services)
        {
            if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
            {
                ConfigureCognitoAuthentication(services);
            }
            else
            {
                ConfigureLocalAuthentication(services);
            }
        }

        private static void ConfigureLocalAuthentication(IServiceCollection services)
        {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/Authentication/Login";
                    options.LogoutPath = "/Authentication/LogOut";
                });
        }

        private static void ConfigureCognitoAuthentication(IServiceCollection services)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
                options.MetadataAddress = BookstoreConfiguration.GetSetting("Authentication/Cognito/MetadataAddress");
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.UseTokenLifetime = false;
                options.SaveTokens = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "cognito:username",
                    RoleClaimType = "cognito:groups"
                };

                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = context =>
                    {
                        var returnUrl = $"{context.Request.Scheme}://{context.Request.Host}/signin-oidc";
                        context.ProtocolMessage.RedirectUri = returnUrl;
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    OnAuthorizationCodeReceived = context =>
                    {
                        var returnUrl = $"{context.Request.Scheme}://{context.Request.Host}/signin-oidc";
                        context.TokenEndpointRequest.RedirectUri = returnUrl;
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var customerService = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();

                        var identity = (ClaimsIdentity)context.Principal.Identity;

                        var dto = new CreateOrUpdateCustomerDto(
                            identity.GetSub(),
                            identity.Name,
                            identity.FindFirst(y => y.Type.Contains("givenname"))?.Value ?? string.Empty,
                            identity.FindFirst(y => y.Type.Contains("surname"))?.Value ?? string.Empty);

                        await customerService.CreateOrUpdateCustomerAsync(dto);
                    }
                };
            });
        }
    }
}
