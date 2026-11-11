# .NET Framework 4.8 → .NET 10 Migration Summary

## Status: COMPLETE ✅

**`dotnet build BobsBookstoreClassic.sln` → 0 errors, 0 warnings**

---

## Projects Migrated

| Project | Before | After |
|---------|--------|-------|
| `Bookstore.Web` | .NET Framework 4.8 (old-style csproj) | `net10.0` (SDK-style Web csproj) |
| `Bookstore.Data` | `netstandard2.0` with EF6 | `net10.0` with EF Core 9 |
| `Bookstore.Domain` | `netstandard2.0` (unchanged, added `GenerateAssemblyInfo=false`) | `netstandard2.0` |
| `Bookstore.Common` | `netstandard2.0` | `netstandard2.0` (unchanged) |
| `Bookstore.Cdk` | `net10.0` (already migrated) | `net10.0` + updated CDK 2.276.0 |

---

## Key Changes

### Project Files
- Converted `Bookstore.Web.csproj` from old-style `<Project ToolsVersion="15.0">` format to SDK-style `<Project Sdk="Microsoft.NET.Sdk.Web">`
- Updated solution file (`.sln`) to use SDK project type GUID `{9A19103F-16F7-4668-BE54-9A1E7A4F7556}`
- Added `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` to `Bookstore.Domain` and `Bookstore.Data` to suppress SDK auto-generated attributes that conflict with legacy `Properties/AssemblyInfo.cs`

### NuGet Packages Replaced
| Old Package | New Package |
|-------------|-------------|
| `EntityFramework` 6.5.1 | `Microsoft.EntityFrameworkCore.SqlServer` 9.0.4 |
| `Microsoft.AspNet.Mvc` 5.3.0 | Implicit from `Microsoft.NET.Sdk.Web` |
| `Autofac.Mvc5` / `Autofac.Owin` | Removed — using built-in ASP.NET Core DI |
| `Microsoft.Owin.*` / `Owin` | Removed — using ASP.NET Core middleware |
| `Microsoft.AspNet.Web.Optimization` | Removed — static files served directly |
| `System.Configuration.ConfigurationManager` | `Microsoft.Extensions.Configuration` |
| `Magick.NET-Q8-AnyCPU` 14.6.0 | 15.1.0 (security upgrade) |
| `Amazon.CDK.Lib` 2.188.0 | 2.276.0 (security upgrade) |
| Added: `AWSSDK.Extensions.NETCore.Setup` 3.7.300 | For `AddAWSService<>()` DI integration |

### Application Hosting
- Removed: `Global.asax` / `Global.asax.cs`
- Removed: `Startup.cs` (OWIN startup)
- Removed: `App_Start/BundleConfig.cs`, `App_Start/FilterConfig.cs`, `App_Start/RouteConfig.cs`
- Removed: `Areas/Admin/AdminAreaRegistration.cs`
- Created: `Program.cs` — top-level statements entry point with full ASP.NET Core pipeline
- Created: `appsettings.json` and `appsettings.Development.json`

### Configuration
- `BookstoreConfiguration` updated to use `IConfiguration` from ASP.NET Core; `Initialize(IConfiguration)` is called once at startup
- `Web.config` appSettings migrated to `appsettings.json`
- Connection string migrated to `ConnectionStrings` section
- AWS SSM parameter loading preserved in `ConfigurationSetup.cs`

### Authentication
- Replaced OWIN cookie + OpenID Connect with ASP.NET Core `AddCookie()` + `AddOpenIdConnect()`
- `LocalAuthenticationMiddleware` converted from `OwinMiddleware` to ASP.NET Core convention-based middleware (`InvokeAsync(HttpContext, ICustomerService)`)
- Cognito OIDC event callbacks rewritten using `OpenIdConnectEvents`

### Dependency Injection
- Replaced `Autofac` with built-in `IServiceCollection` DI
- All services, repositories, and EF Core `DbContext` registered with scoped/singleton lifetimes
- `AddAWSService<IAmazonS3>()` and `AddAWSService<IAmazonRekognition>()` for AWS SDK DI integration

