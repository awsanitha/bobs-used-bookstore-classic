# Assessment Report: BobsBookstoreClassic

## Solution Overview

| Attribute | Value |
|-----------|-------|
| **Solution Name** | BobsBookstoreClassic |
| **Total Projects** | 5 |
| **Target Framework** | net10.0 |
| **Total Lines of Code** | 8592 |
| **Overall Complexity** | Critical |
| **Total NuGet Packages** | 62 (across all projects) |
| **Incompatible Packages** | 14 |
| **.NET Core Readiness** | Not Ready |
| **Linux Readiness** | Not Ready |

## Executive Summary

**Solution Migration Mode: COMPLEX**

BobsBookstoreClassic is a layered ASP.NET MVC 5 e-commerce application following a classic Web → Data → Domain architecture, with a shared constants library (Bookstore.Common) and an AWS CDK infrastructure project (Bookstore.Cdk). The solution spans three .NET Framework 4.8 projects (Bookstore.Web, Bookstore.Data, Bookstore.Domain), one .NET 6.0 project (Bookstore.Cdk), and one .NET Standard 2.0 library (Bookstore.Common) that requires no migration.

The framework spread is significant: the web tier uses ASP.NET MVC 5 with OWIN middleware for OpenID Connect authentication, Autofac for dependency injection, Entity Framework 6 for data access, and NLog with AWS CloudWatch for logging. The data tier uses EF6 with AWS SDK services (S3, Rekognition) and Magick.NET for image processing. The solution is tightly coupled to IIS via System.Web, Global.asax, and OWIN middleware — all of which must be replaced.

- **1 Low-complexity project** (Bookstore.Domain) — pure domain model library with zero NuGet dependencies; only needs project format conversion
- **2 Low-complexity projects** (Bookstore.Common, Bookstore.Cdk) — Common is netstandard2.0 (no migration needed); Cdk is already SDK-style and only needs a TFM bump
- **1 Medium-complexity project** (Bookstore.Data) — EF6 → EF Core migration, project format conversion, AWS SDK upgrades
- **1 Critical-complexity project** (Bookstore.Web) — MVC 5 → ASP.NET Core MVC, OWIN → Core middleware, Autofac.Mvc5 → Core DI, 14 incompatible packages, 15 controllers, 42 Razor views, OpenID Connect auth migration, area registration

The primary transformation challenge is the Bookstore.Web project: it requires a full rewrite of the application startup pipeline (Global.asax + OWIN Startup → Program.cs), authentication infrastructure (OWIN OpenID Connect → ASP.NET Core Authentication), dependency injection wiring (Autofac.Mvc5 → Autofac.Extensions.DependencyInjection or built-in DI), bundling/minification (Web.Optimization → wwwroot static files), and routing (RouteConfig/AreaRegistration → endpoint routing). All 15 controllers must be ported from System.Web.Mvc to Microsoft.AspNetCore.Mvc, and 42 Razor views require namespace and helper updates.

### Key Statistics

| Metric | Count |
|--------|-------|
| Projects requiring format conversion | 3 (legacy to SDK-style) |
| Blocking issues | 0 |
| Razor views to migrate | 42 |
| Controllers to migrate | 15 |
| Total estimated changes | 115 |

## Project Analysis Table

| Project | Current Framework | Target | LOC | Packages | Incompatible | Complexity |
|---------|-------------------|--------|-----|----------|--------------|------------|
| Bookstore.Common | netstandard2.0 | netstandard2.0 | 6 | 0 | 0 | Low |
| Bookstore.Domain | net4.8 | net10.0 | 1813 | 0 | 0 | Low |
| Bookstore.Data | net4.8 | net10.0 | 1042 | 4 | 0 | Medium |
| Bookstore.Cdk | net6.0 | net10.0 | 595 | 4 | 0 | Low |
| Bookstore.Web | net4.8 | net10.0 | 5136 | 54 | 14 | Critical |

## Cross-Project Package Summary

