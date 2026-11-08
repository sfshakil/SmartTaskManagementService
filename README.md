# Smart Task Management System

REST API for managing projects and tasks, with role-based access, JWT authentication with refresh tokens, a dashboard, and an AI-assisted task description improver.

This repository currently contains the backend (ASP.NET Core 10). The Angular client is added separately.

## Technology stack

| Area | Choice |
|---|---|
| Runtime | .NET 10, ASP.NET Core Web API |
| Database | SQL Server, code first |
| Writes | Entity Framework Core 10 (change tracking, migrations) |
| Reads / lists | Dapper (hand-written, parameterised SQL) |
| Auth | JWT bearer + rotating refresh tokens, PBKDF2 password hashing |
| Validation | FluentValidation |
| Logging | Serilog (console + rolling file) |
| Docs | Swagger / OpenAPI (Swashbuckle) |
| AI | Any OpenAI-compatible chat API: GitHub Models or Google Gemini (tested with `gemini-3.5-flash-lite`) |
| Resilience | `Microsoft.Extensions.Http.Resilience` for the AI HTTP client |

## Architecture

Clean Architecture with dependencies pointing inwards.

```
src/
  SmartTaskManagement.Domain          Entities and enums. No dependencies.
  SmartTaskManagement.Application     Use cases (services), DTOs, validators, abstractions.
  SmartTaskManagement.Infrastructure  EF Core, Dapper queries, JWT, password hashing, AI client.
  SmartTaskManagement.Api             Controllers, middleware, filters, composition root.
```

Key decisions:

- **EF Core for writes, Dapper for reads.** Services load tracked entities through repositories (`IProjectRepository`, `ITaskRepository`, ...), mutate them and commit through `IUnitOfWork`. Lists, search, sorting, pagination and dashboard figures go through query objects (`IProjectQueries`, `ITaskQueries`, `IDashboardQueries`, `IUserQueries`) implemented with Dapper.
- **Dynamic SQL is whitelisted.** Sort columns come from a fixed dictionary, never from user input; every value is a parameter; `LIKE` input is escaped.
- **Authorization lives in the application layer**, not only in attributes, because rules depend on ownership and assignment (see below).
- **AI provider behind an interface** (`IAiTextImprover`). The client speaks the OpenAI-compatible `chat/completions` format, so GitHub Models, Gemini and similar providers work by changing configuration only. A provider with a different wire format needs one new class in Infrastructure.
- **One response envelope** for every success and error (`ApiResponse<T>`).

## Roles and permissions

| Action | Admin | Project Manager | Team Member |
|---|---|---|---|
| Register / login | yes | yes | yes |
| View projects | all | own projects | projects containing a task assigned to them |
| Create project | yes | yes | no |
| Update / delete project | any | own projects | no |
| Create / update / delete / assign task | any | tasks in own projects | no |
| Update task status | any | tasks in own projects | tasks assigned to them |
| View tasks | all | tasks in own projects | tasks assigned to them |
| Dashboard | global | own projects | own tasks |
| List users | yes | yes | no |
| Change a user's role | yes | no | no |
| Improve description with AI | yes | yes | yes |

Self-registration always creates a Team Member. The first administrator is seeded from configuration (below); admins promote others via `PUT /api/users/{id}/role`.

## Setup

### Prerequisites

- .NET 10 SDK
- SQL Server (LocalDB, Docker or a full instance)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

### 1. Configure secrets

Nothing sensitive is committed. Use user secrets for local development:

set "Jwt:SecretKey" "cuSt0meR@360-87732d4e-de6d-44de-b182-11423c5894e4"
set "Seed:AdminEmail" "admin@smarttaskmanagement.com"
set "Seed:AdminPassword" "Admin@123"

# Optional: only needed for the AI endpoint (see "AI provider" below)
set "Ai:Token" "AQ.Ab8RN6KXxm_b4m9cHsU3XLvsE6DdBtpu1PO-wOgvAUMzoZRekg"

