# Cost Analysis
## ContosoUniversity — Azure Modernization Cost-Optimized Architecture

---

## 1. Selected Services and Cost Justification

### 1.1 Azure App Service — Linux Basic B1

**Monthly cost:** ~$13.14/month (East US, pay-as-you-go)

| Alternative | Cost | Reason Not Selected |
|---|---|---|
| App Service Linux **Free F1** | $0 | No custom domains, 60 min/day CPU quota, not for production |
| App Service Linux **Shared D1** | ~$9.49 | 240 min/day CPU quota, still insufficient for production |
| App Service **Windows B1** | ~$18.25 | ~39% more expensive than Linux B1; no benefit for cross-platform .NET 8 |
| App Service Linux **Standard S1** | ~$56/month | Auto-scale + staging slots, overkill for low-load university app |
| Azure Container Apps (consumption) | ~$0–15/month | Viable alternative if containerized; slightly more operational complexity |
| Azure VM (B1s) | ~$8/month | IaaS — requires OS patching, IIS/Nginx config, no managed runtime |

**Decision:** Linux B1 provides the right balance of cost and capability for a low-to-medium load educational CRUD app. Upgrade path to B2 or P0v3 is straightforward if load increases.

---

### 1.2 Azure SQL Database — Serverless

**Monthly cost:** ~$5–15/month depending on usage (auto-pause enabled)

| Configuration | Cost | Notes |
|---|---|---|
| Serverless, 0.5–2 vCores, auto-pause 1h | **~$5–15/mo** | ✅ Selected — near-zero cost when idle |
| Basic DTU (5 DTUs) | ~$4.90/mo | Fixed cost, limited 2GB storage, no auto-scale |
| Standard S0 (10 DTUs) | ~$15/mo | Fixed cost whether in use or not |
| Standard S1 (20 DTUs) | ~$30/mo | Over-provisioned for this app |
| General Purpose 2 vCores | ~$186/mo | Production SLA tier, too expensive for university app |
| Azure Database for PostgreSQL Flexible Basic | ~$12/mo | Would require EFCore provider migration and schema re-test |

**Decision:** Serverless SQL is ideal for a university application with predictable off-hours inactivity (nights, weekends). Auto-pause eliminates compute cost when the app is not in use. The free pause window significantly reduces monthly spend compared to any provisioned option.

**Storage cost:** 32GB included in serverless tier — sufficient for a university directory app. At 1M students it would be ~10GB of database data.

---

### 1.3 Azure Blob Storage — LRS Hot

**Monthly cost:** <$1/month

| Metric | Value |
|---|---|
| Storage (estimated) | <100 MB (teaching material images) |
| LRS Hot storage rate | $0.018/GB/month |
| Operations (10K read/write) | ~$0.004/month |
| **Total** | **<$0.10/month** |

| Alternative | Cost | Notes |
|---|---|---|
| ZRS (Zone Redundant) | $0.023/GB | 28% more expensive; unnecessary for non-critical image assets |
| GRS (Geo Redundant) | $0.036/GB | 2× cost; not needed for a single-region university app |
| Azure Files | ~$0.06/GB | More expensive than Blob, designed for SMB/NFS mounts — not needed here |
| AWS S3 (for reference) | $0.023/GB | Not Azure; shown for comparison only |

**Decision:** LRS Hot Blob Storage is the cheapest durable object storage in Azure. Teaching material images are not business-critical data requiring geo-redundancy.

---

### 1.4 Azure Storage Queue

**Monthly cost:** Effectively $0 (shared storage account)

| Metric | Value |
|---|---|
| Queue operations | ~10,000/month (5s polling × users) |
| Storage Queue rate | $0.004 per 10,000 operations |
| Estimated cost | **<$0.01/month** |