| Package | Used By | Version(s) | Compatible | Notes |
|---------|---------|------------|------------|-------|
| EntityFramework | Bookstore.Data, Bookstore.Web | 6.5.1 | Yes | Targets netstandard2.1; recommend replacing with Microsoft.EntityFrameworkCore for modern .NET |
| AWSSDK.S3 | Bookstore.Data, Bookstore.Web | 3.7.416.5 | Yes | Targets netstandard2.0; upgrade to latest |
| AWSSDK.Rekognition | Bookstore.Data, Bookstore.Web | 3.7.400.129 | Yes | Targets netstandard2.0; upgrade to latest |
| AWSSDK.Core | Bookstore.Web | 3.7.402.35 | Yes | Transitive dependency; targets netstandard2.0 |
| AWSSDK.CloudWatchLogs | Bookstore.Web | 3.7.410.17 | Yes | Targets netstandard2.0; upgrade to latest |
| AWSSDK.SimpleSystemsManagement | Bookstore.Web | 3.7.404.10 | Yes | Targets netstandard2.0; upgrade to latest |
| Microsoft.AspNet.Mvc | Bookstore.Web | 5.3.0 | No | .NETFramework only; replace with ASP.NET Core MVC |
| Microsoft.Owin | Bookstore.Web | 4.2.2 | No | .NETFramework only; replace with ASP.NET Core middleware |
| Microsoft.Owin.Security.OpenIdConnect | Bookstore.Web | 4.2.2 | No | .NETFramework only; replace with Microsoft.AspNetCore.Authentication.OpenIdConnect |
| Autofac | Bookstore.Web | 8.2.1 | Yes | Targets netstandard2.0; use with Autofac.Extensions.DependencyInjection for Core |
| Autofac.Mvc5 | Bookstore.Web | 6.1.0 | No | .NETFramework4.7.2 only; replace with Autofac.Extensions.DependencyInjection |
| Autofac.Owin | Bookstore.Web | 7.1.0 | No | .NETFramework4.7.2 only; remove — use Core middleware pipeline |
| Newtonsoft.Json | Bookstore.Web | 13.0.3 | Yes | Targets netstandard2.0; current version |
| NLog | Bookstore.Web | 5.4.0 | Yes | Targets netstandard2.0+; upgrade to latest |
| Magick.NET-Q8-AnyCPU | Bookstore.Data | 14.6.0 | Yes | Targets netstandard2.0; upgrade to latest |

## Cross-Project Dependencies

Bookstore.Web (Critical)
  - Bookstore.Common (Low)
  - Bookstore.Data (Medium)
    - Bookstore.Domain (Low)
  - Bookstore.Domain (Low)

Bookstore.Cdk (Low)
  - Bookstore.Common (Low)

### Recommended Transformation Order (Dependency-First)

1. **Bookstore.Common** — netstandard2.0 library, no migration needed; leave unchanged
2. **Bookstore.Domain** — leaf library with zero NuGet dependencies; convert project format only
3. **Bookstore.Data** — depends on Bookstore.Domain; convert project format, migrate EF6 → EF Core, upgrade AWS SDK packages
4. **Bookstore.Cdk** — depends on Bookstore.Common; already SDK-style, bump TFM from net6.0 → net10.0, upgrade CDK packages
5. **Bookstore.Web** — depends on Common, Data, and Domain; full MVC 5 → ASP.NET Core MVC migration (migrate last)

## Key Findings