# Optional: override the LocalDB connection string used in Development
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\MSSQLLocalDB;Database=SmartTaskManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"


In other environments use environment variables, for example `Jwt__SecretKey`, `ConnectionStrings__DefaultConnection`, `Ai__Token`.

The API refuses to start if the JWT key is missing or shorter than 32 characters.

### AI provider

The defaults in `appsettings.json` target GitHub Models. Choose one provider and set its values as user secrets.

**Google Gemini** (free API key from Google AI Studio):

set "Ai:BaseUrl" "https://generativelanguage.googleapis.com/v1beta/openai/"
set "Ai:Model" "gemini-3.5-flash-lite"
set "Ai:Token" "<gemini api key>"
set "Ai:MaxTokens" "1000"

Model availability changes over time. If the provider answers 404 with a message that a model is no longer available, switch `Ai:Model` to the model it recommends. Only the task title and description are sent to the provider, so do not use real company data with a free tier.

If the AI endpoint returns 503, the cause is written to the log (provider status code, or a short excerpt of an unexpected response body).

### 2. Create the database

Generate the initial migration once, then run the API (it applies pending migrations on startup)
If no migrations exist yet, the app creates the schema from the model so it can still start. Drop that database before adding migrations later.

### 3. Run

- Swagger UI: `/swagger` on the HTTPS address shown at startup (default `https://localhost:7249/swagger`; the port comes from `launchSettings.json`). Available in Development, or when `Swagger:Enabled` is `true`.
- Liveness: `GET /health/live`
- Readiness (checks SQL Server): `GET /health/ready`

## Configuration reference

| Key | Purpose | Default |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection | LocalDB (Development only) |
| `Jwt:Issuer`, `Jwt:Audience` | Token validation | set in `appsettings.json` |
| `Jwt:SecretKey` | HMAC signing key (32+ chars) | none, required |
| `Jwt:AccessTokenMinutes` | Access token lifetime | 15 |
| `Jwt:RefreshTokenDays` | Refresh token lifetime | 7 |
| `Ai:BaseUrl`, `Ai:Model`, `Ai:Token` | OpenAI-compatible endpoint, model, API key | GitHub Models: `models.github.ai`, `openai/gpt-4o-mini`, none |
| `Ai:Temperature`, `Ai:MaxTokens`, `Ai:TimeoutSeconds` | Generation limits | 0.3, 500, 30 |
| `Cors:AllowedOrigins` | Allowed frontend origins | `http://localhost:4200` |
| `RateLimiting:GlobalPerMinute / AuthPerMinute / AiPerMinute` | Requests per minute | 120 / 10 / 10 |
| `Seed:AdminEmail`, `Seed:AdminPassword` | First admin account | none |

## API overview

All responses use this envelope:

```json
{ "success": true, "message": "optional", "data": { }, "errors": null, "traceId": null }
```

Errors set `success: false`, a `message`, optional per-field `errors`, and the request `traceId`.

| Method | Route | Description |
|---|---|---|
| POST | `/api/auth/register` | Create an account (Team Member) |
| POST | `/api/auth/login` | Returns access and refresh tokens |
| POST | `/api/auth/refresh` | Rotates the refresh token |
| POST | `/api/auth/logout` | Revokes the refresh token |
| GET | `/api/projects` | Search, sort, paginate |
| GET | `/api/projects/{id}` | Project details |
| POST | `/api/projects` | Create |
| PUT | `/api/projects/{id}` | Update |
| DELETE | `/api/projects/{id}` | Delete (cascades to tasks) |
| GET | `/api/tasks` | Search, filter, sort, paginate |
| GET | `/api/tasks/{id}` | Task details |
| POST | `/api/tasks` | Create |
| PUT | `/api/tasks/{id}` | Update title, description, priority, due date |
| PATCH | `/api/tasks/{id}/assign` | Assign or unassign |
| PATCH | `/api/tasks/{id}/status` | Change status |
| DELETE | `/api/tasks/{id}` | Delete |
| POST | `/api/tasks/improve-description` | AI suggestion (nothing is saved) |
| GET | `/api/dashboard` | Totals, by status, by priority, completed vs pending, upcoming due |
| GET | `/api/users` | List active users (for assignment) |
| PUT | `/api/users/{id}/role` | Change role (Admin) |
| GET | `/health/live`, `/health/ready` | Health checks |

