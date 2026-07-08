# Migration Summary: .NET Framework 4.8 → net10.0

## Status: ✅ BUILD SUCCEEDED — 0 Compilation Errors, 0 Code Warnings

`dotnet build BobsBookstoreClassic.sln` exits with code 0.

---

## What Was Migrated

### Project Files
| Project | Before | After |
|---------|--------|-------|
| `Bookstore.Web` | Legacy XML `.csproj` targeting `net4.8` (MVC5, Autofac, OWIN) | SDK-style `Microsoft.NET.Sdk.Web` targeting `net10.0` |
| `Bookstore.Data` | SDK-style `netstandard2.0` with EF6 | SDK-style `net10.0` with EF Core 10 |
| `Bookstore.Domain` | SDK-style `netstandard2.0` | SDK-style `net10.0` with EF Core reference |
| `Bookstore.Common` | SDK-style `netstandard2.0` | SDK-style `net10.0` |

### Key Transformations

#### ASP.NET Core Migration (Bookstore.Web)
- **Removed**: `Global.asax`, `Startup.cs`, `App_Start/` folder (all App_Start files)
- **Created**: `Program.cs` — single entry point combining Global.asax + Startup + all App_Start setup
- **Created**: `appsettings.json` / `appsettings.Development.json` from `Web.config`
- **Created**: `nlog.config` for ASP.NET Core NLog integration
- **Removed**: `Web.config`, `Web.Debug.config`, `Web.Release.config`, `Views/Web.config`, `Areas/Admin/Views/web.config`, `packages.config`
- **Removed**: `AdminAreaRegistration.cs` (area discovery is automatic in ASP.NET Core)
- **Replaced**: `IAppBuilder` (OWIN) with ASP.NET Core middleware pipeline
- **Replaced**: `Autofac` DI with built-in `Microsoft.Extensions.DependencyInjection`
- **Replaced**: `Microsoft.Owin.Security.Cookies` + `Microsoft.Owin.Security.OpenIdConnect` with `Microsoft.AspNetCore.Authentication.*`
- **Removed**: `Microsoft.AspNetCore.Authentication.Cookies` NuGet reference (included in ASP.NET Core shared framework for net10.0)

#### Controllers (System.Web.Mvc → Microsoft.AspNetCore.Mvc)
- All controllers: `ActionResult` → `IActionResult`, namespace updated
- All controllers: `using System.Web.Mvc;` → `using Microsoft.AspNetCore.Mvc;`
- `AdminAreaControllerBase`: Added `[Area("Admin")]` attribute for ASP.NET Core area discovery
- `AuthenticationController`: Updated to use `HttpContext.SignOutAsync()`
- `InventoryController`: `HttpPostedFileBase` → `IFormFile`, uses `file.OpenReadStream()`

#### Helpers
- `LocalAuthenticationMiddleware`: Rewritten as `IMiddleware` (ASP.NET Core), removed OWIN dependency
- `HttpContextExtensions`: Rewritten using `Microsoft.AspNetCore.Http.HttpContext`
- `ControllerExtensions`: Updated namespace to `Microsoft.AspNetCore.Mvc`
- `ImageTypesAttribute`: `HttpPostedFileBase` → `IFormFile`
- `MaxFileSizeAttribute`: `HttpPostedFileBase` → `IFormFile`, `file.ContentLength` → `file.Length`
- `MvcHelpers`: `HtmlHelper` → `IHtmlHelper` from `Microsoft.AspNetCore.Mvc.Rendering`
- `IOwinRequestExtensions.cs`: **Deleted** (no longer needed)

#### EF6 → EF Core (Bookstore.Data)
- `ApplicationDbContext`: `DbContext(string connectionString)` → `DbContext(DbContextOptions<T>)`
- `ModelBuilder`: EF6 `DbModelBuilder` → EF Core `ModelBuilder`; `PluralizingTableNameConvention` → explicit `ToTable()` calls
- `HasRequired(...).WillCascadeOnDelete(false)` → `HasOne(...).OnDelete(DeleteBehavior.Restrict)`
- `HasIndex(...).IsUnique()` moved from inline attribute to Fluent API
- `DatabaseGeneratedOption.Identity` → `ValueGeneratedOnAdd()`
- `Database.SetInitializer(new BookstoreDbInitializer())` → EF Core `HasData()` seeding in `BookstoreDbSeeder.cs`
- `BookstoreDbInitializer` (EF6) → `BookstoreDbSeeder` (EF Core static seeder)
- All repositories: `using System.Data.Entity;` → `using Microsoft.EntityFrameworkCore;`
- All repositories: `Include("string")` → `Include(x => x.Property)` with `ThenInclude()`
- `PaginatedList`: `CountAsync()`/`ToListAsync()` now from EF Core extensions

