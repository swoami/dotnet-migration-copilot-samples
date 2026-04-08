# Modernization Plan
## ContosoUniversity — .NET Framework 4.8 → .NET 8 / ASP.NET Core / Azure

---

## Overview

This plan is structured in five phases progressing from analysis through production deployment. Each phase builds on the previous and includes clear entry/exit criteria.

**Total estimated effort:** 7–12 working days (solo developer) | 4–7 days (2-person team)

---

## Phase 1 — Assessment & Environment Preparation
**Goal:** Establish the baseline, set up tooling, and validate the target environment.

### Steps

| # | Task | Dependency | Effort |
|---|---|---|---|
| 1.1 | Review this assessment document and architecture plan | — | 0.5d |
| 1.2 | Provision Azure resources (App Service Linux B1, SQL Database Serverless, Storage Account, Key Vault) | 1.1 | 0.5d |
| 1.3 | Configure Azure SQL Database and verify connectivity from dev machine | 1.2 | 0.5d |
| 1.4 | Set up GitHub Actions CI/CD pipeline stub (build + deploy to App Service) | 1.2 | 0.5d |
| 1.5 | Install .NET 8 SDK and verify local Linux/WSL build environment | — | 0.5d |

**Exit criteria:** Azure resources provisioned, CI pipeline runs (even if app isn't migrated yet), .NET 8 SDK available.

---

## Phase 2 — Project & Framework Migration
**Goal:** Replace the .NET Framework 4.8 project system with a .NET 8 SDK-style project. Remove all `System.Web` dependencies.

### Steps

| # | Task | Dependency | Effort |
|---|---|---|---|
| 2.1 | Create new ASP.NET Core 8 MVC project (SDK-style `.csproj`) | 1.5 | 0.5d |
| 2.2 | Copy source files (Controllers, Models, Data, Services, Views) into new project | 2.1 | 0.5d |
| 2.3 | Replace `Global.asax` + `HttpApplication` with `Program.cs` and `WebApplication.CreateBuilder()` | 2.2 | 0.5d |
| 2.4 | Replace `App_Start/RouteConfig` and `FilterConfig` with ASP.NET Core middleware and routing configuration | 2.3 | 0.5d |
| 2.5 | Replace `App_Start/BundleConfig` (`System.Web.Optimization`) with CDN references or `BundleMinifier` | 2.3 | 0.5d |
| 2.6 | Replace `ConfigurationManager` + `Web.config` connection strings with `IConfiguration` + `appsettings.json` | 2.3 | 0.5d |
| 2.7 | Replace `System.Web.Mvc.Controller` base with `Microsoft.AspNetCore.Mvc.Controller` in all controllers | 2.2 | 1d |
| 2.8 | Fix `HttpPostedFileBase` → `IFormFile` in `CoursesController` file upload actions | 2.7 | 0.5d |
| 2.9 | Fix `HttpStatusCodeResult`, `HttpNotFound()`, `JsonRequestBehavior` → ASP.NET Core equivalents | 2.7 | 0.5d |
| 2.10 | Verify local build succeeds (`dotnet build`) with no errors | 2.9 | 0.25d |

**Exit criteria:** Project builds successfully on .NET 8 with no `System.Web` references.

---

## Phase 3 — Dependency Migration
**Goal:** Replace all Windows-only and outdated dependencies.

### Steps

| # | Task | Dependency | Effort |
|---|---|---|---|
| 3.1 | Upgrade EF Core 3.1.32 → EF Core 8.x (package version bump) | 2.10 | 0.5d |
| 3.2 | Replace `SchoolContextFactory` (uses `ConfigurationManager`) with DI-based `DbContext` registration | 3.1 | 0.5d |
| 3.3 | Update `DbInitializer` to be invoked from `Program.cs` startup via DI | 3.2 | 0.25d |
| 3.4 | Replace `System.Messaging` MSMQ with `Azure.Storage.Queues` SDK in `NotificationService` | 2.10 | 1d |
| 3.5 | Replace local file-system upload logic in `CoursesController` with Azure Blob Storage (`Azure.Storage.Blobs` SDK) | 2.10 | 1d |
| 3.6 | Replace `Newtonsoft.Json` with `System.Text.Json` (or keep Newtonsoft if needed for compatibility) | 2.10 | 0.25d |
| 3.7 | Remove `packages.config` and `Antlr`, `WebGrease`, `Microsoft.Web.Infrastructure`, `Modernizr` legacy packages | 3.1 | 0.25d |
| 3.8 | Add ASP.NET Core Authentication middleware (Entra ID OIDC or Cookie Auth) to replace Windows Auth | 2.10 | 1d |
| 3.9 | Verify build after all dependency changes | 3.8 | 0.25d |

**Exit criteria:** All Windows-only packages removed; Azure SDK packages installed and application compiles.

---

## Phase 4 — Configuration, Views & Static Assets
**Goal:** Finalize configuration management, update views, and validate the full request/response cycle.

### Steps

| # | Task | Dependency | Effort |
|---|---|---|---|
| 4.1 | Create `appsettings.json` with Azure SQL connection string placeholder and Azure Storage settings | 3.9 | 0.25d |
| 4.2 | Configure Azure Key Vault integration (reference Key Vault secrets from App Service settings) | 4.1 | 0.5d |
| 4.3 | Review all Razor views for `System.Web.Mvc.Html` helper incompatibilities | 3.9 | 0.5d |
| 4.4 | Update `_Layout.cshtml` — replace bundle references with direct script/link tags or CDN | 4.3 | 0.25d |
| 4.5 | Update `notifications.js` polling endpoint URL if changed | 4.3 | 0.25d |
| 4.6 | Migrate `Course` model `TeachingMaterialImagePath` to store Blob Storage URL instead of local path | 3.5 | 0.25d |
| 4.7 | Run application locally and smoke-test all CRUD operations | 4.6 | 1d |

**Exit criteria:** Application runs locally on .NET 8, all pages render, CRUD operations work, file upload stores to Blob Storage, notifications deliver via Storage Queue.

---

## Phase 5 — Validation & Deployment
**Goal:** Validate correctness, security, and deploy to Azure.

### Steps

| # | Task | Dependency | Effort |
|---|---|---|---|
| 5.1 | Run EF Core migrations against Azure SQL Database (or seed with `DbInitializer`) | 4.7 | 0.5d |
| 5.2 | Deploy to Azure App Service Linux via GitHub Actions CI/CD | 5.1 | 0.5d |
| 5.3 | Configure App Service Application Settings (connection strings, storage account, queue name) | 5.2 | 0.25d |
| 5.4 | Enable App Service Managed Identity and grant access to Storage Account and Key Vault | 5.3 | 0.25d |
| 5.5 | End-to-end smoke test on production App Service URL | 5.4 | 0.5d |
| 5.6 | Configure App Service **Always On** (or disable if using serverless/scale-to-zero) | 5.5 | 0.25d |
| 5.7 | Enable Azure SQL Database auto-pause (serverless tier) | 5.5 | 0.25d |
| 5.8 | Configure Application Insights for basic telemetry (optional but recommended) | 5.5 | 0.5d |
| 5.9 | Update README with new deployment instructions | 5.8 | 0.5d |

**Exit criteria:** Application running on Azure App Service Linux, all data persists in Azure SQL, images in Blob Storage, notifications via Storage Queue, CI/CD pipeline green.

---

## Phase Dependencies (Waterfall)

```
Phase 1 (Preparation)
    └── Phase 2 (Framework Migration)
            └── Phase 3 (Dependency Migration)
                    └── Phase 4 (Configuration & Views)
                                └── Phase 5 (Validation & Deployment)
```

---

## Parallel Opportunities

These tasks can be parallelized by a two-person team:

- **Track A**: Phase 2 (framework + controller migration) while **Track B**: Azure resource provisioning + CI/CD setup (Phase 1)
- **Track A**: MSMQ→Queue replacement (3.4) while **Track B**: File storage replacement (3.5)
- **Track A**: View review (4.3–4.4) while **Track B**: Key Vault integration (4.2)

---

## Go/No-Go Checklist Before Production

- [ ] All automated builds pass on `main` branch
- [ ] EF Core migrations applied cleanly to Azure SQL Database
- [ ] File uploads write to Azure Blob Storage (not local disk)
- [ ] Notifications deliver via Azure Storage Queue
- [ ] No `System.Web`, `System.Messaging`, or `ConfigurationManager` references remain
- [ ] Secrets in Key Vault (no plaintext connection strings in code or config files)
- [ ] App Service Managed Identity enabled
- [ ] HTTPS enforced (App Service default domain is HTTPS)
- [ ] Application smoke-tested on production URL
