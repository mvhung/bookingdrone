# AGENTS.md - EventMap & Crew Scheduler

Comprehensive architectural reference, development guidelines, and operational instructions for AI coding agents and human developers maintaining this repository.

---

## 1. Project Overview & Philosophy

**EventMap & Crew Scheduler** is a specialized, production-ready fullstack system designed for a tight-knit team of 8-10 event production professionals (FPV Drone, Butterfly Drone, Flycam, Camera Operators, Photographers).

### Core Philosophy
1. **Map-First & Minimalist**: The primary interface is a 100vw x 100vh full-screen interactive Leaflet map. No complex side navigation or dashboards.
2. **Location Name IS Job Title**: Venue/hall names automatically resolve from coordinates using the Overpass API (85m radius POI) with Photon fallback, and are editable inline.
3. **Single Adaptive Drawer**: A single 400px side window handles both Create and Edit workflows.
4. **Strict Role-Based Privacy**: 
   - Team members ONLY see jobs where they are the **Job Owner (`OwnerUserId`)** or an **Assigned Crew Member (`JobAssignments`)**.
   - Team members cannot see other members' unrelated jobs, venues, or schedules.
5. **Role-Based Financial Masking**:
   - Only the Job Owner/Creator sees total client revenue and each member's payout.
   - Assigned crew members only see their own payout. Total revenue and other members' payouts are masked to `null`.
6. **Strict Ownership & Permissions**:
   - Only the Creator or Owner (`canEdit === true`) can update or delete a job, adjust payouts, or change assignments.
   - Assigned crew members have **Read-Only** access.

---

## 2. Monorepo Repository Structure

```
/
├── AGENTS.md                     # Agent guide and system documentation
├── README.md                     # Repository overview & quickstart
├── .gitignore                    # Root gitignore (.NET + Node)
├── backend/                      # ASP.NET Core 8 Web API
│   ├── Controllers/
│   │   ├── AuthController.cs     # PIN login, Avatar picker, Current user
│   │   ├── JobsController.cs     # Strict visibility, financial masking & ownership
│   │   ├── AvailabilityController.cs # Blind availability engine (Rule C)
│   │   ├── ProfileController.cs  # Services, office hours, custom off-days
│   │   ├── ServicesController.cs # Dynamic service catalog management
│   │   └── AdminController.cs    # User CRUD, reset PIN & permissions
│   ├── Data/
│   │   ├── AppDbContext.cs       # EF Core mappings, precisions, relations
│   │   └── DbInitializer.cs      # Seed data: 8 members & October 2026 jobs
│   ├── DTOs/                     # Strongly-typed Request/Response DTOs
│   ├── Models/                   # User, ServiceType, EventJob, JobAssignment, UserBusyDate
│   ├── Services/                 # JwtService
│   ├── Program.cs                # Dependency injection, JWT, CORS, Swagger
│   └── appsettings.json          # SQL Server connection string & JWT key
└── frontend/                     # Vue 3 + TypeScript + TailwindCSS v4 + Vite
    ├── src/
    │   ├── components/
    │   │   ├── MapView.vue       # 100% Leaflet Map, custom pill markers, Overpass POI
    │   │   ├── FloatingHeader.vue# Photon search, Date filter, View switcher, Excel, Profile
    │   │   ├── JobSideDrawer.vue # Adaptive drawer with role-based editing & view
    │   │   ├── ProfileModal.vue  # Skill toggles, fixed office hours, link to calendar
    │   │   ├── CalendarView.vue  # Google Calendar style monthly schedule & busy dates
    │   │   ├── AdminModal.vue    # Admin panel for user management & service types
    │   │   └── PinLoginModal.vue # Avatar selector + 4-digit numeric keypad
    │   ├── stores/               # Pinia stores (auth.ts, jobs.ts)
    │   ├── services/             # api.ts (fetch wrapper with JWT)
    │   ├── types/                # TypeScript interfaces and constants
    │   ├── style.css             # Tailwind v4 import & custom pin styles
    │   └── App.vue               # Root layout
```

---

## 3. Database Schema (EF Core / SQLite)

