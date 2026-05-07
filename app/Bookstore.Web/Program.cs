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
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Initialize BookstoreConfiguration from IConfiguration
BookstoreConfiguration.Initialize(builder.Configuration);

// Load AWS SSM parameters if needed
LoadAwsConfiguration(builder.Configuration);

// Configure NLog
var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Configure services
builder.Services.AddControllersWithViews();

// Register DbContext
var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection")
    ?? builder.Configuration.GetConnectionString("BookstoreDatabaseConnection");
builder.Services.AddBookstoreDbContext(connectionString);

// Register domain services
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

// Register repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

// Register file service
if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
{
    builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
    builder.Services.AddScoped<IFileService, S3FileService>();
}
else
{
    var webRootPath = builder.Environment.WebRootPath ?? builder.Environment.ContentRootPath;
    builder.Services.AddSingleton<IFileService>(new LocalFileService(webRootPath));
}

// Register image validation service
if (BookstoreConfiguration.GetSetting("Services/ImageValidationService") == "aws")
{
    builder.Services.AddSingleton<IAmazonRekognition, AmazonRekognitionClient>();
    builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
}
else
{
    builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
}

// Register local auth middleware if needed
if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
{
    builder.Services.AddScoped<LocalAuthenticationMiddleware>();
}

builder.Services.AddHttpContextAccessor();

// Configure authentication
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
                ctx.ProtocolMessage.RedirectUri = GetReturnUrl(ctx.Request);
                return Task.CompletedTask;
            },
            OnAuthorizationCodeReceived = ctx =>
            {
                ctx.TokenEndpointRequest.RedirectUri = GetReturnUrl(ctx.Request);
                return Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var service = ctx.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                var identity = (ClaimsIdentity)ctx.Principal!.Identity!;

                var dto = new CreateOrUpdateCustomerDto(
                    identity.GetSub(),
                    identity.Name,
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
}

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure DB is created and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    BookstoreDbSeeder.Seed(db);
}

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

static string GetReturnUrl(HttpRequest request)
{
    return $"{request.Scheme}://{request.Host}/signin-oidc";
}

static void LoadAwsConfiguration(IConfiguration configuration)
{
    var rootPath = "/" + Constants.AppName;

    if (BookstoreConfiguration.GetSetting("Services/Database") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParameterRequest { Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection" };
            var response = client.GetParameterAsync(request).GetAwaiter().GetResult();
            BookstoreConfiguration.AddSetting("ConnectionStrings/BookstoreDatabaseConnection", response.Parameter.Value);
            BookstoreConfiguration.AddConnectionString("BookstoreDatabaseConnection", response.Parameter.Value);
        }
        catch { /* SSM not available in dev */ }
    }

    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest { Path = $"{rootPath}/Authentication/", Recursive = true };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
            foreach (var parameter in response.Parameters)
                BookstoreConfiguration.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty), parameter.Value);
        }
        catch { /* SSM not available in dev */ }
    }

    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest { Path = $"{rootPath}/Files/", Recursive = true };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
            foreach (var parameter in response.Parameters)
                BookstoreConfiguration.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty), parameter.Value);
        }
        catch { /* SSM not available in dev */ }
    }
}
