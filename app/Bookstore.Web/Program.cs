using Bookstore.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;
using System;
using System.IO;

// Early-init NLog so startup errors are captured
var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config", optional: true).GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure NLog as the logging provider
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Load configuration and populate BookstoreConfiguration
    ConfigurationSetup.ConfigureConfiguration(builder.Configuration);

    // Configure NLog targets based on service settings
    LoggingSetup.ConfigureLogging();

    // MVC with views and areas — global Authorize filter to require auth by default
    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(typeof(AuthorizeAttribute));
    });

    // Authentication
    AuthenticationConfig.ConfigureAuthentication(builder.Services);

    // Application services and repositories
    DependencyInjectionSetup.ConfigureServices(builder.Services, builder.Environment);

    var app = builder.Build();

    // --- Initialize database ---
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<Bookstore.Data.ApplicationDbContext>();
        db.Database.EnsureCreated();
    }

    // --- Middleware pipeline ---
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    // Serve static files from wwwroot (if present)
    app.UseStaticFiles();

    // Serve static files from the legacy Content directory
    var contentPath = Path.Combine(app.Environment.ContentRootPath, "Content");
    if (Directory.Exists(contentPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(contentPath),
            RequestPath = "/Content"
        });
    }

    // Serve static files from the legacy Scripts directory
    var scriptsPath = Path.Combine(app.Environment.ContentRootPath, "Scripts");
    if (Directory.Exists(scriptsPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(scriptsPath),
            RequestPath = "/Scripts"
        });
    }

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    // Local authentication middleware (cookie-based local dev auth)
    if (BobsBookstoreClassic.Data.BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
    {
        app.UseMiddleware<Bookstore.Web.Helpers.LocalAuthenticationMiddleware>();
    }

    // Area routing — Admin area must be registered before the default route
    app.MapControllerRoute(
        name: "admin_default",
        pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}",
        defaults: new { area = "Admin" });

    // Default MVC route
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
