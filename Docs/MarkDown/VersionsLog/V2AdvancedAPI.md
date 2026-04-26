# FlowArtes — V2 Advanced Controllers Design

This document defines the **new controllers** to build for the 2nd version, their endpoints, which existing primitives they call, and exactly where each endpoint is consumed in the frontend (page + UI element).

The naming convention follows the role-scoped URL pattern the mockups already use:  
`/api/staff/...`, `/api/coach/...`, `/api/ee/...`, `/api/admin/...`  
These are **view/aggregation controllers** — they never own a table, they compose data from the domain services already built.

---

## Design Principles

- **These controllers contain no business logic.** They call existing services (CoachClassService, ParticipantService, etc.) and assemble the response. All mutations still go through the primitive controllers (`/api/coachclasses`, `/api/participants`, etc.).
- **One controller per role-area.** Each controller maps to a sidebar section visible to that role.
- **Filtering over pagination where the dataset is calendar-bound.** Calendars always request a week/month range (`?from=&to=`). Lists use `?page=&pageSize=&` + domain filters.
- **Billing is read-only computed data.** No billing table exists — it's derived from `Validated` CoachClass rows + Participant.ClassPrice + AppSettings rates.

---

## Controller 1 — `StaffDashboardController`
**Route prefix:** `/api/staff`  
**Roles:** `staff`, `admin`

This is the most complex controller — it aggregates data for the entire Direção panel.

---

### `GET /api/staff/dashboard`
**Page:** `/staff` — *Painel de Direção* (landing page after login for staff)  
**UI:** The KPI card row (Encarregados, Professores, Estudantes Ativos, Eventos Ativos, Aulas Marcadas, Aulas Realizadas, Pendentes Validação) + Próximos Eventos strip  

**Returns:**
```json
{
  "totalParents": 28,
  "totalCoaches": 8,
  "totalActiveStudents": 64,
  "totalActiveEvents": 4,
  "classesScheduledThisMonth": 156,
  "classesCompletedThisMonth": 142,
  "classesPendingValidation": 8,
  "upcomingEvents": [
    { "eventId": 1, "title": "Espetáculo Anual", "startDatetime": "...", "participantCount": 45 }
  ]
}
```
**Calls:** counts from Users, Coaches, Students, Events, CoachClasses (grouped by status), Events ordered by startDatetime ASC limit 3.  
**No pagination** — fixed shape.

---

### `GET /api/staff/agenda`
**Page:** `/staff/agenda` — *Agenda Global* calendar  
**UI:** Week calendar grid + day detail list below it. Nav arrows trigger new requests.  

**Query params:** `?from=YYYY-MM-DD&to=YYYY-MM-DD&studioId=&status=`  
**Filter:** optional studio filter (dropdown "Filtrar por estúdio"), optional status filter  

**Returns:** list of CoachClass slim cards for the date window:
```json
[{
  "classId": 1,
  "startDatetime": "...",
  "endDatetime": "...",
  "modalityName": "Ballet Clássico",
  "studioName": "Estúdio 1",
  "coachName": "Prof. João Santos",
  "studentNames": ["Sofia Silva"],
  "status": "Approved",
  "currentParticipants": 1,
  "maxParticipants": 4
}]
```
**Calls:** CoachClasses filtered by date window + optional studio/status.  
**No pagination** — date-bounded, ~200 max rows per week across all studios.

---

### `GET /api/staff/validate-classes`
**Page:** `/staff/validate-classes` — *Validações de Aulas*  
**UI:** Three tabs: **Requisitadas** / **Finalizadas** / **Pendentes**. Each tab is a filtered list.  

**Query params:** `?tab=requested|finished|pending&page=1&pageSize=15`  
- `requested` → status = Requested (0)  
- `finished` → status = Finished (4), shows per-participant validation counts  
- `pending` → status = Pending (6), shows coach + parent validation summary  

