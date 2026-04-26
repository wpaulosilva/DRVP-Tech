# FlowArtes — V3 Tests Framework & Patch Notes

This document covers all changes made to the codebase **after the V2 Advanced Controllers patch**. It is structured as a reference for understanding what was added, what was fixed, and why each decision was made.

---

## Overview

Patch v3 covers four areas:

1. **Bug fixes** — missing or incorrect behaviour found during manual endpoint testing
2. **Participant price resolution** — completing the AppSetting pricing integration
3. **Lifecycle automation** — replacing the manual finish endpoint with a background worker
4. **Test infrastructure** — building the unit and integration test suites and fixing concurrency issues

---

## 1. Bug Fixes

### 1.1 Studio Modality Endpoint Route Correction

**Problem:** The controller implemented `POST /api/studios/modality` and `DELETE /api/studios/modality` with a body DTO (`StudioModalityRequest`). The Postman collection and `EndpointsMapping.md` already documented the correct RESTful routes but the controller didn't match them.

**Fix:**
- `[HttpPost("modality")]` → `[HttpPost("{studioId}/modalities/{modalityId}")]`
- `[HttpDelete("modality")]` → `[HttpDelete("{studioId}/modalities/{modalityId}")]`
- Method signatures changed from `([FromBody] StudioModalityRequest)` to `(int studioId, int modalityId)` (route parameters, no body)
- `StudioModalityRequest` DTO removed — no longer used
- Controller comments updated to reflect the actual routes
- Postman entries updated: bodies removed, descriptions corrected

**Files changed:** `StudioController.cs`, `StudioDTO.cs`, `DanceSchoolApp_v2_postman_collection.json`

---

### 1.2 Coach Row Not Created on User Creation

**Problem:** `UserService.CreateUserAsync` called `_coachService.CreateCoachAsync(user.UserId)` without `await` (fire-and-forget), and also evaluated `role.RoleId` without a null guard — meaning if `FirstRole` was not provided, the line would throw `NullReferenceException`.

**Fix:**
```csharp
// Before
if (role.RoleId == Roles.Coach)
    _coachService.CreateCoachAsync(user.UserId);

// After
if (role is not null && role.RoleId == Roles.Coach)
    await _coachService.CreateCoachAsync(user.UserId);
```

**Files changed:** `UserService.cs`

---

### 1.3 Studio Create/Update Missing modalityIds[]

**Problem:** `StudioCreateRequest` and `StudioUpdateRequest` DTOs had no `ModalityIds` field. The mockup (page 14 — "Adicionar Estúdio") clearly shows "Modalidades compatíveis" as a checkbox list. The endpoint mapping already documented `modalityIds[]` in the body but the service ignored it.

**Fix:**
- Added `public List<int> ModalityIds { get; set; } = new();` to both DTOs
- `StudioService.CreateStudioAsync`: after creating the `Studio` object, queries active modalities matching the IDs and adds them to `studio.IdModalities` before `SaveChangesAsync`
- `StudioService.UpdateStudioAsync`: changed `FindAsync` to `FirstOrDefaultAsync` with `.Include(s => s.IdModalities)`, then clears and reassigns modalities from the new list
- Postman `POST Studio` and `PUT Studio` bodies updated to include `"modalityIds": [{{modality_id}}]`

**Design note:** The bulk `modalityIds[]` in create/update is the form-submission path. The fine-grained `POST/DELETE /{studioId}/modalities/{modalityId}` endpoints remain for individual toggles.

**Files changed:** `StudioDTO.cs`, `StudioService.cs`, `DanceSchoolApp_v2_postman_collection.json`

---

### 1.4 Modality Coach Assignment Endpoints Added to Postman

`POST /api/modalities/{id}/coaches/{coachId}` and `DELETE /api/modalities/{id}/coaches/{coachId}` were already implemented in the controller and service but missing from the Postman collection.