1. **MVC 5 to ASP.NET Core MVC rewrite required**: Bookstore.Web is built on ASP.NET MVC 5 (System.Web.Mvc) with 15 controllers, 42 Razor views, and an Admin area. All controllers, route configuration, filter registration, and view helpers must be ported to ASP.NET Core MVC equivalents.
2. **OWIN authentication pipeline must be replaced**: The application uses Microsoft.Owin with OpenID Connect (Microsoft.Owin.Security.OpenIdConnect) and cookie authentication. This entire pipeline must be rewritten to ASP.NET Core Authentication middleware with `Microsoft.AspNetCore.Authentication.OpenIdConnect`.
3. **Autofac DI container migration**: Autofac.Mvc5 and Autofac.Owin are framework-only. Replace with Autofac.Extensions.DependencyInjection or migrate to ASP.NET Core's built-in DI container.
4. **Entity Framework 6 → EF Core migration**: Both Bookstore.Data and Bookstore.Web reference EntityFramework 6.5.1. While EF6 technically targets netstandard2.1 and could run, migrating to EF Core is strongly recommended for modern .NET alignment, performance, and ongoing support.
5. **Three projects require legacy-to-SDK project format conversion**: Bookstore.Domain, Bookstore.Data, and Bookstore.Web use old-style .csproj with MSBuild imports and explicit Compile includes. All three must be converted to SDK-style format.
6. **Global.asax and BundleConfig must be replaced**: The application uses Global.asax for startup and BundleConfig/Web.Optimization for JS/CSS bundling. These must be replaced with Program.cs and static file serving from wwwroot.
7. **14 incompatible NuGet packages in the web tier**: All ASP.NET MVC 5, OWIN, and bundling-related packages are .NETFramework-only and have no compatible versions for net10.0.
8. **AWS CDK project on end-of-life .NET 6.0**: Bookstore.Cdk targets net6.0 (out of support). It is already SDK-style and only needs a TFM bump to net10.0 with package upgrades.

## External Dependencies

| Dependency | Type | Impact |
|------------|------|--------|
| Amazon S3 | Cloud Service | Used for file storage (S3FileService); AWSSDK.S3 is compatible — upgrade to latest |
| Amazon Rekognition | Cloud Service | Used for image validation (RekognitionImageValidationService); AWSSDK.Rekognition is compatible — upgrade to latest |
| Amazon CloudWatch Logs | Cloud Service | Used for centralized logging via NLog + AWS.Logger.NLog; compatible — upgrade to latest |
| AWS Systems Manager (SSM) | Cloud Service | Used for configuration/secrets (AWSSDK.SimpleSystemsManagement); compatible — upgrade to latest |
| OpenID Connect Identity Provider | Identity | OWIN-based OpenID Connect auth must be migrated to ASP.NET Core Authentication |
| SQL Server | Database | Entity Framework 6 DbContext connects to SQL Server; migrate connection string to appsettings.json and EF Core |
| AWS CDK | Infrastructure | Bookstore.Cdk deploys the application stack; bump TFM and CDK library versions |

## Actionable Next Steps

1. **Phase 1 — Leaf libraries** (Low risk): Convert Bookstore.Domain to SDK-style .csproj targeting net10.0. Bookstore.Common (netstandard2.0) stays unchanged.
2. **Phase 2 — Data layer** (Medium risk): Convert Bookstore.Data to SDK-style, migrate EntityFramework 6 → EF Core, upgrade AWS SDK and Magick.NET packages, replace ConfigurationManager usage with IConfiguration.
3. **Phase 3 — CDK infrastructure** (Low risk): Bump Bookstore.Cdk TFM from net6.0 to net10.0, upgrade Amazon.CDK.Lib, Cdklabs.CdkNag, and Constructs packages.
4. **Phase 4 — Web application** (Critical risk): Full migration of Bookstore.Web — convert project format, replace all 14 incompatible packages, rewrite startup pipeline (Global.asax + OWIN → Program.cs), migrate authentication (OWIN OpenID Connect → ASP.NET Core Authentication), port all controllers and views to ASP.NET Core MVC, replace Autofac.Mvc5 DI wiring, move static assets to wwwroot, convert Web.config to appsettings.json.
5. **Phase 5 — Integration validation** (Medium risk): Full solution build, verify all project references resolve, validate authentication flow end-to-end, confirm AWS service integrations, and test Admin area routing.

---

## Per-Project Assessment Details

### Bookstore.Common

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | netstandard2.0 |
| **Lines of Code** | 6 |
| **NuGet Packages** | 0 |
| **Project References** | 0 |
| **Complexity** | Low |
| **Estimated Changes** | 0 |

#### Migration Analysis

##### Migration Strategy

