# Azure Modernization Assessment Report
## ContosoUniversity — .NET Framework 4.8 Application

**Assessment Date:** 2026-04-08  
**Assessed By:** Azure Modernization Architect (AI-assisted)  
**Repository:** swoami/dotnet-migration-copilot-samples  

---

## 1. Executive Summary

ContosoUniversity is an ASP.NET MVC 5 web application running on .NET Framework 4.8. It manages university entities (students, instructors, courses, departments) and includes a file-upload feature and a Windows-only MSMQ-based notification system. The application is a strong candidate for modernization to .NET 8 on ASP.NET Core, targeting Linux-based Azure hosting for cost savings.

**Modernization Complexity: MEDIUM-HIGH**

The primary complexity drivers are:
- Hard dependency on `System.Messaging` (MSMQ) — Windows-only
- Full `System.Web` stack (ASP.NET MVC 5) — not portable to Linux as-is
- Local file-system storage for uploaded teaching-material images

Mitigating factors that reduce overall risk:
- Data layer already uses **EF Core 3.1.32** (not classic EF6), making the jump to EF Core 8 minimal
- `Microsoft.Extensions.*` packages (DI, Logging, Caching, Configuration) already referenced — patterns are already cloud-friendly
- Standard MVC Razor views translate directly to ASP.NET Core Razor views with minor syntax changes
- No COM interop, P/Invoke, or registry access detected
- No Windows Services or scheduled background jobs beyond MSMQ consumption

---

## 2. Application Architecture — Current State

### 2.1 Application Type
| Attribute | Value |
|---|---|
| Framework | .NET Framework 4.8 |
| Web framework | ASP.NET MVC 5.2.9 |
| Hosting | IIS / IIS Express |
| Authentication | Windows Authentication (IIS-level, no custom auth code) |
| Entry point | `Global.asax` / `HttpApplication` |

### 2.2 Layer Structure
```
ContosoUniversity/
├── App_Start/          # MVC bootstrapping (BundleConfig, FilterConfig, RouteConfig)
├── Controllers/        # MVC controllers (BaseController + 6 entity controllers)
├── Data/               # EF Core DbContext, factory, seed initializer
├── Models/             # Domain models + ViewModels
├── Services/           # NotificationService (MSMQ), LoggingService
├── Views/              # Razor .cshtml views
├── Uploads/            # Local file-system storage for teaching material images
├── Scripts/            # jQuery 3.4.1, Bootstrap 5.3.3, validation, notifications.js
├── Content/            # CSS (Bootstrap, Site.css, notifications.css)
├── Web.config          # Configuration (connection strings, app settings)
└── Global.asax         # Application startup
```

### 2.3 Domain Entities
- **Student** / **Instructor** (TPH inheritance via `Person` base class)
- **Course** (with optional `TeachingMaterialImagePath`)
- **Department** (with budget tracking)
- **Enrollment** (student-course link with Grade)
- **CourseAssignment** (instructor-course many-to-many)
- **OfficeAssignment** (instructor office, one-to-one)
- **Notification** (CRUD audit events from MSMQ)

### 2.4 External Integrations
| Integration | Technology | Notes |
|---|---|---|
| Database | SQL Server (LocalDB in dev) | EF Core 3.1.32, Code First migrations implied |
| Messaging | MSMQ (System.Messaging) | Windows-only private queue |
| File Storage | Local file system (`~/Uploads/`) | `Server.MapPath` — IIS-specific |
| Auth identity | MSAL (Microsoft.Identity.Client 4.21.1) | Referenced but auth not wired in controllers |
| JSON | Newtonsoft.Json 13.0.3 | Serialization for MSMQ messages |

---

## 3. Technical Constraints Analysis

### 3.1 Windows-Only Dependencies (Blockers for Linux)
| Component | API / Package | Impact | Replacement |
|---|---|---|---|
| MSMQ notification service | `System.Messaging` | **HIGH** — whole notification subsystem | Azure Service Bus or Azure Storage Queue |
| IIS hosting | `IISExpressWindowsAuthentication`, `System.Web` pipeline | **HIGH** — entire web host | Kestrel on ASP.NET Core (Linux-native) |
| File path resolution | `Server.MapPath(...)` | **MEDIUM** — 2 methods in CoursesController | `IWebHostEnvironment.WebRootPath` |
| Startup lifecycle | `Global.asax` / `HttpApplication` | **MEDIUM** — application bootstrap | `Program.cs` + `Startup.cs` (ASP.NET Core) |
| Bundle & minification | `System.Web.Optimization` | **LOW** — static asset pipeline | ASP.NET Core `BundleMinifier` or Vite/npm |
| Windows Auth | IIS integrated Windows Auth | **LOW** — no actual auth in controllers | Azure Entra ID or cookie auth |