1. **`Users`**:
   - `Id` (int PK)
   - `Name` (nvarchar(100))
   - `PinHash` (nvarchar(255) - BCrypt hashed 4-digit PIN)
   - `AvatarUrl` (nvarchar(500))
   - `ServicesJson` (nvarchar(1000) - `["BUTTERFLY", "FPV", "FLYCAM", "CAMERA", "PHOTO"]`)
   - `IsAdmin` (bit - admin privilege to manage users and services)
   - `OfficeBusyEnabled` (bit)
   - `OfficeBusyStart` (time, default `08:00`)
   - `OfficeBusyEnd` (time, default `17:30`)

2. **`ServiceTypes`**:
   - `Id` (int PK)
   - `Code` (nvarchar(50) UNIQUE - e.g. `BUTTERFLY`, `FPV`, `FLYCAM`, `CAMERA`, `PHOTO`, `DRONE_LIGHT`)
   - `Name` (nvarchar(100) - e.g. `Bướm`, `Flycam`, `Trình diễn drone`)
   - `Icon` (nvarchar(50) - emoji/icon `🦋`, `🚁`, `✨`)
   - `Description` (nvarchar(255), nullable)
   - `IsActive` (bit - soft activation status)
   - `DisplayOrder` (int)

3. **`UserBusyDates`**:
   - `Id` (int PK)
   - `UserId` (int FK -> Users, cascade delete)
   - `BusyDate` (date)
   - `StartTime`, `EndTime` (time, nullable - support specific hour ranges or full day)
   - `Reason` (nvarchar(255))

4. **`EventJobs`**:
   - `Id` (int PK)
   - `CreatedByUserId` (int FK -> Users, restrict delete)
   - `OwnerUserId` (int? FK -> Users, nullable for external clients)
   - `ExternalClientName` (nvarchar, nullable)
   - `EventDate` (date)
   - `StartTime`, `EndTime` (time)
   - `ServiceCode` (nvarchar(50) - dynamic service code)
   - `LocationName` (nvarchar(255) - Serves as primary Job Title)
   - `SubAddress` (nvarchar(500))
   - `Latitude`, `Longitude` (`decimal(9,6)`)
   - `ClientTotalRevenue` (`decimal(18,0)`, nullable)
   - `ExternalWorkersNote` (nvarchar(500) - external freelancers)
   - `Note` (nvarchar(1000))

5. **`JobAssignments`**:
   - `Id` (int PK)
   - `JobId` (int FK -> EventJobs, cascade delete)
   - `UserId` (int FK -> Users, restrict delete)
   - `CastAmount` (`decimal(18,0)`)
   - `IsPaid` (bit - payment status)
   - `PersonalNote` (nvarchar(500))

---

## 4. Key Business Logic Rules

### Rule A: Strict Job Visibility
- In `JobsController.GetJobs()`:
  ```csharp
  query = query.Where(j => j.OwnerUserId == currentUserId || j.Assignments.Any(a => a.UserId == currentUserId));
  ```
- Unrelated events of other team members are strictly excluded.

### Rule B: Role-Based Financial Masking
- If `currentUserId == job.OwnerUserId || currentUserId == job.CreatedByUserId`:
  - Returns `ClientTotalRevenue`, worker payouts, and payment statuses.
- If `currentUserId != job.OwnerUserId && currentUserId != job.CreatedByUserId`:
  - `ClientTotalRevenue = null`.
  - In `Assignments`, other workers' `CastAmount` values are masked to `null`. Only `a.UserId == currentUserId` receives their own payout amount.

### Rule C: Blind Availability Engine (`GET /api/availability`)
- Filters candidates based on `ServicesJson`.
- Evaluates:
  1. Mon–Fri Office Hours overlap (`OfficeBusyEnabled`).
  2. Ad-hoc day off (`UserBusyDates`).
  3. Job conflicts (`isTimeOverlap`).
- Returns strictly `{ userId, name, isBusy, busyLabel }`. No locations or event counts are ever exposed.

### Rule D: Strict Edit & Delete Permissions
- In `JobsController.UpdateJob()` and `JobsController.DeleteJob()`:
  ```csharp
  bool canEdit = job.OwnerUserId == currentUserId || job.CreatedByUserId == currentUserId;
  if (!canEdit) return Forbid(); // 403
  ```
