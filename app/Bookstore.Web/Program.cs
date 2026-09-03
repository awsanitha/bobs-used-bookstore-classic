using System.Security.Claims;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Bookstore.Common;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.AWS.Logger;
using NLog.Config;
using NLog.Web;
using NLogTarget = NLog.Targets.Target;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. BookstoreConfiguration — wraps IConfiguration with override support
// ---------------------------------------------------------------------------
var bookstoreConfig = new BookstoreConfiguration(builder.Configuration);
builder.Services.AddSingleton(bookstoreConfig);

// ---------------------------------------------------------------------------
// 2. AWS SSM parameter fetching (mirrors ConfigurationSetup.cs)
// ---------------------------------------------------------------------------
var rootPath = "/" + Constants.AppName;

if (bookstoreConfig.GetSetting("Services/Database") == "aws")
{
    using var client = new AmazonSimpleSystemsManagementClient();
    var request = new GetParameterRequest { Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection" };
    var response = await client.GetParameterAsync(request);
    bookstoreConfig.AddSetting(
        response.Parameter.Name.Replace($"{rootPath}/Database/", string.Empty),
        response.Parameter.Value);
}

if (bookstoreConfig.GetSetting("Services/Authentication") == "aws")
{
    using var client = new AmazonSimpleSystemsManagementClient();
    var request = new GetParametersByPathRequest { Path = $"{rootPath}/Authentication/", Recursive = true };
    var response = await client.GetParametersByPathAsync(request);
    foreach (var parameter in response.Parameters)
    {
        bookstoreConfig.AddSetting(
            parameter.Name.Replace($"{rootPath}/", string.Empty),
            parameter.Value);
    }
}

if (bookstoreConfig.GetSetting("Services/FileService") == "aws")
{
    using var client = new AmazonSimpleSystemsManagementClient();
    var request = new GetParametersByPathRequest { Path = $"{rootPath}/Files/", Recursive = true };
    var response = await client.GetParametersByPathAsync(request);
    foreach (var parameter in response.Parameters)
    {
        bookstoreConfig.AddSetting(
            parameter.Name.Replace($"{rootPath}/", string.Empty),
            parameter.Value);
    }
}

// ---------------------------------------------------------------------------
// 3. NLog configuration (mirrors LoggingSetup.cs)
// ---------------------------------------------------------------------------
var nlogConfig = new LoggingConfiguration();
NLogTarget loggingTarget;

if (bookstoreConfig.GetSetting("Services/LoggingService") == "aws")
{
    loggingTarget = new AWSTarget { LogGroup = Constants.AppName };
}
else
{
    loggingTarget = new NLog.Targets.DebuggerTarget();
}

nlogConfig.AddTarget("aws", loggingTarget);
nlogConfig.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, loggingTarget));
LogManager.Configuration = nlogConfig;

builder.Host.UseNLog();

// ---------------------------------------------------------------------------
// 4. MVC + Authorization (mirrors FilterConfig.cs)
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ---------------------------------------------------------------------------
// 5. Authentication (mirrors AuthenticationSetup.cs)
// ---------------------------------------------------------------------------
if (bookstoreConfig.GetSetting("Services/Authentication") == "aws")
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.ClientId = bookstoreConfig.GetSetting("Authentication/Cognito/LocalClientId");
        options.MetadataAddress = bookstoreConfig.GetSetting("Authentication/Cognito/MetadataAddress");
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.SaveTokens = true;
        options.UseTokenLifetime = false;
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
                return Task.CompletedTask;
            },
            OnAuthorizationCodeReceived = context =>
            {
                var returnUrl = $"{context.Request.Scheme}://{context.Request.Host}/signin-oidc";
                context.TokenEndpointRequest!.RedirectUri = returnUrl;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var customerService = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();

                var identity = (ClaimsIdentity)context.Principal!.Identity!;

                var dto = new CreateOrUpdateCustomerDto(
                    identity.GetSub(),
                    identity.Name!,
                    identity.FindFirst(y => y.Type.Contains("givenname"))!.Value,
                    identity.FindFirst(y => y.Type.Contains("surname"))!.Value);

                await customerService.CreateOrUpdateCustomerAsync(dto);
            }
        };
    });
}
else
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie();
}

// ---------------------------------------------------------------------------
// 6. DI registrations (mirrors DependencyInjectionSetup.cs)
// ---------------------------------------------------------------------------

// Services
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

// Repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

// Open generic
builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

// ApplicationDbContext — EF Core with connection string
var connectionString = bookstoreConfig.GetConnectionString("BookstoreDatabaseConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Conditional file service
if (bookstoreConfig.GetSetting("Services/FileService") == "aws")
{
    builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
    builder.Services.AddScoped<IFileService, S3FileService>();
}
else
{
    builder.Services.AddSingleton<IFileService>(sp =>
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var webRootPath = Path.Combine(env.WebRootPath ?? env.ContentRootPath, "Content");
        return new LocalFileService(webRootPath);
    });
}

// Conditional image validation service
if (bookstoreConfig.GetSetting("Services/ImageValidationService") == "aws")
{
    builder.Services.AddSingleton<IAmazonRekognition, AmazonRekognitionClient>();
    builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
}
else
{
    builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
}

// LocalAuthenticationMiddleware uses the convention-based pattern (RequestDelegate constructor)
// and resolves ICustomerService via InvokeAsync method injection — no DI registration needed.

// ---------------------------------------------------------------------------
// 7. Build the app
// ---------------------------------------------------------------------------
var app = builder.Build();

// ---------------------------------------------------------------------------
// 8. Middleware pipeline
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

// Conditional local authentication middleware
if (bookstoreConfig.GetSetting("Services/Authentication") != "aws")
{
    app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.UseAuthorization();

// ---------------------------------------------------------------------------
// 9. Endpoint routing (mirrors RouteConfig.cs + AdminAreaRegistration.cs)
// ---------------------------------------------------------------------------
app.MapAreaControllerRoute(
    name: "Admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "Default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