**Returns** (unified shape, fields populated per tab):
```json
{
  "items": [{
    "classId": 1,
    "modalityName": "Ballet Clássico",
    "startDatetime": "...",
    "studentName": "Maria Silva",
    "parentName": "Ana Costa",
    "coachName": "Prof. Ana Costa",
    "requestedBy": "Carlos Almeida (EE)",
    "durationMinutes": 60,
    "coachValidationStatus": "Pending",
    "participantSummary": {
      "total": 1,
      "confirmed": 0,
      "disputed": 0,
      "pending": 1
    }
  }],
  "totalCount": 2,
  "page": 1,
  "pageSize": 15
}
```
**Action buttons on each card call the existing primitive endpoints:**
- "Aceitar Aula" → `PATCH /api/coachclasses/{id}/staff-approve`
- "Recusar Aula" → `PATCH /api/coachclasses/{id}/reject`
- "Finalizar" → `PATCH /api/coachclasses/{id}/finish`
- "Validar" (Pending tab) → `PATCH /api/coachclasses/{id}/staff-validate`

---

### `GET /api/staff/validate-students`
**Page:** `/staff/validate-students` — *Validar Novos Estudantes*  
**UI:** Three tab filters: Pendentes / Aprovados / Todos. Each student card shows personal info + parent contact + action buttons.  

**Query params:** `?status=pending|approved|all&page=1&pageSize=10`  

**Returns:**
```json
{
  "items": [{
    "studentId": 1,
    "firstName": "Sofia", "lastName": "Martins",
    "birthDate": "2016-03-15",
    "phone": "+351 912 345 678",
    "address": "Rua das Flores, 123, Lisboa",
    "nif": "••••••••",
    "acceptanceStatus": 0,
    "submittedAt": "2026-03-18",
    "parentName": "Maria Silva",
    "parentEmail": "maria@email.com"
  }],
  "totalCount": 3, "page": 1, "pageSize": 10
}
```
**Action buttons on each card call:**
- "Aceitar Estudante" → `PATCH /api/students/{id}/accept`
- "Recusar Estudante" → `PATCH /api/students/{id}/reject` (body: `{ reason }` required)

---

### `GET /api/staff/billing/students`
**Page:** `/staff/billing` — *Faturação*, tab **Tabela de Alunos**  
**UI:** Summary KPIs (Total Alunos, Receita Total, Horas Totais, Pendentes) + filterable table + "Exportar Excel" button  

**Query params:** `?month=YYYY-MM&search=&status=all|paid|pending|late&page=1&pageSize=25`  

**Business logic (no billing table — computed):**  
For each student, sum `Participant.ClassPrice` across `Validated` classes in the requested month.  
Price per class = `(durationMinutes / 60) × rate`, where:  
- `rate` = `AppSetting["class_price_weekday"]` (default 36€/h) for Mon–Fri  
- `rate` = `AppSetting["class_price_weekend"]` (default 43.2€/h) for Sat–Sun  
The price is already stored on `Participant.ClassPrice` at join time if populated, otherwise recomputed.

**Returns:**
```json
{
  "summary": {
    "totalStudents": 5,
    "totalRevenue": 1370.00,
    "totalHours": 50,
    "pendingCount": 2
  },
  "items": [{
    "studentId": 1,
    "studentName": "Ana Martins",
    "hoursCompleted": 12,
    "hoursContracted": 16,
    "totalAmount": 300.00,
    "paymentStatus": "Paid",
    "lastPaymentDate": "2026-01-01"
  }],
  "totalCount": 5, "page": 1, "pageSize": 25
}
```
**Note:** `paymentStatus` and `lastPaymentDate` are not in DB currently. These are deferred to a later sprint — return `null` for now and leave a TODO.  
**"Exportar Excel" button** → `GET /api/staff/billing/students/export?month=YYYY-MM` (separate endpoint, returns file stream — implement after core billing works).

---

### `GET /api/staff/billing/coaches`
**Page:** `/staff/billing` — *Faturação*, tab **Tabela de Professores**  
**UI:** Same structure as student billing but coach-facing.  