1. **No migration required.** Bookstore.Common targets netstandard2.0, which is cross-platform compatible with net10.0. The project is already SDK-style and contains a single `Constants.cs` file with shared constants.
2. Leave the project completely unchanged — do not modify `<TargetFramework>`, project format, or any source files.
3. Verify that downstream consumers (Bookstore.Cdk and Bookstore.Web) continue to reference and build against this library after their own migrations.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| None identified | Low | netstandard2.0 is fully compatible with net10.0; no runtime or API risk |

##### Recommendations

1. Keep the project at netstandard2.0 to maximize compatibility across all consuming projects.
2. If desired in the future, the project could be upgraded to net10.0 for access to newer APIs, but this is not required or recommended during this migration.

##### Cross-Project Impact

Bookstore.Common is referenced by Bookstore.Cdk and Bookstore.Web. Since it requires no changes, it poses zero risk to dependent projects and can be treated as stable throughout the migration.

---

### Bookstore.Domain

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net4.8 |
| **Lines of Code** | 1813 |
| **NuGet Packages** | 0 |
| **Project References** | 0 |
| **Complexity** | Low |
| **Estimated Changes** | 1 |


#### Migration Analysis

##### Migration Strategy

1. **Convert from old-style .csproj to SDK-style .csproj** targeting `net10.0`. Remove all `<Import>`, `<PropertyGroup>` build configurations, and explicit `<Compile Include>` items — SDK-style projects auto-include .cs files by convention.
2. **Remove `Properties/AssemblyInfo.cs`** and enable `<GenerateAssemblyInfo>true</GenerateAssemblyInfo>` in the new SDK-style project (the default). Preserve `RootNamespace` (Bookstore.Domain) and `AssemblyName` (Bookstore.Domain) in the `<PropertyGroup>`.
3. **Verify all domain models compile** against net10.0. The project uses only `System.ComponentModel.DataAnnotations` and core BCL types — all available in net10.0 without additional packages.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| Minimal API surface change | Low | Pure POCO domain models with DataAnnotations; no framework-specific APIs used |

##### Recommendations

1. Migrate this project first (after confirming Bookstore.Common is stable) since it is a leaf dependency with no external packages.
2. Run a build after conversion to verify all 44 source files compile cleanly before proceeding to Bookstore.Data.

##### Cross-Project Impact

Bookstore.Domain is referenced by both Bookstore.Data and Bookstore.Web. It must be migrated successfully before either dependent project. As a pure domain model library with no dependencies, it is the lowest-risk starting point.

---

### Bookstore.Data

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net4.8 |
| **Lines of Code** | 1042 |
| **NuGet Packages** | 4 |
| **Project References** | 1 |
| **Complexity** | Medium |
| **Estimated Changes** | 6 |

#### Package Compatibility (4 packages, 0 incompatible)

| Package | Version | Compatibility | Recommendation |
|---------|---------|---------------|----------------|
| AWSSDK.Rekognition | 3.7.400.129 | COMPATIBLE | UpgradePackage |
| AWSSDK.S3 | 3.7.416.5 | COMPATIBLE | UpgradePackage |
| EntityFramework | 6.5.1 | COMPATIBLE | ReplacePackage |
| Magick.NET-Q8-AnyCPU | 14.6.0 | COMPATIBLE | UpgradePackage |

#### Project Dependencies (1)


- Bookstore.Domain
#### Legacy Files Inventory (1 files across 1 kinds)

| Kind | Files |
|------|------:|
| `.config` | 1 |


#### Migration Analysis

##### Migration Strategy