**Fix:** Added both requests to the `School > Modality` folder in the collection.

---

### 1.5 PUT User Roles Added to Postman

`PUT /api/users/{id}/roles` existed in the controller but was missing from the Postman `People > User` folder. Added with body `{ "roleIds": [2] }` and a description of all role IDs.

---

### 1.6 Integration Test 403 Anomaly (POST /api/coachclasses from parent)

**Problem:** `CoachClassLifecycleTests.FullClassLifecycle_RequestedToValidated_AllTransitionsSucceed` was getting 403 on the parent's POST request, even though the endpoint has `[Authorize(Roles = "staff,parent")]`.

**Root cause:** The test was making three sequential `LoginAndGetCookie` calls — for parent, staff, and coach — before any requests were sent. Each login overwrites the shared `HttpClient` cookie jar, so the last login (coach) was the active session when the parent's POST was made.

**Fix:** Login immediately before each request that requires a specific role, not at the top of the test:
```csharp
var parentJwt = await LoginAndGetCookie("parent_lc");
// make parent request immediately
var staffJwt = await LoginAndGetCookie("staff_lc");
// make staff request immediately
```

**Files changed:** `CoachClassLifecycleTests.cs`

---

## 2. Participant Price Resolution

### Background

`ParticipantJoinRequest.ClassPrice` was nullable with a TODO comment indicating it should resolve from AppSettings once that was built. AppSettings pricing was already fully implemented in `AppSettingService` and used by `BillingService`.

### Implementation

`ParticipantService.JoinClassAsync` now resolves `ClassPrice` using this priority chain before creating the `Participant` row:

| Priority | Condition | Value |
|----------|-----------|-------|
| 1 | `request.ClassPrice.HasValue` | Use the provided value directly (staff custom override) |
| 2 | Class `StartDatetime` is Saturday or Sunday | `AppSetting["class_price_weekend"]` (default 43.20€/h) |
| 3 | All other days | `AppSetting["class_price_weekday"]` (default 36.00€/h) |

This is consistent with how `BillingService.GetStudentBillingAsync` uses the same settings for computing monthly billing totals. The stored `ClassPrice` on the `Participant` row is the source of truth for billing — it is snapshotted at enrollment time so future rate changes do not retroactively alter past billing.

**Files changed:** `ParticipantService.cs`, `ParticipantDTO.cs` (comment updated)

---

## 3. Class Lifecycle Automation

### 3.1 Design Decision — BackgroundService vs SQL Server Agent

A SQL Server Agent job would require DBA setup, a running SQL Agent service, and cannot be version-controlled alongside the application code. For this project's context (academic, single-deploy-unit), an ASP.NET Core `BackgroundService` is the professional standard and adds no operational dependencies.

The 5-minute polling interval means a worst-case 5-minute delay between a class's scheduled end time and it being marked Finished — acceptable for a validation window measured in hours.

### 3.2 ClassLifecycleWorker

**File:** `DanceSchoolApp.Server/Services/ClassLifecycleWorker.cs`

A `BackgroundService` registered manually in `Program.cs`:
```csharp
builder.Services.AddHostedService<ClassLifecycleWorker>();
```

It is **not** auto-registered by the reflection loop because `BackgroundService` is a singleton and the loop registers everything as scoped. The worker uses `IServiceScopeFactory` to create a fresh async scope per cycle, giving it access to scoped services like `AppDbContext` and `NotificationService`.

**Cycle behaviour:**
1. Runs immediately at startup, then every 5 minutes via `PeriodicTimer`
2. Errors within a cycle are logged but do not stop the worker

#### Rule 1 — Auto-Finish (Approved → Finished)
```
Query: Status == Approved AND EndDatetime <= UtcNow (excluding Cancelled by status filter)
Action: Status = Finished, FinishedAt = UtcNow
Notifications: coach + all distinct parent users
```