**Query params:** `?month=YYYY-MM&search=&status=all|paid|pending&page=1&pageSize=25`  

**Business logic:** Same rate logic, but amounts represent what the school *pays* the coach (cost), not what it receives. The school pays coaches per hour taught on `Validated` classes.

**Returns:**
```json
{
  "summary": {
    "totalCoaches": 5,
    "totalExpense": 7806.00,
    "totalHours": 212,
    "pendingCount": 3
  },
  "items": [{
    "coachId": 1,
    "coachName": "Prof. João Santos",
    "modalities": ["Ballet Clássico", "Contemporâneo"],
    "hoursTaught": 45,
    "ratePerHour": 35.00,
    "totalAmount": 1575.00,
    "paymentStatus": "Pending",
    "lastPaymentDate": "2026-02-28"
  }],
  "totalCount": 5, "page": 1, "pageSize": 25
}
```
**Note:** `ratePerHour` comes from `AppSetting["coach_rate_per_hour"]` (new key to add, default 35€/h). `paymentStatus` deferred — return null.

---

### `GET /api/staff/appsettings`
**Page:** `/staff` sidebar → *Configurações* (not yet in mockup, but US19 requires it)  
**UI:** Simple key-value form for pricing configuration  

**Returns:** all `AppSetting` rows  
**Calls:** existing `AppSettingService.GetAllAsync()`

`PATCH /api/staff/appsettings/{key}` — update a setting value  
Body: `{ "value": "43.20" }`  
**Calls:** existing `AppSettingService.UpdateAsync(key, value)`

---

## Controller 2 — `CoachPortalController`
**Route prefix:** `/api/coach`  
**Roles:** `coach` only (enforced; coaches see only data tied to their own coachId)

---

### `GET /api/coach/dashboard`
**Page:** `/coach` — *Bem-vindo, Prof. X!* dashboard  
**UI:** Three KPI cards (Aulas Dadas Este Mês, Aulas que Faltam, Validações Pendentes) + Próximas Aulas strip  

**Returns:**
```json
{
  "coachName": "João Santos",
  "classesTaughtThisMonth": 24,
  "classesUpcoming": 6,
  "validationsPending": 2,
  "upcomingClasses": [{
    "classId": 1,
    "startDatetime": "...",
    "modalityName": "Ballet Clássico",
    "studioName": "Estúdio A",
    "studentNames": ["Ana Rodrigues"],
    "status": "Approved"
  }]
}
```
**Calls:** CoachClasses filtered by coachId (from JWT), current month for counts, next 5 upcoming for the strip.

---

### `GET /api/coach/agenda`
**Page:** `/coach/agenda` — *Agenda* calendar  
**UI:** Week calendar + day detail below. Arrows → new request. Different status classes shown in different colors (suggestion from mockup notes).  

**Query params:** `?from=YYYY-MM-DD&to=YYYY-MM-DD`  

**Returns:** same slim card shape as `/api/staff/agenda` but filtered to calling coach only. Includes `status` so frontend can color-code.  
**Calls:** CoachClasses where `idCoach = callingCoachId` within date range.

---

### `GET /api/coach/validate`
**Page:** `/coach/validate` — *Validar Aulas*, two sub-tabs  
**UI tab "Pedidos de Aula":** Classes in `StaffApproved` status awaiting coach accept/reject  
**UI tab "Validações":** Classes in `Finished` status awaiting coach-validate  

**Query params:** `?tab=requests|validations&page=1&pageSize=10`  

**Returns (requests tab):**
```json
{
  "items": [{
    "classId": 1,
    "requestedAt": "...",
    "studentName": "Teresa Almeida",
    "requestedBy": "Carlos Almeida (EE)",
    "preferredDate": "2026-03-20T10:00",
    "durationMinutes": 60,
    "studioName": "Estúdio 1",
    "notes": "Preferência por aulas de manhã"
  }],
  "totalCount": 2, "page": 1, "pageSize": 10
}
```
**Returns (validations tab):**
```json
{
  "items": [{
    "classId": 3,
    "studentName": "Sofia Silva",
    "startDatetime": "2026-03-01T10:00",
    "studioName": "Estúdio 1",
    "expiresAt": "2026-03-03T12:00",
    "coachValidationStatus": "Pending"
  }],
  "totalCount": 3, "page": 1, "pageSize": 10
}
```
**`expiresAt`** = `startDatetime + 48h` (computed, not stored).  

