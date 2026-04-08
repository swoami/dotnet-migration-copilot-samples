# Migration Risks, Compatibility Issues & Required Refactoring
## ContosoUniversity — .NET Framework 4.8 → .NET 8

---

## 1. High-Risk Items

### RISK-01 — System.Messaging (MSMQ) — Windows-Only
**Severity:** 🔴 HIGH  
**Component:** `ContosoUniversity/Services/NotificationService.cs`  
**Description:**  
The entire notification subsystem depends on `System.Messaging.MessageQueue`, a Windows-only API that requires the MSMQ Windows Feature to be installed. This is fundamentally incompatible with Linux hosting.

**Impact:**  
- Application will not compile or run on Linux without replacement
- `MessageQueue`, `XmlMessageFormatter`, `MessageQueueException`, `MessagePriority` — all Windows-specific

**Required Refactoring:**  
- Replace `NotificationService` with an `Azure.Storage.Queues`-based implementation
- Replace `MessageQueue.Exists()` / `MessageQueue.Create()` with queue client initialization
- Replace `_queue.Send(message)` with `QueueClient.SendMessageAsync(Base64-encoded JSON)`
- Replace `_queue.Receive(timeout)` with `QueueClient.ReceiveMessagesAsync()` + `DeleteMessageAsync()`
- No functional change to callers (`BaseController.SendEntityNotification()` interface stays the same)

**Effort:** 1 day

---

### RISK-02 — System.Web Namespace — Not Available on .NET Core / Linux
**Severity:** 🔴 HIGH  
**Components:** All controllers, `Global.asax`, `App_Start/`, `PaginatedList.cs`  
**Description:**  
The `System.Web` assembly is only available on .NET Framework running on Windows. It provides `HttpApplication`, `HttpContext`, `System.Web.Mvc`, `HttpPostedFileBase`, `HttpStatusCodeResult`, and the entire IIS pipeline.

**Impact:**  
- Cannot reference `System.Web.Mvc` on .NET 8 — must migrate to `Microsoft.AspNetCore.Mvc`
- `HttpPostedFileBase` → `IFormFile`
- `JsonRequestBehavior.AllowGet` → removed (ASP.NET Core allows GET JSON by default)
- `return HttpNotFound()` → `return NotFound()`
- `return new HttpStatusCodeResult(HttpStatusCode.BadRequest)` → `return BadRequest()`
- `[ValidateAntiForgeryToken]` works the same in ASP.NET Core
- `[Bind(Include = "...")]` → use model binding with `[FromForm]` or ViewModel input models

**Required Refactoring:**  
- All 6 controllers + BaseController
- Estimated ~30 method signatures to update

**Effort:** 1.5 days

---

### RISK-03 — Server.MapPath() — IIS-Specific File Path Resolution
**Severity:** 🔴 HIGH  
**Component:** `CoursesController.cs` (Create, Edit, Delete actions)  
**Description:**  
`Server.MapPath("~/Uploads/TeachingMaterials/")` is IIS-specific. On Linux / Kestrel it is not available.

**Impact:**  
- Compile error on ASP.NET Core unless replaced
- Even if replaced with `IWebHostEnvironment.WebRootPath`, local file system storage is incompatible with stateless scale-out and Linux containers

**Required Refactoring:**  
- Replace `Server.MapPath` with `IWebHostEnvironment.WebRootPath` (short term)
- Replace entire local file storage with **Azure Blob Storage** (full resolution)
- Update `Course.TeachingMaterialImagePath` to store Blob URL instead of local relative path
- Update views that render image `<img src="...">` to use Blob URL

**Effort:** 1 day

---

## 2. Medium-Risk Items

### RISK-04 — Global.asax / HttpApplication Startup — Not Portable
**Severity:** 🟡 MEDIUM  
**Component:** `Global.asax.cs`  
**Description:**  
`MvcApplication : HttpApplication` and `Application_Start()` are .NET Framework / IIS lifecycle constructs. ASP.NET Core uses `Program.cs` with `WebApplication.CreateBuilder()`.

**Required Refactoring:**  
- Move `InitializeDatabase()` to `Program.cs` startup
- Move `RegisterRoutes`, `RegisterBundles`, `RegisterGlobalFilters` to ASP.NET Core middleware pipeline
- Remove `Global.asax` file entirely

**Effort:** 0.5 days

---

### RISK-05 — ConfigurationManager — Not Available on .NET Core
**Severity:** 🟡 MEDIUM  
**Components:** `SchoolContextFactory.cs`, `NotificationService.cs`, `Global.asax.cs`  
**Description:**  
`System.Configuration.ConfigurationManager` reads from `Web.config` / `App.config`. This is not the configuration system for ASP.NET Core.

**Required Refactoring:**  
- Replace all `ConfigurationManager.ConnectionStrings["DefaultConnection"]` calls with `IConfiguration["ConnectionStrings:DefaultConnection"]`
- Remove `SchoolContextFactory` and register `SchoolContext` directly in DI (`services.AddDbContext<SchoolContext>(...)`)
- Remove `ConfigurationManager.AppSettings["NotificationQueuePath"]` — inject via `IConfiguration` or `IOptions<T>`

**Effort:** 0.5 days

---

### RISK-06 — App_Start Bundle/Minification — System.Web.Optimization
**Severity:** 🟡 MEDIUM  
**Component:** `App_Start/BundleConfig.cs`  
**Description:**  
`BundleTable.Bundles` and `System.Web.Optimization` are not available on ASP.NET Core.

