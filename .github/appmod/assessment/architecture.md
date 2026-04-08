# Target Azure Architecture
## ContosoUniversity — Linux-First, Cost-Optimized

---

## 1. Architecture Decision Summary

The target architecture replaces every Windows-only dependency with a cross-platform or managed Azure equivalent, enabling the application to run on Linux-based compute for lower cost and broader portability.

```
┌─────────────────────────────────────────────────────────────────┐
│                        Internet / Users                         │
└──────────────────────────────┬──────────────────────────────────┘
                               │ HTTPS
                   ┌───────────▼────────────┐
                   │  Azure App Service     │
                   │  (Linux, .NET 8)       │
                   │  ASP.NET Core MVC      │
                   │  Kestrel web server    │
                   └────┬──────┬────────────┘
                        │      │
           ┌────────────▼─┐  ┌─▼──────────────────┐
           │ Azure SQL DB │  │ Azure Blob Storage  │
           │ (Serverless) │  │ (teaching materials)│
           └──────────────┘  └─────────────────────┘
                        │
           ┌────────────▼──────────────┐
           │ Azure Storage Queue       │
           │ (replaces MSMQ)           │
           └───────────────────────────┘
```

---

## 2. Selected Azure Services

### 2.1 Compute — Azure App Service (Linux)

| Property | Value |
|---|---|
| Service | Azure App Service |
| OS | **Linux** |
| Runtime | .NET 8 |
| Tier | **Basic B1** (1 vCore, 1.75 GB RAM) — scalable to B2 if needed |
| Deployment | ZIP deploy / GitHub Actions |

**Justification:**
- App Service eliminates IIS management overhead; Kestrel runs natively on Linux
- Linux App Service plans are **~30% cheaper** than equivalent Windows plans for the same SKU
- No OS patching, no IIS configuration — fully managed PaaS
- Scale-out to 3 instances on Basic tier if load increases without moving to Premium
- Docker containerization is optional but supported if portability is needed later
- **No need for Azure Kubernetes Service** — the app is a simple MVC web app; AKS would over-engineer and over-cost

**Linux Compatibility Notes:**
- All `System.Web` dependencies removed; ASP.NET Core is fully cross-platform
- `Server.MapPath` replaced with `IWebHostEnvironment.WebRootPath` — Linux path separator safe
- No P/Invoke or Windows DLL dependencies remain after migration
- `System.Messaging` (MSMQ) fully removed and replaced (see §2.4)

---

### 2.2 Database — Azure SQL Database (Serverless)

| Property | Value |
|---|---|
| Service | Azure SQL Database |
| Model | **Serverless** (auto-pause after 1 hour of inactivity) |
| vCores | 0.5–2 vCores (auto-scale) |
| Max storage | 32 GB (expandable) |
| Redundancy | Locally redundant (LRS) — sufficient for university use case |

**Justification:**
- Azure SQL Database is fully managed, no patching or backup management required
- **Serverless tier** is ideal for a university CRUD app with intermittent load — the database pauses when idle, reducing cost to near-zero outside business hours
- EF Core 8 works with Azure SQL Database with zero code changes beyond the connection string
- Supports all SQL Server features used (datetime2, TPH, composite keys, stored proc if needed later)
- **Not chosen:** Azure Database for PostgreSQL — would require EFCore provider change and schema adjustments; SQL Server continuity reduces migration risk

---

### 2.3 File Storage — Azure Blob Storage

| Property | Value |
|---|---|
| Service | Azure Blob Storage |
| Container | `teaching-materials` (private, access via SAS or App Service Managed Identity) |
| Redundancy | LRS (Locally Redundant Storage) |
| Access tier | **Hot** (active images served frequently) |

**Justification:**
- Eliminates local file system dependency — compatible with Linux, scale-out, and container deployments
- Blob Storage is the cheapest Azure storage option (~$0.018/GB/month for hot LRS)
- Managed Identity allows the App Service to access Blob Storage without storing credentials
- Images served via SAS URL or proxied through the application (keeps current UX)
- **Not chosen:** Azure Files — higher cost, not needed for simple blob storage

---

### 2.4 Messaging — Azure Storage Queue

| Property | Value |
|---|---|
| Service | Azure Storage Queue |
| Queue name | `contosouniversity-notifications` |
| Message retention | 7 days (default) |
| Visibility timeout | 30 seconds |

**Justification:**
- Direct replacement for MSMQ semantics (send/receive/delete message pattern)
- Azure Storage Queue is **included in the same storage account** as Blob Storage — zero additional service cost
- Supports JSON message payloads (current Newtonsoft.Json serialization preserved)
- The current 5-second JavaScript polling pattern translates directly
- **Not chosen:** Azure Service Bus — Service Bus (Basic tier ~$0.05/million ops, Standard ~$10/month base) is overkill for a simple notification queue with a single consumer; Storage Queue is effectively free at this scale