**Action buttons:**
- "Aceitar Aula" → `PATCH /api/coachclasses/{id}/coach-accept`
- "Recusar Aula" → `PATCH /api/coachclasses/{id}/coach-reject` (body: `{ reason }`)
- "Realizada" / "Não Realizada" → `PATCH /api/coachclasses/{id}/coach-validate` (body: `{ didTeach: true/false }`)

---

## Controller 3 — `ParentPortalController` (EE)
**Route prefix:** `/api/ee`  
**Roles:** `parent` only

---

### `GET /api/ee/dashboard`
**Page:** `/ee` — *Dashboard* (first impression page)  
**UI:** Two KPI cards (Aulas Marcadas, Aulas por Validar) + Próximas Aulas strip  

**Returns:**
```json
{
  "parentName": "Maria Silva",
  "totalScheduledClasses": 8,
  "classesAwaitingValidation": 1,
  "upcomingClasses": [{
    "classId": 1,
    "startDatetime": "...",
    "modalityName": "Ballet Clássico",
    "coachName": "Prof. João Santos",
    "studioName": "Estúdio A",
    "status": "Approved",
    "studentName": "Maria Silva"
  }]
}
```
**Calls:** CoachClasses where `createdBy = callingUserId`, status = Approved, ordered by startDatetime ASC, limit 4.

---

### `GET /api/ee/classes/my`
**Page:** `/ee/classes` tab **Minhas Marcações** — week calendar view  
**UI:** Week calendar grid showing parent's own booked classes. Arrows → new request.  

**Query params:** `?from=YYYY-MM-DD&to=YYYY-MM-DD`  

**Returns:** same slim card shape, filtered to classes created by calling parent within date range. Includes status for color/badge display.

---

### `GET /api/ee/classes/available-slots`
**Page:** `/ee/classes` tab **Criar Aula** — booking discovery calendar  
**UI:** Two dropdowns (Modalidade, Professor) + week calendar showing available time slots. Parent clicks a day to see slots, then clicks "Pedir Aula" on a slot.  

**Query params:** `?from=YYYY-MM-DD&to=YYYY-MM-DD&modalityId=&coachId=`  
(both filters optional but at least one should be present for useful results)  

**Algorithm (read-only, no mutations):**
1. Load `CoachAvailability` slots valid for the requested week, filtered by coach/modality if provided.
2. Subtract: existing `CoachClass` bookings (non-rejected/non-cancelled) that overlap each slot.
3. Subtract: `BlockedPeriod` entries (global + coach-specific) that overlap each slot.
4. Return the remaining free windows grouped by day.

**Returns:**
```json
[{
  "date": "2026-03-11",
  "slots": [{
    "coachId": 1,
    "coachName": "Prof. João Santos",
    "startTime": "11:00",
    "endTime": "18:00",
    "modalityIds": [1, 3]
  }]
}]
```
**This is the most complex read query in the project.** It lives in a new `BookingService` (no DB writes, pure query logic).  
**"Pedir Aula" button on a slot** → opens the modal → user fills duration/students → POST to **existing** `POST /api/coachclasses`.

---

### `GET /api/ee/classes/open`
**Page:** `/ee/classes` tab **Aulas Existentes** — joinable classes list  
**UI:** Cards showing group/ensemble classes available for enrollment. Classes at max capacity must NOT appear (mockup noted this as wrong — enforce here).  

**Query params:** `?modalityId=&page=1&pageSize=10`  

**Returns:** subset of existing `GET /api/coachclasses/open` but only classes where `currentParticipants < maxParticipants`. Adds filter by modality.  
**"Inscrever" button** → `POST /api/participants` (body: `{ classId, studentId }`)

