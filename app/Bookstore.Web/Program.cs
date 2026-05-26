using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using BobsBookstoreClassic.Data;
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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Extensions.Logging;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddNLog();

// ── Configuration ─────────────────────────────────────────────────────────────
// Initialise the static BookstoreConfiguration from appsettings.json / env vars
var appSettingsSection = builder.Configuration.GetSection("AppSettings");
var connectionStringsSection = builder.Configuration.GetSection("ConnectionStrings");

BookstoreConfiguration.Initialize(
    appSettingsSection.AsEnumerable(makePathsRelative: true)
        .Where(kv => kv.Value != null)
        .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)),
    connectionStringsSection.AsEnumerable(makePathsRelative: true)
        .Where(kv => kv.Value != null)
        .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));

// Pull additional settings from AWS SSM Parameter Store when configured
var rootPath = "/" + Constants.AppName;
if (BookstoreConfiguration.GetSetting("Services/Database") == "aws")
{
    using var ssmClient = new AmazonSimpleSystemsManagementClient();
    var response = await ssmClient.GetParameterAsync(new GetParameterRequest
    {
        Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection"
    });
    BookstoreConfiguration.AddConnectionString("BookstoreDatabaseConnection", response.Parameter.Value);
}

if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
{
    using var ssmClient = new AmazonSimpleSystemsManagementClient();
    var response = await ssmClient.GetParametersByPathAsync(new GetParametersByPathRequest
    {
        Path = $"{rootPath}/Authentication/",
        Recursive = true
    });
    foreach (var p in response.Parameters)
        BookstoreConfiguration.AddSetting(p.Name.Replace($"{rootPath}/", string.Empty), p.Value);
}

if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
{
    using var ssmClient = new AmazonSimpleSystemsManagementClient();
    var response = await ssmClient.GetParametersByPathAsync(new GetParametersByPathRequest
    {
        Path = $"{rootPath}/Files/",
        Recursive = true
    });
    foreach (var p in response.Parameters)
        BookstoreConfiguration.AddSetting(p.Name.Replace($"{rootPath}/", string.Empty), p.Value);
}

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ── Domain services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

// ── File / image services ─────────────────────────────────────────────────────
if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
{
    builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
    builder.Services.AddScoped<IFileService, S3FileService>();
}
else
{
    var webRootPath = Path.Combine(builder.Environment.WebRootPath ?? builder.Environment.ContentRootPath, "Content");
    builder.Services.AddSingleton<IFileService>(new LocalFileService(webRootPath));
}

if (BookstoreConfiguration.GetSetting("Services/ImageValidationService") == "aws")
{
    builder.Services.AddSingleton<IAmazonRekognition, AmazonRekognitionClient>();
    builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
}
else
{
    builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
}

// ── Authentication ────────────────────────────────────────────────────────────
if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.ClientId = BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
        options.MetadataAddress = BookstoreConfiguration.GetSetting("Authentication/Cognito/MetadataAddress");
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
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
            OnRedirectToIdentityProvider = ctx =>
            {
                ctx.ProtocolMessage.RedirectUri = $"{ctx.Request.Scheme}://{ctx.Request.Host}/signin-oidc";
                return Task.CompletedTask;
            },
            OnAuthorizationCodeReceived = ctx =>
            {
                ctx.TokenEndpointRequest!.RedirectUri = $"{ctx.Request.Scheme}://{ctx.Request.Host}/signin-oidc";
                return Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var service = ctx.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                var dto = new CreateOrUpdateCustomerDto(
                    identity.GetSub(),
                    identity.Name!,
                    identity.FindFirst(c => c.Type.Contains("givenname"))?.Value ?? string.Empty,
                    identity.FindFirst(c => c.Type.Contains("surname"))?.Value ?? string.Empty);
                await service.CreateOrUpdateCustomerAsync(dto);
            }
        };
    });
}
else
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie();
    builder.Services.AddScoped<LocalAuthenticationMiddleware>();
}

builder.Services.AddHttpContextAccessor();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
{
    app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "Admin_default",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { area = "Admin" },
    constraints: null,
    dataTokens: new { area = "Admin" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