**Required Refactoring:**  
- Remove `BundleConfig.cs`
- Replace with direct `<script>` and `<link>` tags referencing CDN versions of jQuery/Bootstrap, or use `BuildBundlerMinifier` NuGet package
- The `.js` and `.css` files in `Scripts/` and `Content/` can be served as static files from `wwwroot/`

**Effort:** 0.5 days

---

### RISK-07 — Windows Authentication — Linux Incompatible
**Severity:** 🟡 MEDIUM  
**Component:** Project settings (`IISExpressWindowsAuthentication=enabled`)  
**Description:**  
Windows Authentication (Kerberos/NTLM via IIS) is not available on Linux App Service.

**Required Refactoring:**  
- Implement ASP.NET Core authentication: Cookie auth + Microsoft Entra ID (OIDC), or simple forms-based cookie auth
- Note: current controllers have no `[Authorize]` attributes — auth is effectively not enforced in code; this is a low-urgency change but must be designed before production

**Effort:** 1–2 days

---

### RISK-08 — EF Core 3.1 → EF Core 8 Version Gap
**Severity:** 🟡 MEDIUM  
**Component:** `Data/SchoolContext.cs`, all EF queries  
**Description:**  
While already using EF Core (positive), upgrading from 3.1 to 8 spans multiple major versions with breaking changes.

**Known breaking changes to check:**
- `HasDiscriminator()` API may have minor signature changes
- `Find(id)` returns `T?` (nullable) in EF Core 6+ — null checks may be needed
- `.Single()` on empty result throws — existing code already handles this pattern but should be reviewed
- `DateTime` precision handling may differ slightly

**Required Refactoring:**  
- Update package versions: `Microsoft.EntityFrameworkCore.*` 3.1.32 → 8.x
- Review and test all LINQ queries for behavioral changes
- Run EF Core migrations on fresh database to validate schema generation

**Effort:** 0.5 days

---

## 3. Low-Risk Items

### RISK-09 — Outdated NuGet Packages with Known CVEs
**Severity:** 🟢 LOW  
**Components:** Multiple packages  
**Description:**  
Several packages are outdated:
- `Microsoft.EntityFrameworkCore` 3.1.32 — end-of-life (EOL: Dec 2022)
- `Microsoft.Data.SqlClient` 2.1.4 — multiple CVEs patched in later versions
- `Microsoft.Identity.Client` 4.21.1 — very old (current is 4.60+)
- `jQuery` 3.4.1 — multiple CVEs (XSS); current is 3.7+
- `Newtonsoft.Json` 13.0.3 — current (safe)

**Required Refactoring:**  
- Update all packages to current stable versions as part of the migration

**Effort:** 0.25 days

---

### RISK-10 — No Unit Tests
**Severity:** 🟢 LOW  
**Description:**  
No test project was found in the repository. This increases the risk of regressions during migration, as there is no automated safety net.

**Mitigation:**  
- Prioritize manual smoke testing of all CRUD paths post-migration
- Consider adding a minimal xUnit integration test project for critical database operations
- Use the EF Core InMemory provider for lightweight controller tests

**Effort:** 1–2 days (optional, recommended)

---

### RISK-11 — Notification Model Stored in DB but Queue is Ephemeral
**Severity:** 🟢 LOW  
**Component:** `Notification` model, `SchoolContext.Notifications` DbSet  
**Description:**  
The `Notification` model has a full database table (`Notification`) defined in EF Core, but `NotificationService.MarkAsRead()` is not implemented and notifications are pulled from MSMQ (not the database). This is an inconsistency.

**Required Refactoring:**  
- During migration, decide: use Storage Queue only (ephemeral, current behavior) or persist to DB
- If persisting, implement `MarkAsRead` properly
- If not persisting, remove the `Notification` DbSet and `Notifications` table

**Effort:** 0.25 days

---

### RISK-12 — Path Separator Differences (Windows vs Linux)
**Severity:** 🟢 LOW  
**Description:**  
Any remaining hardcoded `\` (backslash) path separators will break on Linux. `Path.Combine()` is already used in some places which handles this correctly.

**Required Refactoring:**  
- Audit all string paths; ensure `Path.Combine()` is used consistently
- Blob Storage paths use `/` (forward slash) by convention — no issue

**Effort:** 0.25 days

---

## 4. Risk Summary Matrix

| Risk | Severity | Effort | Phase |
|---|---|---|---|
| RISK-01: MSMQ (System.Messaging) | 🔴 HIGH | 1d | Phase 3 |
| RISK-02: System.Web namespace | 🔴 HIGH | 1.5d | Phase 2 |
| RISK-03: Server.MapPath / local file storage | 🔴 HIGH | 1d | Phase 3 |
| RISK-04: Global.asax / HttpApplication | 🟡 MEDIUM | 0.5d | Phase 2 |
| RISK-05: ConfigurationManager | 🟡 MEDIUM | 0.5d | Phase 2 |
| RISK-06: System.Web.Optimization bundles | 🟡 MEDIUM | 0.5d | Phase 2 |
| RISK-07: Windows Authentication | 🟡 MEDIUM | 1–2d | Phase 3 |
| RISK-08: EF Core 3.1 → 8 | 🟡 MEDIUM | 0.5d | Phase 3 |
| RISK-09: Outdated packages / CVEs | 🟢 LOW | 0.25d | Phase 3 |
| RISK-10: No unit tests | 🟢 LOW | Optional | Phase 5 |
| RISK-11: Notification DB/Queue inconsistency | 🟢 LOW | 0.25d | Phase 3 |
| RISK-12: Path separator differences | 🟢 LOW | 0.25d | Phase 4 |