---

### `GET /api/ee/classes/validate`
**Page:** `/ee/classes` tab **Validar Aulas**  
**UI:** List of `Finished` classes where calling parent has a participant with `ValidationStatus = Pending`. Each shows deadline (48h from finish), student name, class detail. Two buttons per entry.  

**Query params:** `?page=1&pageSize=10`  

**Returns:**
```json
{
  "items": [{
    "participantId": 5,
    "classId": 3,
    "modalityName": "Ballet Clássico",
    "studentName": "Maria Silva",
    "startDatetime": "2026-03-15T14:00",
    "coachName": "Prof. Sofia Costa",
    "expiresAt": "2026-03-17T14:00",
    "validationStatus": "Pending"
  }],
  "totalCount": 2, "page": 1, "pageSize": 10
}
```
**Action buttons:**
- "Confirmar Realização" → `PATCH /api/participants/{participantId}/parent-validate` (body: `{ attended: true }`)
- "Não Realizada" → `PATCH /api/participants/{participantId}/parent-validate` (body: `{ attended: false }`)

---

### `GET /api/ee/students`
**Page:** `/ee/students` — *Meus Estudantes*  
**UI:** List of parent's students with Editar/Remover buttons. "Adicionar Estudante" button at top.  

**Returns:** students belonging to calling parent, including `acceptanceStatus` so the UI can show pending/accepted/rejected badge.  
**Calls:** existing `GET /api/students` filtered by `parentUserId` (already enforced server-side for parent role).

**"Adicionar Estudante"** → `POST /api/students`  
**"Editar"** → opens modal → `PUT /api/students/{id}` (needs to be added to existing StudentController — currently only PATCH accept/reject/deactivate exists)  
**"Remover"** → `PATCH /api/students/{id}/deactivate`

---

### `GET /api/ee/inventory/school`
**Page:** `/ee/inventory` tab **Escolar**  
**UI:** Grid of item cards (like a shop). Filter by Categoria, Tamanho, Estado (search box). Each item card leads to an item detail page (per mockup correction: not variant blobs).  

**Query params:** `?categoryId=&search=&page=1&pageSize=12`  
**Filter:** `fromSchool = true` items only, active only, with at least one active variant in stock.  

**Returns:**
```json
{
  "items": [{
    "itemId": 1,
    "name": "Collant Ballet Rosa",
    "categoryName": "Vestuário",
    "imageUrl": "...",
    "lowestPrice": 5.00,
    "variantCount": 3
  }],
  "totalCount": 10, "page": 1, "pageSize": 12
}
```
**"Pedir Empréstimo" / item card click** → `GET /api/items/{id}` (existing, shows full detail with variants) → then `POST /api/requisitions` for the selected variant.

---

### `GET /api/ee/inventory/community`
**Page:** `/ee/inventory` tab **Comunidade**  
**UI:** Grid of community-listed items (from parents, not school). Filters: Categoria, Tamanho, Preço máx. "Anunciar Item" button. Each card shows contact info + "Ligar/Contactar" button (just displays phone/email, no API call).  

**Query params:** `?categoryId=&maxPrice=&search=&page=1&pageSize=12`  
**Filter:** `fromSchool = false`, active only.  

**Returns:** same shape as school inventory but includes contact fields (now flat on Item after Fix 2):
```json
{
  "items": [{
    "itemId": 5,
    "name": "Sapatilhas Jazz Pretas",
    "description": "Sapatilhas de jazz usadas apenas 2 vezes",
    "categoryName": "Calçado",
    "imageUrl": "...",
    "lowestPrice": 25.00,
    "contactPhone": "+351 912 345 678",
    "contactEmail": null,
    "ownerName": "Teresa Martins"
  }],
  "totalCount": 8, "page": 1, "pageSize": 12
}
```
**"Anunciar Item" button** → `POST /api/items` (parent creates item with `fromSchool=false`, their own contact info).