Query parameters for list endpoints: `search`, `sortBy`, `descending`, `pageNumber`, `pageSize` (max 100).

- Projects `sortBy`: `name`, `createdAt`, `owner`, `totalTasks`
- Tasks `sortBy`: `title`, `status`, `priority`, `dueDate`, `createdAt`, `project`
- Tasks filters: `projectId`, `status`, `priority`, `assignedToId`, `dueFromUtc`, `dueToUtc`

Enums are serialised as strings: status `ToDo | InProgress | Completed | Cancelled`, priority `Low | Medium | High | Critical`, role `Admin | ProjectManager | TeamMember`.

A Postman collection is in [`docs/SmartTaskManagement.postman_collection.json`](docs/SmartTaskManagement.postman_collection.json). The Swagger document can also be exported from `/swagger/v1/swagger.json`.

## Security notes

- Passwords: PBKDF2-SHA512, 210,000 iterations, per-user salt, constant-time comparison.
- Access tokens are short lived. Refresh tokens are random, stored only as SHA-256 hashes, single use (rotated on refresh), revoked on logout. Presenting an already-rotated token revokes the user's active tokens.
- Login errors do not reveal whether an email exists.
- HTTPS redirection and HSTS outside Development; security headers on every response.
- CORS restricted to configured origins and the methods and headers the client needs.
- Rate limiting: global per IP, a tight limit on auth endpoints, and a per-user limit on the AI endpoint.
- Secrets come from user secrets or environment variables only.
- Unhandled exceptions return a generic message with a trace id; details go to the log only.

## Folder structure

```
.
├── README.md
├── PROMPTS.md
├── docs/
│   └── SmartTaskManagement.postman_collection.json
└── src/
    ├── SmartTaskManagement.Domain/
    │   ├── Common/              BaseEntity
    │   ├── Entities/            User, RefreshToken, Project, TaskItem
    │   └── Enums/               UserRole, TaskItemStatus, TaskPriority
    ├── SmartTaskManagement.Application/
    │   ├── Abstractions/        Repositories, query interfaces, security and AI contracts
    │   ├── Common/              ApiResponse, PagedQuery/PagedResult, exceptions, AccessScope
    │   ├── DTOs/
    │   ├── Services/            Auth, Project, Task, Dashboard, User, DescriptionImprovement
    │   └── Validators/          FluentValidation rules
    ├── SmartTaskManagement.Infrastructure/
    │   ├── Ai/                  OpenAI-compatible chat client and prompt templates
    │   ├── Options/             Strongly typed, validated configuration
    │   ├── Persistence/
    │   │   ├── Configurations/  EF Core entity mappings
    │   │   ├── Dapper/          Read-side queries
    │   │   ├── Repositories/    Write-side repositories
    │   │   ├── AppDbContext.cs
    │   │   └── DatabaseInitializer.cs
    │   └── Security/            Password hasher, token service, current user
    └── SmartTaskManagement.Api/
        ├── Controllers/
        ├── Extensions/          Auth, CORS, rate limiting, Swagger setup
        ├── Filters/             Validation filter
        ├── Middleware/          Global exception handling
        └── Program.cs
```

## Patterns used

Repository and Unit of Work (writes), CQRS-style query objects (reads), Strategy (swappable AI provider behind `IAiTextImprover`), Options pattern, Dependency Injection throughout, and a decorator-style pipeline of middleware and filters for cross-cutting concerns.