- Assigned crew members have **Read-Only** access in the UI and cannot modify any job properties, payouts, or delete the job.

---

## 5. Local Development Commands

### Backend (.NET 8 Web API)
```bash
cd backend
dotnet restore
dotnet build
dotnet run --urls "http://localhost:5220"
```
- Swagger UI: `http://localhost:5220/swagger`
- Database: Uses lightweight SQLite (`data/eventmap.db`). Automatically created and seeded on startup.

### Frontend (Vue 3 + Vite)
```bash
cd frontend
npm install
npm run dev
```
- URL: `http://localhost:5173/`
- Default Test PIN: `1234` for all 8 seeded members (`Hưng`, `Hải`, `Minh`, `Tuấn`, `Long`, `Khánh`, `Việt`, `Tùng`).

---

## 6. Guidelines for AI Agents Modifying This Project
- **Never expose financial figures on map pins**: Map markers must only display `[Icon] Time • Location (CrewNames)`.
- **Always preserve role-based DTO masking**: Never return `ClientTotalRevenue` to non-owners.
- **Maintain monorepo structure**: Keep backend in `/backend` and frontend in `/frontend`.
- **Keep map-first aesthetics**: Maintain full-screen map layout (`100vw` x `100vh`) with zero intrusive sidebars.

---

## 7. Production Deployment & DevOps Architecture (VPS + SQLite + Docker + Caddy)

### Strategy & Rationale
- Designed for low-cost, ultra-lightweight production (8–10 crew members, 1 vCPU, 1–2 GB RAM VPS ~$2–$3/month).
- **Database**: Uses SQLite (`Microsoft.EntityFrameworkCore.Sqlite`) storing all records into a single persistent file (`data/eventmap.db`). Zero RAM overhead compared to SQL Server's 2GB minimum.
- **Single-Host Distribution**: Vue 3 SPA is compiled (`npm run build`) and injected directly into ASP.NET Core `wwwroot/`. The .NET 8 application serves static files and handles SPA routing fallback (`app.MapFallbackToFile("index.html")`), eliminating CORS issues and removing the need for a separate frontend server.
- **Automated SSL/TLS**: Caddy acts as reverse proxy on ports 80/443, automatically acquiring and auto-renewing Let's Encrypt certificates when a domain is configured.

### Container & File Structure
- `Dockerfile`: Multi-stage build (Node 20 Alpine -> .NET 8 SDK -> ASP.NET 8 Runtime).
- `docker-compose.yml`:
  - `web`: .NET 8 container running at port 8080.
  - `caddy`: Reverse proxy routing port 80/443 to `web:8080`.
  - Persistent volume: `./data:/app/data` (stores `eventmap.db` on host VPS).
  - SSL certs volume: `caddy_data` and `caddy_config`.
- `Caddyfile`: Configures routing. Default is `:80` for IP testing, or `domain.com { reverse_proxy web:8080 }` for automatic HTTPS.

### VPS Deployment Playbook (Next Steps When Resuming)
1. **VPS Specs**: Ubuntu 22.04 LTS, 1 vCPU, 1GB RAM (Vietnix, TinoHost, AZDIGI, Hetzner, etc.).
2. **Access VPS via SSH**:
   ```bash
   ssh root@<VPS_IP>
   ```
3. **Install Docker Engine**:
   ```bash
   curl -fsSL https://get.docker.com | sh
   ```
4. **Clone & Launch Application**:
   ```bash
   git clone https://github.com/mvhung/bookingdrone.git
   cd bookingdrone
   docker compose up -d
   ```
5. **Configure Custom Domain & HTTPS**:
   - In DNS manager, add `A` record pointing to `<VPS_IP>`.
   - In `Caddyfile`, replace `:80` block with your domain:
     ```caddyfile
     booking.yourdomain.com {
         reverse_proxy web:8080
     }
     ```
   - Reload Caddy: `docker compose restart caddy`.
6. **Operations & Maintenance**:
   - Pull updates: `git pull && docker compose up -d --build`
   - Backup DB: Copy `./data/eventmap.db`
   - Live logs: `docker compose logs -f web` or `docker compose logs -f caddy`