---

## Controller 4 — `AdminPortalController`
**Route prefix:** `/api/admin`  
**Roles:** `admin` only

---

### `GET /api/admin/dashboard`
**Page:** `/admin` — *Painel de Administração*  
**UI:** Two KPI cards (Contas de Direção, Contas Ativas)  

**Returns:**
```json
{
  "totalStaffAccounts": 3,
  "activeStaffAccounts": 3
}
```
**Calls:** Users where role = staff.

---

### `GET /api/admin/users`
**Page:** `/admin/users` — *Contas de Direção*  
**UI:** Search box + table of staff users. "Nova Conta" button. Edit/deactivate buttons per row.  

**Query params:** `?search=&page=1&pageSize=20`  
**Filter:** Users with role `staff` only.  

**Returns:**
```json
{
  "items": [{
    "userId": 1,
    "name": "Carlos Rodrigues",
    "email": "carlos@example.com",
    "isActive": true
  }],
  "totalCount": 3, "page": 1, "pageSize": 20
}
```
**"Nova Conta"** → `POST /api/users` (existing, body: `{ username, email, firstRole: 1 }`)  
**Edit icon** → `PUT /api/users/{id}` (edit modal)  
**Deactivate (×)** → `PATCH /api/users/{id}/deactivate`

---

## Summary Table — What to Build vs What Already Exists

| New Endpoint | Calls (existing) | Build |
|---|---|---|
| `GET /api/staff/dashboard` | Users count, Coaches count, Students count, Events count, CoachClasses count by status | **New** aggregation query in StaffDashboardService |
| `GET /api/staff/agenda` | CoachClasses + filters | **New** query (reuse CoachClass includes) |
| `GET /api/staff/validate-classes` | CoachClasses by status + Participants | **New** paged query |
| `GET /api/staff/validate-students` | Students by acceptance_status | **New** paged query |
| `GET /api/staff/billing/students` | Participants (Validated) + CoachClasses + AppSettings | **New** computed aggregation in BillingService |
| `GET /api/staff/billing/coaches` | CoachClasses (Validated) + AppSettings | **New** computed aggregation in BillingService |
| `GET /api/staff/appsettings` | AppSettingService.GetAllAsync() | **Thin wrapper** — nearly free |
| `PATCH /api/staff/appsettings/{key}` | AppSettingService.UpdateAsync() | **Thin wrapper** — nearly free |
| `GET /api/coach/dashboard` | CoachClasses by coachId | **New** aggregation |
| `GET /api/coach/agenda` | CoachClasses by coachId + date range | **New** query |
| `GET /api/coach/validate` | CoachClasses (StaffApproved/Finished) by coachId | **New** paged query |
| `GET /api/ee/dashboard` | CoachClasses by createdBy | **New** aggregation |
| `GET /api/ee/classes/my` | CoachClasses by createdBy + date range | **New** query |
| `GET /api/ee/classes/available-slots` | CoachAvailability + CoachClasses + BlockedPeriods | **New** BookingService (most complex) |
| `GET /api/ee/classes/open` | CoachClasses (Approved, not full) + modality filter | **Extends** existing open query |
| `GET /api/ee/classes/validate` | Participants (Pending validation, Finished class) by parent | **New** query |
| `GET /api/ee/students` | Students by parentUserId | **Thin wrapper** (already enforced) |
| `GET /api/ee/inventory/school` | Items (fromSchool=true) + variants | **New** filtered query |
| `GET /api/ee/inventory/community` | Items (fromSchool=false) + contact fields | **New** filtered query |
| `GET /api/admin/dashboard` | Users by role=staff | **New** (tiny) |
| `GET /api/admin/users` | Users by role=staff + search | **New** paged query |

---

## New Services to Create