1. **Convert from old-style .csproj to SDK-style .csproj** targeting `net10.0`. Remove all explicit `<Compile Include>` items, `<Import>` statements, assembly `<Reference>` elements with HintPaths, and build property groups. Preserve `RootNamespace` (Bookstore.Data) and `AssemblyName` (Bookstore.Data).
2. **Remove `Properties/AssemblyInfo.cs`** — use SDK-style auto-generation.
3. **Replace EntityFramework 6.5.1 with Microsoft.EntityFrameworkCore.SqlServer** (latest net10.0-compatible version). Migrate `ApplicationDbContext` from `System.Data.Entity.DbContext` to `Microsoft.EntityFrameworkCore.DbContext`. Update `DbSet` configurations, replace `HasRequired`/`HasOptional` fluent API calls with EF Core equivalents, replace `Database.SetInitializer` with EF Core migrations or `EnsureCreated`, and update `BookstoreDbInitializer` seed logic.
4. **Upgrade AWSSDK.Rekognition and AWSSDK.S3** to their latest stable versions (targets netstandard2.0, fully compatible).
5. **Upgrade Magick.NET-Q8-AnyCPU** from 14.6.0 to latest (14.16.0+), which targets netstandard2.0.
6. **Remove `App.config`** — connection strings and configuration will be supplied via `IConfiguration` / `appsettings.json` from the web host.
7. **Replace `System.Configuration.ConfigurationManager` usage** in `BookstoreConfiguration.cs` with injected `IConfiguration` or `IOptions<T>`.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| EF6 → EF Core behavioral differences | Medium | Include/ThenInclude patterns, lazy loading defaults, identity resolution, and seed data initialization differ between EF6 and EF Core |
| ConfigurationManager dependency | Medium | BookstoreConfiguration.cs likely reads from ConfigurationManager — must convert to IConfiguration/IOptions pattern |
| Magick.NET cross-platform | Low | Magick.NET works on Linux but requires native ImageMagick libraries; verify container image includes them |

##### Recommendations

1. Migrate EF6 → EF Core with careful attention to navigation property loading (EF Core defaults to no lazy loading) and seed data initialization.
2. Register `ApplicationDbContext` via DI (`AddDbContext<ApplicationDbContext>`) in the web project's Program.cs.
3. Ensure the `BookstoreConfiguration` class accepts `IConfiguration` via constructor injection rather than reading static config.
4. Test all repository methods after migration, particularly query patterns using Include/ThenInclude.

##### Cross-Project Impact

Bookstore.Data is referenced by Bookstore.Web. The EF Core migration here directly impacts how the web project registers and consumes the DbContext. Bookstore.Data depends on Bookstore.Domain, which must be migrated first.

---

### Bookstore.Cdk

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net6.0 |
| **Lines of Code** | 595 |
| **NuGet Packages** | 4 |
| **Project References** | 1 |
| **Complexity** | Low |
| **Estimated Changes** | 4 |

#### Package Compatibility (4 packages, 0 incompatible)

| Package | Version | Compatibility | Recommendation |
|---------|---------|---------------|----------------|
| Amazon.CDK.Lib | 2.188.0 | COMPATIBLE | UpgradePackage |
| Cdklabs.CdkNag | 2.35.66 | COMPATIBLE | UpgradePackage |
| Constructs | 10.4.2 | COMPATIBLE | UpgradePackage |
| Amazon.Jsii.Analyzers | * | COMPATIBLE | KeepPackage |

#### Project Dependencies (1)

- Bookstore.Common

#### Migration Analysis

##### Migration Strategy

1. **Change `<TargetFramework>` from `net6.0` to `net10.0`** in `Bookstore.Cdk.csproj`. The project is already SDK-style with `<OutputType>Exe</OutputType>`, so no format conversion is needed.
2. **Remove `<RollForward>Major</RollForward>`** — this was set to allow running on newer runtimes; it is no longer needed when targeting net10.0 directly.
3. **Upgrade Amazon.CDK.Lib** to the latest stable version (actively maintained, targets netstandard2.1).
4. **Upgrade Cdklabs.CdkNag and Constructs** to their latest stable versions.
5. **Keep Amazon.Jsii.Analyzers** at wildcard version (`*`) — it is an analyzer-only package with `PrivateAssets="all"`.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| CDK API breaking changes | Low | CDK Lib v2 maintains backward compatibility within major version; minor API adjustments may be needed if jumping many minor versions |

##### Recommendations

1. After bumping the TFM, run `cdk synth` to verify all stacks (CoreStack, DatabaseStack, EcsStack, NetworkStack) synthesize correctly.
2. Update the ECS task definition in EcsStack.cs if it references a .NET 6 base image — it should reference a .NET 10 container image.