| Alternative | Cost | Notes |
|---|---|---|
| Azure Storage Queue | **~$0** | ✅ Selected — included in storage account |
| Azure Service Bus Basic | ~$0.05/million ops | No topics/subscriptions; acceptable but more complex to set up |
| Azure Service Bus Standard | ~$10/month base | Topic/subscription support; overkill for simple notification queue |
| Azure Service Bus Premium | ~$677/month | Enterprise, dedicated — completely out of scope |

**Decision:** Azure Storage Queue provides MSMQ-equivalent semantics (send, receive, delete) at zero marginal cost since the storage account already exists for Blob Storage. Service Bus would cost an additional $10+/month for no meaningful benefit at this scale.

---

### 1.5 Azure Key Vault

**Monthly cost:** ~$0 (free tier sufficient)

| Metric | Value |
|---|---|
| Secrets (connection string, storage key) | 2–4 secrets |
| Key Vault Standard operations | 10,000 ops free per month |
| Estimated cost | **$0/month** |

**Decision:** Key Vault is essentially free at this usage level and eliminates the security risk of storing secrets in configuration files.

---

## 2. Total Cost Summary

| Service | Tier | Monthly Cost |
|---|---|---|
| Azure App Service (Linux B1) | Basic | ~$13 |
| Azure SQL Database (Serverless) | Serverless | ~$5–15 |
| Azure Blob Storage (LRS Hot) | Pay-per-use | <$1 |
| Azure Storage Queue | Pay-per-use | <$1 |
| Azure Key Vault | Standard | ~$0 |
| **Total** | | **~$19–30/month** |

**Annual estimate:** ~$230–360/year

---

## 3. Cost vs. Alternative Architecture

| Architecture | Monthly Cost | Notes |
|---|---|---|
| **This plan** (App Service Linux B1 + SQL Serverless + Blob) | **~$20–30** | ✅ Recommended |
| Windows App Service S1 + SQL Standard S1 + Azure Files | ~$100+ | 3-4× more expensive; Windows + provisioned SQL |
| Azure VM (B2s) + SQL on VM | ~$70+ | IaaS — no managed runtime, operational overhead |
| AKS (3-node dev cluster) + SQL Serverless | ~$120+ | Major overkill for single-app deployment |
| Azure Container Apps (consumption) + SQL Serverless | ~$15–25 | Viable alternative, similar cost, slightly more ops complexity |

---

## 4. Scaling Strategy (Cost-Aware)

### Current Load (Baseline)
- Single App Service instance (B1)
- SQL Serverless auto-pauses after 1 hour of inactivity
- No CDN needed (app serves relatively few users)

### If Load Increases
| Trigger | Action | Cost Impact |
|---|---|---|
| CPU consistently >80% | Scale up B1 → B2 (2 vCore) | +~$13/month |
| Multiple concurrent users | Scale out to 2 instances (B1 × 2) | +~$13/month |
| DB DTU consistently at cap | Move from Serverless to Standard S1 | ~$15/month fixed |
| Globally distributed users | Add Azure CDN for static assets | ~$0.01/GB served |

### Cost Guardrails
- Set **Azure Cost Management budget alerts** at $40/month to detect unexpected spend
- SQL Serverless auto-pause eliminates weekend/night compute costs without manual intervention
- Scale-in rules should be configured if App Service is ever scaled out

---

## 5. Trade-Offs Accepted

| Trade-Off | Impact | Mitigation |
|---|---|---|
| SQL Serverless cold start (~5–10s on first query after auto-pause) | Occasional slow first request | Acceptable for a university app; App Service "Always On" can keep app warm while DB cold-starts are rare |
| LRS storage (no geo-redundancy) | Images lost in datacenter failure (unlikely) | Images are non-critical; database holds the authoritative data |
| Basic App Service (no staging slots) | No zero-downtime deployments | Acceptable for low-traffic app; upgrade to Standard S1 if needed |
| Storage Queue vs. Service Bus | No dead-letter queue, no topics | Notification delivery is best-effort (same as current MSMQ) |