| Service | Responsibility |
|---|---|
| `StaffDashboardService` | Dashboard KPIs, agenda query, billing computations |
| `BookingService` | Available slots algorithm (CoachAvailability − bookings − blocked periods) |
| `BillingService` | Compute student/coach billing from Validated classes + AppSettings rates |
| `ParentPortalService` | My-classes, validate-classes, inventory filtered queries |
| `CoachPortalService` | Dashboard, agenda, validate queries (all filtered by coachId from JWT) |
| `AdminPortalService` | Staff user list + dashboard counts |

These services receive `AppDbContext` via constructor injection like all others and are auto-registered (names end in `Service`).

---

## Existing Endpoints Re-used by Frontend (not new controllers)

The following existing primitive endpoints are called **directly** from the frontend without going through an advanced controller — they are mutation or simple-fetch calls:

| Frontend action | Existing endpoint |
|---|---|
| Login form submit | `POST /api/auth/login` |
| Navbar logout button | `POST /api/auth/logout` |
| Page load session restore | `GET /api/auth/me` |
| Staff accept class request | `PATCH /api/coachclasses/{id}/staff-approve` |
| Staff reject class request | `PATCH /api/coachclasses/{id}/reject` |
| Staff finish class | `PATCH /api/coachclasses/{id}/finish` |
| Staff final validate | `PATCH /api/coachclasses/{id}/staff-validate` |
| Coach accept request | `PATCH /api/coachclasses/{id}/coach-accept` |
| Coach reject request | `PATCH /api/coachclasses/{id}/coach-reject` |
| Coach validate class taught | `PATCH /api/coachclasses/{id}/coach-validate` |
| Parent submit class request | `POST /api/coachclasses` |
| Parent enroll student in open class | `POST /api/participants` |
| Parent validate attendance | `PATCH /api/participants/{id}/parent-validate` |
| Parent add student | `POST /api/students` |
| Parent deactivate student | `PATCH /api/students/{id}/deactivate` |
| Parent requisition item | `POST /api/requisitions` |
| Staff accept student | `PATCH /api/students/{id}/accept` |
| Staff reject student | `PATCH /api/students/{id}/reject` |
| Staff create/edit modality | `POST /api/modalities`, `PATCH /api/modalities/{id}` |
| Staff assign coach to modality | `POST /api/modalities/{id}/coaches/{coachId}` |
| Staff create/edit studio | `POST /api/studios`, `PUT /api/studios/{id}` |
| Staff create/edit event | `POST /api/events`, `PUT /api/events/{id}` |
| Staff create/edit blocked period | `POST /api/blockedperiods`, `PUT /api/blockedperiods/{id}` |
| Staff create user | `POST /api/users` |
| Staff deactivate user | `PATCH /api/users/{id}/deactivate` |
| Staff update app setting | `PATCH /api/staff/appsettings/{key}` |
| Notification bell | `GET /api/notifications/user/{userId}` |
| Mark notification read | `PATCH /api/notifications/{id}/read` |

---

## Missing Primitive Endpoints to Add

These are gaps found during this analysis — small additions to existing controllers:

| What | Where | Why needed |
|---|---|---|
| `PUT /api/students/{id}` | StudentController | Parent "Editar" button on student modal needs to update PersonInfo fields |
| `GET /api/coaches/me` | CoachController | Coach portal needs own coachId to filter — or derive from JWT claims in service |
| `GET /api/ee/classes/open?modalityId=` | Extend existing `/api/coachclasses/open` | Needs modality filter + enforce `currentParticipants < maxParticipants` (mockup fix) |
| `PATCH /api/users/{id}/personinfo` | UserController | Coach and parent "edit profile" — update PersonInfo fields |

---

## Notes on Deferred Items (not v2 scope)

- **Payment status tracking** (`Paid`/`Pending`/`Late` on billing tables) — there is no payments table. Billing shows computed amounts only. Payment status fields return `null` until a payments module is built.
- **Excel export** (`/api/staff/billing/*/export`) — implement after billing rows are confirmed correct.
- **App_Settings admin tab** — `/admin` sidebar mentions this but it's lower priority than staff operations.
- **News posts frontend** — `GET /api/newsposts` already works, just needs a page in React.