#### Rule 2 — Auto-Advance Expired Validation Window (Finished → Pending)
```
windowHours = AppSetting["validation_window_hours"] (default 48)
Query: Status == Finished AND FinishedAt + windowHours <= UtcNow
Action: Status = Pending
Notifications: all active staff users (Warning — "Validation Window Expired")
```

### 3.3 Removal of Manual Finish Endpoint

`PATCH /api/coachclasses/{id}/finish` has been removed from `CoachClassController`. The `FinishAsync` method is kept in `CoachClassService` (called by the worker), but is no longer exposed via HTTP. Integration tests that previously called this endpoint were updated to seed the Finished state directly via `_factory.SeedDatabase`.

### 3.4 Late Validation Notifications

When coach or parent validates a class that is already in `Pending` status (meaning the window expired before they responded), staff receive an additional notification:
- Coach late: "Late Coach Validation Received"
- Parent late: handled in `ParticipantService.TryAdvanceClassToStaffReviewAsync` — if the class is already `Pending`, skip the status change but still notify staff

### 3.5 DB Schema Change

`finished_at [datetime2](7) NULL` was added to `[dbo].[Coach_Class]`. This column records the exact moment the worker (or the legacy `FinishAsync` call) marked the class as Finished, and is what Rule 2 uses to compute whether the validation window has expired.

**Migration approach:** Since the project uses DB-first scaffolding (not code-first migrations), a plain SQL script was provided:

```sql
-- add_finished_at.sql (idempotent — safe to re-run)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Coach_Class]') AND name = N'finished_at'
)
BEGIN
    ALTER TABLE [dbo].[Coach_Class] ADD [finished_at] [datetime2](7) NULL;
END
```

Integration tests are unaffected — SQLite rebuilds the schema from the C# model via `EnsureCreated()` and picks up `FinishedAt` automatically.

**Files changed:** `ClassLifecycleWorker.cs` (new), `CoachClass.cs` (model), `AppDbContext.cs` (mapping), `Program.cs` (registration), `CoachClassService.cs` (FinishAsync stamps FinishedAt), `CoachClassController.cs` (Finish endpoint removed), `CoachClassLifecycleTests.cs` (Step 4 updated)

---

## 4. Test Infrastructure

### 4.1 Unit Tests (Layer 1)

**27 tests, all passing.** Located in `DanceSchoolApp.Tests/Unit/`.

| Test class | Coverage |
|-----------|----------|
| `BillingServiceTests` | Weekday vs weekend rate, coach rate, multi-student aggregation, month filtering |
| `BookingServiceTests` | Slot subtraction algorithm, global/studio/coach blocked periods, cancelled booking exclusion, availability validity dates |
| `CoachClassStatusTests` | All status transition guards — valid transitions and every invalid combination that must throw `InvalidOperationException` |
| `ParticipantOwnershipTests` | Student belongs to calling parent (happy path), student belongs to different parent (throws `UnauthorizedAccessException`) |

**Unit test DB pattern:** Each test method calls `DbContextFactory.Create()` which opens a new `SqliteConnection("DataSource=:memory:")` and creates a fresh schema via `EnsureCreated()`. Tests are fully isolated with no shared state.

### 4.2 Integration Tests (Layer 2)

**14 tests, all passing.** Located in `DanceSchoolApp.Tests/Integration/`.

| Test class | Coverage |
|-----------|----------|
| `AuthIntegrationTests` | Valid login → 200 + cookie, wrong password → 401, inactive user → 401, `GET /api/auth/me` with/without cookie, logout clears cookie |
| `AuthorizationBoundaryTests` | Parent calling staff endpoint → 403, parent calling staff-approve → 403, staff calling coach-accept → 403, parent accessing another parent's student → 403, unauthenticated → 401 |
| `CoachClassLifecycleTests` | Full 7-step lifecycle: create (parent) → staff-approve → coach-accept → finish (DB seed) → coach-validate → parent-validate → staff-validate; verifies status at each step |
| `BillingIntegrationTests` | Validated class appears in billing with correct amount, Finished (non-Validated) class excluded, empty month returns 200 with empty items |