##### Cross-Project Impact

Bookstore.Cdk depends on Bookstore.Common (netstandard2.0, no changes needed). The CDK project is independent of the web/data/domain projects and can be migrated in parallel with Bookstore.Data after Bookstore.Common is confirmed stable.

---

### Bookstore.Web

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net4.8 |
| **Lines of Code** | 5136 |
| **NuGet Packages** | 54 |
| **Project References** | 3 |
| **Complexity** | Critical |
| **Estimated Changes** | 104 |

#### Package Compatibility (54 packages, 14 incompatible)

| Package | Version | Compatibility | Recommendation |
|---------|---------|---------------|----------------|
| Antlr | 3.5.0.2 | INCOMPATIBLE | ReplacePackage |
| Autofac | 8.2.1 | COMPATIBLE | KeepPackage |
| Autofac.Mvc5 | 6.1.0 | INCOMPATIBLE | ReplacePackage |
| Autofac.Owin | 7.1.0 | INCOMPATIBLE | ReplacePackage |
| AWS.Logger.Core | 3.3.3 | COMPATIBLE | UpgradePackage |
| AWS.Logger.NLog | 3.3.4 | COMPATIBLE | UpgradePackage |
| AWSSDK.CloudWatchLogs | 3.7.410.17 | COMPATIBLE | UpgradePackage |
| AWSSDK.Core | 3.7.402.35 | COMPATIBLE | UpgradePackage |
| AWSSDK.Rekognition | 3.7.400.129 | COMPATIBLE | UpgradePackage |
| AWSSDK.S3 | 3.7.416.5 | COMPATIBLE | UpgradePackage |
| AWSSDK.SimpleSystemsManagement | 3.7.404.10 | COMPATIBLE | UpgradePackage |
| EntityFramework | 6.5.1 | COMPATIBLE | ReplacePackage |
| jQuery | 3.7.1 | COMPATIBLE | ReplacePackage |
| jQuery.Validation | 1.21.0 | COMPATIBLE | ReplacePackage |
| Microsoft.AspNet.Mvc | 5.3.0 | INCOMPATIBLE | ReplacePackage |
| Microsoft.AspNet.Razor | 3.3.0 | INCOMPATIBLE | ReplacePackage |
| Microsoft.AspNet.Web.Optimization | 1.1.3 | INCOMPATIBLE | ReplacePackage |
| Microsoft.AspNet.WebPages | 3.3.0 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Bcl.AsyncInterfaces | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.Bcl.Memory | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.Bcl.TimeProvider | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.CodeDom.Providers.DotNetCompilerPlatform | 4.1.0 | COMPATIBLE | ReplacePackage |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.3 | COMPATIBLE | UpgradePackage |
| Microsoft.Extensions.Logging.Abstractions | 9.0.3 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.Abstractions | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.JsonWebTokens | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.Logging | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.Protocols | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.Protocols.OpenIdConnect | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.Tokens | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.jQuery.Unobtrusive.Validation | 4.0.0 | COMPATIBLE | ReplacePackage |
| Microsoft.Owin | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Host.SystemWeb | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Security | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Security.Cookies | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Security.OpenIdConnect | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Web.Infrastructure | 2.0.1 | COMPATIBLE | ReplacePackage |
| Modernizr | 2.8.3 | COMPATIBLE | ReplacePackage |
| Newtonsoft.Json | 13.0.3 | COMPATIBLE | KeepPackage |
| NLog | 5.4.0 | COMPATIBLE | UpgradePackage |
| Owin | 1.0 | INCOMPATIBLE | ReplacePackage |
| System.Buffers | 4.6.1 | COMPATIBLE | ReplacePackage |
| System.Diagnostics.DiagnosticSource | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.IdentityModel.Tokens.Jwt | 8.7.0 | COMPATIBLE | UpgradePackage |
| System.IO.Pipelines | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.Memory | 4.6.3 | COMPATIBLE | ReplacePackage |
| System.Numerics.Vectors | 4.6.1 | COMPATIBLE | ReplacePackage |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | COMPATIBLE | ReplacePackage |
| System.Text.Encoding | 4.3.0 | COMPATIBLE | ReplacePackage |
| System.Text.Encodings.Web | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.Text.Json | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.Threading.Tasks.Extensions | 4.6.3 | COMPATIBLE | ReplacePackage |
| System.ValueTuple | 4.6.1 | COMPATIBLE | ReplacePackage |
| WebGrease | 1.6.0 | INCOMPATIBLE | ReplacePackage |

