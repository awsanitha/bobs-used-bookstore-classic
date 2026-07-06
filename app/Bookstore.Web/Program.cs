using BobsBookstoreClassic.Data;
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
using Amazon.S3;
using Amazon.Rekognition;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Bookstore.Common;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using System.Security.Claims;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Initialize BookstoreConfiguration from ASP.NET Core IConfiguration
    BookstoreConfiguration.Initialize(builder.Configuration);

    // Load additional settings from AWS SSM if configured
    ConfigureAwsSettings(builder.Configuration);

    // Configure NLog
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Add MVC with views
    builder.Services.AddControllersWithViews();

    // Add routing
    builder.Services.AddRouting(options =>
    {
        options.LowercaseUrls = false;
    });

    // Add TempData (uses cookies)
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession();

    // Configure EF Core
    var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

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
        var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
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

    // Register local auth middleware if using local auth
    if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
    {
        builder.Services.AddScoped<LocalAuthenticationMiddleware>();
    }

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
                OnRedirectToIdentityProvider = ctx =>
                {
                    var returnUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}/signin-oidc";
                    ctx.ProtocolMessage.RedirectUri = returnUrl;
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
                    var identity = (ClaimsIdentity?)ctx.Principal?.Identity;
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
        // Local authentication - use a simple cookie scheme
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Authentication/Login";
            });
    }

    builder.Services.AddAuthorization();

    var app = builder.Build();

    // Ensure database exists and seed data
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            db.Database.EnsureCreated();
            await BookstoreDbSeeder.SeedAsync(db);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "An error occurred while initializing the database.");
        }
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseStaticFiles();

    app.UseRouting();

    app.UseSession();

    app.UseAuthentication();
    app.UseAuthorization();

    // Use local authentication middleware when not using Cognito
    if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
    {
        app.UseMiddleware<LocalAuthenticationMiddleware>();
    }

    // Map area routes first
    app.MapControllerRoute(
        name: "admin",
        pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}",
        defaults: new { area = "Admin" },
        constraints: new { area = "Admin" }
    ).WithMetadata(new Microsoft.AspNetCore.Mvc.AreaAttribute("Admin"));

    app.MapControllerRoute(
        name: "areaRoute",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Application startup failed");
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
        catch { /* Silently fail - use default connection string */ }
    }

    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest
            {
                Path = $"{rootPath}/Authentication/",
                Recursive = true
            };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();

            foreach (var parameter in response.Parameters)
            {
                BookstoreConfiguration.AddSetting(
                    parameter.Name.Replace($"{rootPath}/", string.Empty),
                    parameter.Value);
            }
        }
        catch { /* Silently fail */ }
    }

    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        try
        {
            using var client = new AmazonSimpleSystemsManagementClient();
            var request = new GetParametersByPathRequest
            {
                Path = $"{rootPath}/Files/",
                Recursive = true
            };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();

            foreach (var parameter in response.Parameters)
            {
                BookstoreConfiguration.AddSetting(
                    parameter.Name.Replace($"{rootPath}/", string.Empty),
                    parameter.Value);
            }
        }
        catch { /* Silently fail */ }
    }
}