### 3.2 IIS-Specific Features Used
- `<system.webServer>` settings (request size limits, pipeline mode)
- `maxRequestLength` / `maxAllowedContentLength` — maps to ASP.NET Core request body limits
- Integrated Windows Authentication at IIS level

### 3.3 File System Usage
- Teaching material images stored in `~/Uploads/TeachingMaterials/`
- `Server.MapPath` used in `CoursesController.Create` and `CoursesController.Edit`
- Old images deleted when courses are updated/deleted (local file I/O)
- Not suitable for scale-out / Linux containers without shared persistent storage

### 3.4 Background Jobs / Schedulers
- No `HostedService`, `Quartz.NET`, or Windows Services found
- MSMQ consumer is synchronous — polled on each `GET /Notifications` HTTP request
- No cron jobs or timer-based tasks detected

---

## 4. Data Layer

| Attribute | Value |
|---|---|
| ORM | Entity Framework Core 3.1.32 |
| Database | SQL Server (Microsoft.Data.SqlClient 2.1.4) |
| Schema management | Code First (DbInitializer seed, no explicit migration files found) |
| Patterns | Repository-less (direct DbContext injection via factory) |
| Relationships | TPH inheritance, one-to-one, one-to-many, many-to-many |
| Data types | `datetime2` explicitly configured for all DateTime columns |
| Connection string | Web.config `DefaultConnection` (LocalDB `MSSQLLocalDB` in dev) |

**Notes:**
- Already on EF Core — the path to EF Core 8 is straightforward (target framework + package version bump)
- `SchoolContextFactory.Create()` uses `ConfigurationManager` — must be replaced with `IConfiguration` (ASP.NET Core)
- `DbInitializer.Initialize(context)` is called at startup — can be preserved with minor changes

---

## 5. Performance & Usage Profile

| Attribute | Assessment |
|---|---|
| Application type | Interactive CRUD web app |
| Expected load | Low-to-medium (university internal tool) |
| Latency sensitivity | Standard web latency acceptable (<500ms) |
| Batch processing | None |
| Real-time requirements | Near-real-time notifications (5s polling — not true real-time) |
| Session state | No server-side session state detected |
| Caching | `Microsoft.Extensions.Caching.Memory` referenced but not actively used in current code |
| File upload size | Max 5MB per image, 10MB request limit |

---

## 6. Modernization Complexity Summary

| Category | Complexity | Effort |
|---|---|---|
| Framework upgrade (ASP.NET MVC 5 → ASP.NET Core 8) | HIGH | 3-5 days |
| MSMQ → Azure Service Bus / Storage Queue | MEDIUM | 1-2 days |
| Local file storage → Azure Blob Storage | MEDIUM | 1 day |
| Configuration (Web.config → appsettings.json) | LOW | 0.5 days |
| EF Core 3.1 → EF Core 8 | LOW | 0.5 days |
| Authentication setup (Entra ID or cookie) | MEDIUM | 1-2 days |
| CI/CD pipeline and Docker/App Service packaging | LOW | 0.5 days |
| **Total estimated effort** | | **7–12 working days** |

---

## 7. Key Assumptions

1. The SQL Server database will be migrated to **Azure SQL Database** (not kept on-premises).
2. No Windows Service / worker process exists outside this web application.
3. Current Windows Authentication is a development convenience — no production AD integration is required; simpler auth (Entra ID external or cookie-based) is acceptable.
4. The MSMQ notification system is a non-critical feature (best-effort delivery) and can be replaced with Azure Storage Queue (cheaper) rather than premium Service Bus.
5. The `TeachingMaterialImagePath` values stored in the database will be updated to reference Azure Blob Storage URLs during migration.
6. No existing unit or integration tests are present in the repository (none found in file listing).
7. Target is a **single-region** deployment (multi-region not needed for a university app at this scale).