### 4.3 Concurrency Fix — SQLite Connection Registry Race

**Problem:** xUnit runs test classes in parallel by default. When `AuthIntegrationTests`, `AuthorizationBoundaryTests`, `CoachClassLifecycleTests`, and `BillingIntegrationTests` all initialized their `CustomWebApplicationFactory` concurrently, their `SqliteConnection` instances all called `InitializeDbConnection` simultaneously. This method writes to a process-wide non-thread-safe `Dictionary` (`SqliteConnection.CreateAggregateCore`) causing intermittent `InvalidOperationException: Operations that change non-concurrent collections must have exclusive access`.

**Fix — two combined changes:**

**Change 1:** Disable assembly-level parallelism in `DanceSchoolApp.Tests/AssemblyInfo.cs`:
```csharp
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

**Change 2:** Rewrite `CustomWebApplicationFactory` to use unique named in-memory SQLite databases instead of a single `SqliteConnection` object:

```csharp
// Before — single shared connection, all scopes hold a reference to keep it alive
private readonly SqliteConnection _connection = new("DataSource=:memory:");

// After — unique named database per factory instance, no explicit connection object needed
private readonly string _dbName = $"test_{Guid.NewGuid():N}";
private string ConnectionString => $"DataSource={_dbName};Mode=Memory;Cache=Shared";
```

Named shared-cache mode (SQLite URI `Mode=Memory;Cache=Shared`) keeps the in-memory database alive as long as any connection using the same URI is open — EF Core manages this automatically. No `SqliteConnection` field to hold, no `Dispose` override needed.

**Files changed:** `AssemblyInfo.cs` (new), `CustomWebApplicationFactory.cs` (rewritten)

### 4.4 Test Execution Commands

```bash
# Run all tests (sequential due to assembly attribute)
dotnet test

# Run only unit tests (fast — no HTTP, no factory startup)
dotnet test --filter "Category=Unit"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Run a specific test by name
dotnet test --filter "FullClassLifecycle_RequestedToValidated_AllTransitionsSucceed"
```

---

## 5. Documentation Updates

### EndpointsMapping.md
- Studio "Editar" row updated to include `body: { name, capacity, address?, modalityIds[] }`
- Two new rows added to the `/staff/studios` table for individual modality link/unlink

### DanceSchoolApp_v2_postman_collection.json
| Change | Detail |
|--------|--------|
| `POST Studio` body | Added `"modalityIds": [{{modality_id}}]` |
| `PATCH Studio` renamed | → `PUT Studio`, method changed to PUT, body includes `"modalityIds"` |
| `POST/DELETE Studio Modality` | Bodies removed — route params only |
| `PUT User Roles (replace)` | Added to People > User folder |
| `POST Assign Coach to Modality` | Added to School > Modality folder |
| `DELETE Remove Coach from Modality` | Added to School > Modality folder |
| `GET Roles` | Added to People > User folder for role ID reference |

---

## 6. AppSetting Keys Reference

| Key | Default | Used by |
|-----|---------|---------|
| `class_price_weekday` | `36.00` | `ParticipantService` (price resolution), `BillingService` |
| `class_price_weekend` | `43.20` | `ParticipantService` (price resolution), `BillingService` |
| `coach_rate_per_hour` | `35.00` | `BillingService` (coach billing) |
| `validation_window_hours` | `48` | `ClassLifecycleWorker` (Rule 2 cutoff) |
| `max_participants` | `8` | Reserved — not yet enforced in service layer |

All keys are auto-seeded by `AppSettingService.GetOrCreateAsync` on first access if absent from the DB. Changing a value via `PATCH /api/staff/appsettings/{key}` takes effect immediately on the next relevant request or worker cycle.