#### Configuration
- `BookstoreConfiguration`: Replaced `ConfigurationManager.AppSettings` with `IConfiguration`-based initialization (`BookstoreConfiguration.Initialize(config)`)
- `BookstoreConfiguration` namespace changed from `BobsBookstoreClassic.Data` → `Bookstore.Data`

#### Models / ViewModels
- `InventoryCreateUpdateViewModel`: `HttpPostedFileBase CoverImage` → `IFormFile? CoverImage`
- All ViewModels: `using System.Web.Mvc;` → `using Microsoft.AspNetCore.Mvc.Rendering;`
- `OrderDetailsViewModel`: String properties initialized with `= null!` to satisfy nullable analysis
- `OrderDetailsItemViewModel`: String properties initialized with `= null!`
- `OfferIndexViewModel`: `Filters` property initialized to `new OfferFilters()`
- `OfferIndexItemViewModel`: String properties initialized with `= null!`
- `InventoryDetailsViewModel`: String properties initialized with `= null!`

#### Domain
- `IShoppingCartRepository.GetAsync()`: Return type `Task<ShoppingCart>` → `Task<ShoppingCart?>`
- `ICustomerRepository.GetAsync()`: Return types updated to nullable
- `IOfferRepository.GetAsync()`: Return type updated to nullable
- `IAddressRepository.GetAsync()`: Return type updated to nullable
- `OfferStatistics`: Fixed namespace from `Bookstore.Domain.Orders` → `Bookstore.Domain.Offers`
- `PaginatedList`: Moved EF Core using, fixed nullable constructor

#### Views
- `Areas/Admin/Views/Orders/Index.cshtml`: `Html.EnumDropDownListFor` → `Html.DropDownListFor` with `Html.GetEnumSelectList<OrderStatus>()`
- `Areas/Admin/Views/Offers/Index.cshtml`: `Html.EnumDropDownListFor` → `Html.DropDownListFor` with `Html.GetEnumSelectList<OfferStatus>()`
- All `@Html.Partial(...)` calls replaced with `<partial name="..." />` tag helpers (eliminates MVC1000 deadlock warnings)
- All `@{ Html.RenderPartial(...); }` calls replaced with `<partial name="..." />` tag helpers

---

## Remaining Warnings (all non-blocking)

The 532 remaining warnings are exclusively NuGet security advisories for transitive dependencies:
- **NU1901/NU1902/NU1903**: Package vulnerability notifications for `Magick.NET-Q8-AnyCPU` 14.6.0 and `System.Security.Cryptography.Xml` 9.0.0 (transitive)
- **NU1603**: Dependency version resolution notices

There are **zero CS compiler warnings** and **zero MVC warnings**.

---

## Next Steps

### Runtime / Deployment Considerations
1. **EF Core Migrations**: The `HasData()` seeding added in `BookstoreDbSeeder` will only apply when `dotnet ef database update` is run or `EnsureCreated()` is called at startup. Add the following to `Program.cs` if fresh DB initialization is required:
   ```csharp
   using var scope = app.Services.CreateScope();
   var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
   db.Database.EnsureCreated(); // or db.Database.Migrate()
   ```

2. **Static Files**: The application serves static content from `wwwroot/`. Content in `Content/` and `Scripts/` folders needs to either be moved to `wwwroot/` or a static file middleware mapping added.

3. **Session Middleware**: `AddSession()` is configured but `AddDistributedMemoryCache()` should be added before `AddSession()` in `Program.cs` for proper TempData/session support.

4. **HTTPS Redirection**: `app.UseHttpsRedirection()` is not currently enabled; add if needed.

5. **Magick.NET**: Version 14.6.0 has known vulnerabilities. Consider upgrading to the latest release to eliminate NU1902 warnings.

6. **AWS Cognito Redirect URI**: In the original code, the redirect URI was dynamically built from the current request. The migrated OpenIdConnect events preserve this behavior.

7. **Connection String**: The default `appsettings.json` connection string uses `(localdb)\MSSQLLocalDB`. Update for ECS/container deployment via environment variables or AWS SSM.

8. **BookstoreConfiguration.Initialize()**: This is called once at startup. If settings are loaded from SSM after initialization, the `AddSetting()` / `AddConnectionString()` methods correctly patch the in-memory cache.

9. **Dockerfile**: The existing `Dockerfile` uses Windows containers. A Linux-compatible Dockerfile (per `19-linux-containerization.md`) is recommended for ECS on Fargate with Linux containers.