#### Project Dependencies (3)

- Bookstore.Common
- Bookstore.Data
- Bookstore.Domain

#### Legacy Files Inventory (49 files across 3 kinds)

| Kind | Files |
|------|------:|
| `.asax` | 1 |
| `.cshtml` | 42 |
| `.config` | 6 |

#### Migration Analysis

##### Migration Strategy

1. **Convert from old-style .csproj to SDK-style .csproj** using `Microsoft.NET.Sdk.Web`, targeting `net10.0`. Remove all explicit `<Compile Include>`, `<Content Include>`, `<Reference>` with HintPaths, `<Import>` statements, `<ProjectTypeGuids>`, and `<MvcBuildViews>`. Convert `packages.config` to `<PackageReference>` entries. Preserve `RootNamespace` (Bookstore.Web) and `AssemblyName` (Bookstore.Web).
2. **Remove `Properties/AssemblyInfo.cs`** and enable SDK-style auto-generation.
3. **Delete `Global.asax` and `Global.asax.cs`**. Create `Program.cs` with the ASP.NET Core host builder pattern, composing all service registrations and middleware from the current `App_Start/` classes.
4. **Remove all 14 incompatible packages** (Microsoft.AspNet.Mvc, Microsoft.AspNet.Razor, Microsoft.AspNet.WebPages, Microsoft.AspNet.Web.Optimization, Microsoft.Owin.*, Autofac.Mvc5, Autofac.Owin, Owin, Antlr, WebGrease). Add ASP.NET Core equivalents: the framework provides MVC, Razor, and authentication middleware built-in.
5. **Remove all BCL polyfill packages** (System.Buffers, System.Memory, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe, System.Threading.Tasks.Extensions, System.ValueTuple, System.Text.Encoding, System.Text.Encodings.Web, System.Text.Json, System.Diagnostics.DiagnosticSource, System.IO.Pipelines, Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.Memory, Microsoft.Bcl.TimeProvider) — these are inbox in net10.0.
6. **Remove content/tooling packages** (jQuery, jQuery.Validation, Microsoft.jQuery.Unobtrusive.Validation, Modernizr, Microsoft.CodeDom.Providers.DotNetCompilerPlatform, Microsoft.Web.Infrastructure). Deliver client-side assets via wwwroot/LibMan instead of NuGet.
7. **Migrate authentication**: Replace `Startup.cs` OWIN pipeline (`app.UseCookieAuthentication`, `app.UseOpenIdConnectAuthentication`) with ASP.NET Core Authentication in Program.cs (`builder.Services.AddAuthentication().AddCookie().AddOpenIdConnect()`). Port `AuthenticationSetup.cs` logic. Replace `Microsoft.Owin.Security.OpenIdConnect` with `Microsoft.AspNetCore.Authentication.OpenIdConnect`.
8. **Migrate DI container**: Replace Autofac.Mvc5 `DependencyResolver.SetResolver` pattern in `DependencyInjectionSetup.cs` with `Autofac.Extensions.DependencyInjection` (`builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())`) or migrate all registrations to ASP.NET Core's built-in DI.
9. **Port all 15 controllers** from `System.Web.Mvc.Controller` to `Microsoft.AspNetCore.Mvc.Controller`. Update namespaces, replace `HttpPostedFileBase` with `IFormFile`, `FormCollection` with model binding, `MvcHtmlString` with `IHtmlContent`, and `JsonRequestBehavior.AllowGet` with standard `Json()`.
10. **Migrate routing**: Replace `RouteConfig.cs` (`RouteTable.Routes.MapRoute`) and `AdminAreaRegistration.cs` (`AreaRegistration`) with ASP.NET Core endpoint routing (`app.MapControllerRoute`, `app.MapAreaControllerRoute`) in Program.cs.
11. **Migrate all 42 Razor views**: Update `@using` directives from `System.Web.Mvc` to `Microsoft.AspNetCore.Mvc`, replace `@Html.ActionLink`/`@Url.Action` signatures as needed, replace `@Scripts.Render`/`@Styles.Render` (BundleConfig) with direct `<script>`/`<link>` tags referencing wwwroot, and update `_ViewImports.cshtml` with ASP.NET Core tag helpers (`@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`).
12. **Move static assets**: Relocate `Content/` and `Scripts/` directories to `wwwroot/`. Remove `BundleConfig.cs` and all `@Scripts.Render`/`@Styles.Render` calls.
13. **Migrate Web.config**: Convert `<appSettings>` and `<connectionStrings>` to `appsettings.json`. Remove `<system.web>`, `<system.webServer>`, and MVC/OWIN configuration sections. Delete `Web.Debug.config`, `Web.Release.config`, and Views-level `Web.config` files.
14. **Replace `ConfigurationManager` calls** in `ConfigurationSetup.cs` and throughout the project with injected `IConfiguration`.
15. **Migrate logging**: Replace NLog configuration in `LoggingSetup.cs` to use `NLog.Web.AspNetCore` integration with `builder.Host.UseNLog()`. The AWS.Logger.NLog target for CloudWatch remains compatible.
16. **Migrate `FilterConfig.cs`**: Replace `GlobalFilters.Filters.Add(new HandleErrorAttribute())` with ASP.NET Core exception middleware (`app.UseExceptionHandler`) and any custom action filters.
17. **Replace EntityFramework 6.5.1** with the EF Core packages registered in Bookstore.Data (consume via DI in controllers and services).

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| OWIN → Core authentication rewrite | High | OpenID Connect configuration, token validation, and cookie auth must be fully re-implemented in ASP.NET Core middleware; incorrect mapping can break authentication |
| Autofac DI container migration | Medium | All service registrations in DependencyInjectionSetup.cs must be mapped; any use of OWIN per-request lifetime scope or DependencyResolver.Current.GetService must be replaced with constructor injection |
| Admin area routing | Medium | AdminAreaRegistration uses MVC 5 area registration; must convert to ASP.NET Core area routing conventions and ensure controller/view discovery works |
| BundleConfig removal | Medium | All @Scripts.Render/@Styles.Render references in 42 views must be replaced with direct script/link tags; missing any causes runtime 404s for assets |
| HttpContext.Current usage | Medium | Any code using HttpContext.Current (common in MVC 5 helpers like HttpContextExtensions.cs, MvcHelpers.cs) must be replaced with IHttpContextAccessor injection |
| View helper compatibility | Medium | MvcHtmlString, Html.Action, Html.RenderAction (if used), and custom HtmlHelper extensions must be ported to ASP.NET Core equivalents |

##### Recommendations

1. Migrate the web project last, after all dependencies (Domain, Data, Common) are confirmed building on net10.0.
2. Start with the project format conversion and package cleanup (remove incompatible + polyfill packages) to get a compilable SDK-style project, then tackle authentication and DI.
3. Set up a Program.cs with minimal middleware first (static files, routing, controllers) and incrementally add authentication, authorization, and area routing.
4. Test the authentication flow end-to-end early since it is the highest-risk component.
5. Use `IHttpContextAccessor` to replace any `HttpContext.Current` patterns in helper classes.
6. Consider using `NLog.Web.AspNetCore` for seamless NLog integration with ASP.NET Core's logging pipeline.

##### Cross-Project Impact

Bookstore.Web is the top-level application and depends on all other projects (Bookstore.Common, Bookstore.Data, Bookstore.Domain). It must be migrated last. Its migration is gated by Bookstore.Data's EF Core migration (it must consume the new DbContext pattern). Changes to Bookstore.Data's configuration approach (ConfigurationManager → IConfiguration) will be wired in Bookstore.Web's Program.cs.