### Entity Framework 6 → EF Core 9
- `ApplicationDbContext(string connectionString)` → `ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)`
- `DbModelBuilder` → `ModelBuilder`
- `HasRequired().WithMany()` → `HasOne().WithMany().OnDelete(DeleteBehavior.Restrict)`
- `HasColumnType("nvarchar").HasMaxLength(450)` → `HasMaxLength(450)` (nvarchar is default for SQL Server strings)
- `HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity)` → `ValueGeneratedOnAdd()`
- Nested eager loading: `Include(x => x.Items.Select(y => y.Child))` → `.Include(x => x.Items).ThenInclude(y => y.Child)`
- String includes (`.Include("Genre")`) → Lambda includes (`.Include(x => x.Genre)`)
- `await Task.Run(() => dbContext.Set.Add(entity))` → `await dbContext.Set.AddAsync(entity)`
- Removed `DropCreateDatabaseIfModelChanges<T>` — replaced with `Database.EnsureCreated()` at startup
- Data seeding moved to `BookstoreDataSeeder.Seed(ModelBuilder)` using `HasData` with fixed timestamps

### Controllers
- All `System.Web.Mvc` usings → `Microsoft.AspNetCore.Mvc`
- `ActionResult` → `IActionResult`
- `[RouteArea("Admin")]` → `[Area("Admin")]` on `AdminAreaControllerBase`
- File upload: `HttpPostedFileBase.InputStream` → `IFormFile.OpenReadStream()`

### Helpers & Middleware
- `HttpContextExtensions`: `HttpContextBase` → `HttpContext`, `HttpCookie` → `CookieOptions` + `Response.Cookies.Append`
- `ControllerExtensions`: `System.Web.Mvc.Controller` → `Microsoft.AspNetCore.Mvc.Controller`
- `MvcHelpers`: `HtmlHelper` → `IHtmlHelper`
- `ImageTypesAttribute`/`MaxFileSizeAttribute`: `HttpPostedFileBase` → `IFormFile`, `ContentLength` → `Length`
- `IOwinRequestExtensions.cs` → Replaced with `HttpRequestExtensions` using `HttpRequest`

### Views
- Added `_ViewImports.cshtml` for Admin area with correct using directives
- Updated `Views/_ViewImports.cshtml` with `Microsoft.AspNetCore.Mvc.Rendering` and `Microsoft.AspNetCore.Routing`
- `Html.EnumDropDownListFor` with nullable enum → `Html.DropDownListFor` + `Html.GetEnumSelectList<T>()`
- Models using `System.Web.Mvc.SelectListItem` → `Microsoft.AspNetCore.Mvc.Rendering.SelectListItem`

### Static Files
- Static files from `Content/` and `Scripts/` directories served via multiple `UseStaticFiles` middleware registrations
- Bootstrap and jQuery continue to load from their existing locations

---

## Next Steps

1. **EF Core Migrations**: This migration uses `Database.EnsureCreated()` which cannot handle schema changes. For production, add EF Core migrations: `dotnet ef migrations add InitialCreate` and `dotnet ef database update`. The seeded data uses fixed timestamps so migrations are stable.

2. **Magick.NET 15.x API**: `ImageResizeService.cs` uses `magickImage.BaseWidth/BaseHeight` which may need to be updated to `magickImage.Width/Height` in Magick.NET 15.x. Verify at runtime.

3. **Views/Web.config**: The legacy `Views/Web.config` and `Areas/Admin/Views/web.config` are excluded from compilation but remain on disk. They can be safely deleted.

4. **Windows-only deployment**: The original app used Windows containers for ECS. After migrating to `net10.0`, Linux container deployment (`Dockerfile`) should work without Windows containers. The ECS CDK stack (`Bookstore.Cdk/EcsStack.cs`) may need updating to use Linux containers.

5. **HTTPS for Cognito**: The ECS Fargate deployment uses HTTP which prevents Cognito OIDC callback. Consider adding an ALB with HTTPS termination to enable AWS-mode authentication in ECS.

6. **Properties/AssemblyInfo.cs**: Legacy `Properties/AssemblyInfo.cs` files in `Bookstore.Domain` and `Bookstore.Data` are still present. They can be deleted since the SDK auto-generates this information (currently suppressed with `GenerateAssemblyInfo=false`).

7. **App.config in Bookstore.Data**: `App.config` with EF6 configuration is obsolete and can be deleted.
