# BobsBookstoreClassic Migration Summary
## .NET Framework 4.8 → .NET 10.0

**Build Status:** ✅ SUCCEEDED — 0 errors, 0 compilation warnings

---

## Changes Made

### Project Files

| Project | Before | After |
|---------|--------|-------|
| `Bookstore.Web` | Old-style .csproj, .NET Framework 4.8, System.Web.Mvc, Autofac, OWIN | SDK-style `Microsoft.NET.Sdk.Web`, `net10.0` |
| `Bookstore.Data` | SDK-style, `netstandard2.0`, EntityFramework 6 | SDK-style, `net10.0`, EF Core 9.0.5 |
| `Bookstore.Domain` | SDK-style, `netstandard2.0` | SDK-style, `net10.0`, EF Core 9.0.5 |
| `Bookstore.Common` | SDK-style, `netstandard2.0` | SDK-style, `net10.0` |

### Core Infrastructure

- **Program.cs** (new) — replaces `Global.asax` + OWIN `Startup.cs`. Contains full ASP.NET Core startup with DI registration, authentication, routing, middleware configuration
- **appsettings.json** (new) — replaces `Web.config` app settings and connection strings
- **Global.asax / Global.asax.cs** — stubbed out (not used in ASP.NET Core)
- **Startup.cs** — stubbed out (logic moved to Program.cs)

### Data Layer

- **ApplicationDbContext** — migrated from EF6 (`System.Data.Entity`) to EF Core 9 (`Microsoft.EntityFrameworkCore`). Constructor changed from `string connectionString` to `DbContextOptions<ApplicationDbContext>`. All Fluent API calls updated (HasRequired→HasOne, WillCascadeOnDelete→OnDelete, HasDatabaseGeneratedOption→ValueGeneratedOnAdd)
- **BookstoreDbInitializer** — replaced `DropCreateDatabaseIfModelChanges<T>` (EF6 only) with `BookstoreDbSeeder.SeedAsync()` static method
- **BookstoreConfiguration** — replaced `System.Configuration.ConfigurationManager` with `IConfiguration`-backed `Initialize(IConfiguration)` method. Maintains backward-compatible static API
- **PaginatedList** — updated `System.Data.Entity` → `Microsoft.EntityFrameworkCore`
- **All Repositories** — updated `System.Data.Entity` → `Microsoft.EntityFrameworkCore`. Converted nested EF6 includes (`x.Items.Select(y => y.Child)`) to EF Core `ThenInclude` syntax

### Web Layer

- **All Controllers** — `using System.Web.Mvc` → `using Microsoft.AspNetCore.Mvc`, `ActionResult` → `IActionResult`
- **AuthenticationController** — replaced `HttpCookie` with `Response.Cookies.Delete()`
- **InventoryController** — `HttpPostedFileBase.InputStream` → `IFormFile.OpenReadStream()`, `HttpPostedFileBase.FileName` → `IFormFile.FileName`
- **AdminAreaControllerBase** — replaced `[RouteArea]` with `[Area("Admin")]`
- **AdminAreaRegistration** — stubbed out (areas auto-discovered in ASP.NET Core)

### Helpers

- **LocalAuthenticationMiddleware** — rewritten from OWIN (`OwinMiddleware`) to ASP.NET Core middleware (`RequestDelegate` pattern)
- **HttpContextExtensions** — `HttpContextBase` → `HttpContext`, `HttpCookie` → `Response.Cookies.Append()`
- **IOwinRequestExtensions** — renamed helper, `IOwinRequest` → `HttpRequest`
- **MvcHelpers** — `HtmlHelper` → `IHtmlHelper`
- **ImageTypesAttribute** — `HttpPostedFileBase` → `IFormFile`
- **MaxFileSizeAttribute** — `HttpPostedFileBase.ContentLength` → `IFormFile.Length`
- **ControllerExtensions** — updated namespace to `Microsoft.AspNetCore.Mvc`
- **ClaimsPrincipalExtensions** — added nullable annotations

### App_Start Files

All App_Start files (`AuthenticationSetup`, `DependencyInjectionSetup`, `ConfigurationSetup`, `LoggingSetup`, `BundleConfig`, `RouteConfig`, `FilterConfig`) stubbed out — their logic merged into `Program.cs`.

### Models

- `InventoryCreateUpdateViewModel` — `HttpPostedFileBase` → `IFormFile`, `System.Web.Mvc.SelectListItem` → `Microsoft.AspNetCore.Mvc.Rendering.SelectListItem`
- `InventoryIndexViewModel` — `System.Web.Mvc.SelectListItem` → `Microsoft.AspNetCore.Mvc.Rendering.SelectListItem`
- `OfferIndexViewModel` — same namespace update
- `ResaleCreateViewModel` — same namespace update
- `ReferenceDataCreateViewModel` — same namespace update
- `AddressCreateUpdateViewModel` — `System.Web.Mvc.SelectListItem` → `Microsoft.AspNetCore.Mvc.Rendering.SelectListItem`

### Views

- `Areas/Admin/Views/Orders/Index.cshtml` — `Html.EnumDropDownListFor()` → `Html.DropDownListFor()` with `Html.GetEnumSelectList<T>()`
- `Areas/Admin/Views/Offers/Index.cshtml` — same replacement

### Configuration

- **NLog** — configured via `NLog.Web.AspNetCore` builder extension (`builder.Host.UseNLog()`)
- **Authentication** — Cognito: OWIN OpenIdConnect → `Microsoft.AspNetCore.Authentication.OpenIdConnect`; Local: custom ASP.NET Core middleware
- **Authorization** — global `[Authorize]` via `AddControllersWithViews()` options

### Packages Removed

- Autofac, Autofac.Integration.Mvc, Autofac.Integration.Owin
- Microsoft.Owin, Microsoft.Owin.Security.*, Microsoft.Owin.Host.SystemWeb
- Owin, Owin (package)
- EntityFramework 6.x
- Microsoft.AspNet.Mvc 5.x, Microsoft.AspNet.Web.Optimization
- WebGrease, Antlr
- Microsoft.CodeDom.Providers.DotNetCompilerPlatform
- System.Web.* (all)

### Packages Added

- `Microsoft.EntityFrameworkCore.SqlServer` 9.0.5
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` 9.0.5
- `NLog.Web.AspNetCore` 5.3.14
- `Microsoft.Extensions.Configuration.Abstractions` 9.0.5

---

## Next Steps

1. **wwwroot**: Static files (`Content/`, `Scripts/`) should be moved to `wwwroot/` for ASP.NET Core static file serving. Currently served from legacy paths — views may need CSS/JS path updates.
2. **EF Core Migrations**: Run `dotnet ef migrations add InitialCreate` to create migration scripts instead of relying on `EnsureCreated()` for production. The seeder logic in `BookstoreDbSeeder.SeedAsync()` should be reviewed.
3. **Database test**: The `(localdb)\MSSQLLocalDB` connection in `appsettings.json` requires SQL Server LocalDB on Windows. For Linux/Docker, update the connection string or use a containerized SQL Server.
4. **Magick.NET**: Update to a newer version to resolve the 526 NuGet vulnerability warnings. These are package advisories only, not compilation errors.
5. **AWS Cognito HTTPS**: The application correctly notes that Cognito requires HTTPS; configure `ASPNETCORE_HTTPS_PORT` or use a reverse proxy for production.
6. **Views/Web.config**: The `Views/Web.config` file is no longer needed and can be deleted.
7. **NLog config**: Add `nlog.config` for production-level NLog configuration beyond `appsettings.json`.