---

### 2.5 Configuration & Secrets — Azure App Service Application Settings + Azure Key Vault

| Property | Value |
|---|---|
| App settings | Azure App Service Application Settings (environment variables) |
| Secrets | Azure Key Vault (connection strings, storage account keys) |
| Access | App Service Managed Identity → Key Vault |

**Justification:**
- Web.config connection strings and app settings map directly to App Service Application Settings (environment variables)
- Key Vault prevents secrets from being stored in source code or config files
- Managed Identity eliminates credential management
- No separate configuration service (Azure App Configuration) needed at this scale

---

### 2.6 Authentication — Microsoft Entra ID (External Identities)

| Property | Value |
|---|---|
| Service | Microsoft Entra External ID (formerly Azure AD B2C) |
| Flow | Email + password, or Entra organizational account |
| Integration | ASP.NET Core built-in OpenID Connect middleware |

**Justification:**
- Current Windows Authentication is IIS-specific and cannot run on Linux
- Entra External ID provides a managed identity provider with free tier (50,000 MAU free)
- ASP.NET Core has first-class MSAL / Entra integration
- Alternatively, simple **ASP.NET Core Cookie Authentication** with a local user store can be used if AD integration is not needed (simplest, zero Azure cost)

---

## 3. High-Level Architecture Diagram (Textual)

```
[Browser]
   │ HTTPS
   ▼
[Azure App Service — Linux, .NET 8, Basic B1]
   │
   ├──► [Azure SQL Database — Serverless]
   │       • EF Core 8 Code First
   │       • ContosoUniversity schema
   │
   ├──► [Azure Blob Storage — LRS Hot]
   │       • Container: teaching-materials
   │       • Managed Identity access
   │       • Course teaching material images
   │
   ├──► [Azure Storage Queue]
   │       • Queue: contosouniversity-notifications
   │       • JSON notification messages (replaces MSMQ)
   │       • Polled by JS every 5s via /Notifications endpoint
   │
   └──► [Azure Key Vault] (optional enhancement)
           • Connection string secret
           • Storage account key / connection string
           • App Service Managed Identity access
```

---

## 4. Linux Compatibility Considerations

| Item | Current (Windows/IIS) | Target (Linux/Kestrel) |
|---|---|---|
| Web server | IIS + `System.Web` pipeline | Kestrel (cross-platform, built into ASP.NET Core) |
| Startup | `Global.asax`, `HttpApplication` | `Program.cs`, `WebApplication.CreateBuilder()` |
| Routing | `RouteConfig.RegisterRoutes` | Attribute routing + conventional routing (ASP.NET Core) |
| Bundling | `System.Web.Optimization` | `BundleMinifier` NuGet or CDN links |
| File paths | `Server.MapPath("~/Uploads/")` | `IWebHostEnvironment.WebRootPath + "/uploads/"` → replaced with Azure Blob |
| Config | `ConfigurationManager` + Web.config | `IConfiguration` + `appsettings.json` + environment variables |
| Messaging | `System.Messaging` (MSMQ) | `Azure.Storage.Queues` SDK |
| Auth | Windows Auth (IIS) | ASP.NET Core OpenID Connect / Cookie Auth |
| Anti-forgery | `@Html.AntiForgeryToken()` | `@Html.AntiForgeryToken()` (unchanged in Razor) |

---

## 5. Cost Optimization Decisions

| Decision | Saving |
|---|---|
| Linux App Service over Windows | ~30% lower hourly compute cost |
| App Service Basic B1 over Standard S1 | Sufficient for low-medium load; upgrade only if needed |
| SQL Serverless over provisioned | Near-zero cost when idle (auto-pause); ~60-80% saving for intermittent workloads |
| Storage Queue over Service Bus Standard | ~$10/month savings on messaging |
| LRS redundancy over ZRS/GRS | ~50% storage cost saving; acceptable for non-critical university data |
| Single region deployment | Eliminates geo-redundancy cost; acceptable for this use case |
| Managed Identity over API key rotation | Operational cost saving; no secret rotation complexity |

**Estimated monthly cost (Basic tier):**
| Service | Estimated Cost |
|---|---|
| App Service Linux B1 | ~$13/month |
| Azure SQL Database Serverless (0.5–2 vCore) | ~$5–15/month (usage-based) |
| Azure Blob Storage (<1 GB) | <$1/month |
| Azure Storage Queue | <$1/month |
| Azure Key Vault | ~$0 (free tier for secrets) |
| **Total** | **~$20–30/month** |
