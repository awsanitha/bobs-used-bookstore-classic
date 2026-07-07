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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.Web;
using System.Security.Claims;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Initialise BookstoreConfiguration from IConfiguration ──────────────
    BookstoreConfiguration.Initialize(builder.Configuration);

    // ── Fetch secrets from AWS SSM Parameter Store (if configured) ──────────
    ConfigureAwsSettings(builder.Configuration);

    // ── Logging ─────────────────────────────────────────────────────────────
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // ── EF Core ─────────────────────────────────────────────────────────────
    var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        connectionString = builder.Configuration.GetConnectionString("BookstoreDatabaseConnection")
                           ?? "Server=(localdb)\\MSSQLLocalDB;Initial Catalog=BookStoreClassic;MultipleActiveResultSets=true;Integrated Security=SSPI;";
    }

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    // ── MVC ─────────────────────────────────────────────────────────────────
    builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation();

    builder.Services.AddHttpContextAccessor();

    // ── Authentication ───────────────────────────────────────────────────────
    var useAwsAuth = BookstoreConfiguration.GetSetting("Services/Authentication") == "aws";

    if (useAwsAuth)
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
            options.SaveTokens = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
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
                    var redirectUri = $"{context.Request.Scheme}://{context.Request.Host}/signin-oidc";
                    context.ProtocolMessage.RedirectUri = redirectUri;
                    return Task.CompletedTask;
                },
                OnAuthorizationCodeReceived = context =>
                {
                    context.TokenEndpointRequest!.RedirectUri = $"{context.Request.Scheme}://{context.Request.Host}/signin-oidc";
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var service = context.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                    var identity = (ClaimsIdentity?)context.Principal?.Identity;
                    if (identity == null) return;

                    var dto = new CreateOrUpdateCustomerDto(
                        identity.GetSub() ?? string.Empty,
                        identity.Name ?? string.Empty,
                        identity.FindFirst(y => y.Type.Contains("givenname"))?.Value ?? string.Empty,
                        identity.FindFirst(y => y.Type.Contains("surname"))?.Value ?? string.Empty);

                    await service.CreateOrUpdateCustomerAsync(dto);
                }
            };
        });
    }
    else
    {
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Authentication/Login";
                options.LogoutPath = "/Authentication/LogOut";
            });
    }

    // ── Authorization ────────────────────────────────────────────────────────
    builder.Services.AddAuthorization();

    // ── Domain Services ──────────────────────────────────────────────────────
    builder.Services.AddScoped<IBookService, BookService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
    builder.Services.AddScoped<IOfferService, OfferService>();
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<IAddressService, AddressService>();
    builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
    builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

    // ── Repositories ─────────────────────────────────────────────────────────
    builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
    builder.Services.AddScoped<IAddressRepository, AddressRepository>();
    builder.Services.AddScoped<IBookRepository, BookRepository>();
    builder.Services.AddScoped<IOfferRepository, OfferRepository>();
    builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

    builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

    // ── AWS/File Services ────────────────────────────────────────────────────
    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
        builder.Services.AddScoped<IFileService, S3FileService>();
    }
    else
    {
        builder.Services.AddSingleton<IFileService>(sp =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            return new LocalFileService(Path.Combine(env.WebRootPath, "Content"));
        });
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

    // Local auth middleware (used when Services/Authentication == "local")
    if (!useAwsAuth)
    {
        builder.Services.AddScoped<LocalAuthenticationMiddleware>();
    }

    // ── TempData (for notifications) ─────────────────────────────────────────
    builder.Services.AddSession();

    // ────────────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Error handling ───────────────────────────────────────────────────────
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseStaticFiles();
    app.UseRouting();
    app.UseSession();

    // Local authentication middleware
    if (!useAwsAuth)
    {
        app.UseMiddleware<LocalAuthenticationMiddleware>();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    // ── Routing ───────────────────────────────────────────────────────────────
    app.MapAreaControllerRoute(
        name: "Admin",
        areaName: "Admin",
        pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Stopped program because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}

static void ConfigureAwsSettings(IConfiguration configuration)
{
    var rootPath = "/" + Constants.AppName;

    if (BookstoreConfiguration.GetSetting("Services/Database") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParameterRequest
            {
                Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection"
            };
            var response = client.GetParameterAsync(request).GetAwaiter().GetResult();
            BookstoreConfiguration.AddConnectionString("BookstoreDatabaseConnection", response.Parameter.Value);
        }
        catch { /* swallow - will fall back to config file */ }
    }

    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest { Path = $"{rootPath}/Authentication/", Recursive = true };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
            foreach (var parameter in response.Parameters)
            {
                BookstoreConfiguration.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty), parameter.Value);
            }
        }
        catch { /* swallow */ }
    }

    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest { Path = $"{rootPath}/Files/", Recursive = true };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
            foreach (var parameter in response.Parameters)
            {
                BookstoreConfiguration.AddSetting(parameter.Name.Replace($"{rootPath}/", string.Empty), parameter.Value);
            }
        }
        catch { /* swallow */ }
    }
}
