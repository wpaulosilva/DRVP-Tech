# FlowArtes — Page-by-Page API Endpoint Map

## Build Audit Summary

All 4 portal controllers are present and wired:
- `StaffDashboardController` → `/api/staff/*` ✓
- `CoachPortalController` → `/api/coach/*` ✓  
- `ParentPortalController` → `/api/ee/*` ✓
- `AdminPortalController` → `/api/admin/*` ✓

Per-role `/me` endpoints confirmed built:
- `GET /api/coaches/me` (CoachController) ✓
- `GET /api/parents/me` (ParentController) ✓
- `GET /api/staff/me` (StaffController) ✓

**One gap found:** There is no unified `GET /api/users/me` for the header. The three role-specific `/me` endpoints solve this for each role individually — see the "Header" section below for the recommended approach.

**One missing endpoint from the v2 prompts not yet confirmed built:**  
`PATCH /api/users/{id}/personinfo` — profile editing. Verify this exists in UserController before building the profile edit modal in React.

---

## Header Component (every authenticated page)

The header shows: **avatar initials + full name** (not username), **notification bell with badge**, **"Sair" button**.

The header mounts once per session. It does NOT call an endpoint on every navigation — it reads from React context loaded at login time and enriched on first load.

### Header initialization sequence (app startup / page refresh)

| Step | Call | When | What it provides |
|---|---|---|---|
| 1 | `GET /api/auth/me` | Every page load / refresh | `userId`, `username`, `roles` — used to gate routes and restore session |
| 2 | Role-based name fetch (ONE of the three below) | Once after step 1 | Full name for display |
| 2a (parent) | `GET /api/parents/me` | role = parent | `name`, `email` |
| 2b (coach) | `GET /api/coaches/me` | role = coach | `name`, `email`, modalities |
| 2c (staff/admin) | `GET /api/staff/me` | role = staff or admin | `name`, `email` |
| 3 | `GET /api/notifications/user/{userId}?page=1&pageSize=1` | Once after step 1 | Unread count for bell badge |

**Recommendation:** Store `displayName` in AuthContext after step 2. The bell badge count should be polled every ~60s or driven by a future WebSocket.

### Header actions

| Element | Action | Call |
|---|---|---|
| Bell icon | Opens notification dropdown | `GET /api/notifications/user/{userId}?page=1&pageSize=10` |
| Individual notification | Mark as read | `PATCH /api/notifications/{id}/read` |
| "Sair" button | Logout | `POST /api/auth/logout` |

---

## Public Pages

### `/login` — LoginPage

| Element | Call | Notes |
|---|---|---|
| Form submit ("Login") | `POST /api/auth/login` | Body: `{ email, password }`. Sets jwt cookie. |
| "Esqueceu a password?" link → form | `POST /api/auth/forgot-password` | Body: `{ email }`. Always returns 200. |
| Reset password form (after email link) | `POST /api/auth/reset-password` | Body: `{ token, newPassword }`. |
| Page load (already logged in?) | `GET /api/auth/me` | If 200 → redirect to role dashboard. |

---

## Admin Role Pages (`/admin`)

### `/admin` — Painel de Administração (Dashboard)

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` → gate: role must include `admin` |
| Header name | `GET /api/staff/me` (admin has staff-level access) |
| KPI cards (Contas de Direção, Contas Ativas) | `GET /api/admin/dashboard` |

### `/admin/users` — Contas de Direção

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| User table (initial load) | `GET /api/admin/users?page=1&pageSize=20` |
| Search box (on change/submit) | `GET /api/admin/users?search={query}&page=1&pageSize=20` |
| Pagination arrows | `GET /api/admin/users?page={n}&pageSize=20` |
| "Nova Conta" button → form submit | `POST /api/users` body: `{ username, email, firstRole: 1 }` |
| Edit icon (✏) → form submit | `PATCH /api/users/{id}/personinfo` body: `{ firstName, lastName, ... }` |
| Deactivate icon (×) | `PATCH /api/users/{id}/deactivate` |

---

## Staff Role Pages (`/staff`)

### `/staff` — Painel de Direção (Dashboard)

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` → gate: role must include `staff` or `admin` |
| Header name | `GET /api/staff/me` |
| All KPI cards + Próximos Eventos strip | `GET /api/staff/dashboard` |

### `/staff/users` — Utilizadores (parent + coach accounts)

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| User table (initial load) | `GET /api/users?page=1&pageSize=20` |
| Search box | `GET /api/users?search={query}&page=1` |
| Role filter dropdown | `GET /api/users?role=parent&page=1` (if filter added) |
| Pagination | `GET /api/users?page={n}` |
| "Novo Utilizador" button → form submit | `POST /api/users` body: `{ username, email, firstRole }` |
| Edit icon → form submit | `PATCH /api/users/{id}/personinfo` |
| Deactivate (×) | `PATCH /api/users/{id}/deactivate` |
| Activate | `PATCH /api/users/{id}/activate` |
| Role change | `PUT /api/users/{id}/roles` |

### `/staff/validate-students` — Validar Novos Estudantes

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Tab "Pendentes" (default) | `GET /api/staff/validate-students?status=pending&page=1&pageSize=10` |
| Tab "Aprovados" | `GET /api/staff/validate-students?status=approved&page=1&pageSize=10` |
| Tab "Todos" | `GET /api/staff/validate-students?status=all&page=1&pageSize=10` |
| Pagination | `GET /api/staff/validate-students?status={tab}&page={n}` |
| "Aceitar Estudante" button | `PATCH /api/students/{id}/accept` |
| "Recusar Estudante" button | `PATCH /api/students/{id}/reject` body: `{ reason }` (required) |

### `/staff/validate-classes` — Validar Aulas

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Tab "Requisitadas" (default) | `GET /api/staff/validate-classes?tab=requested&page=1&pageSize=15` |
| Tab "Pendentes" | `GET /api/staff/validate-classes?tab=pending&page=1&pageSize=15` |
| Pagination | `GET /api/staff/validate-classes?tab={tab}&page={n}` |
| "Aceitar Aula" button (Requisitadas) | `PATCH /api/coachclasses/{id}/staff-approve` |
| "Recusar Aula" button (Requisitadas) | `PATCH /api/coachclasses/{id}/reject` body: `{ reason? }` |
| "Validar" button (Pendentes tab) | `PATCH /api/coachclasses/{id}/staff-validate` |
| "Cancelar" button (Pendentes tab) | `PATCH /api/coachclasses/{id}/cancel` |


### `/staff/modalities` — Modalidades

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Modality cards list | `GET /api/modalities` |
| "Adicionar Modalidade" → form submit | `POST /api/modalities` body: `{ name, description? }` |
| "Editar" button → form submit | `PATCH /api/modalities/{id}` body: `{ name, description? }` |
| "Eliminar" button | `PATCH /api/modalities/{id}/deactivate` |
| Coach assignment (per mockup: staff assigns coaches to modality) | `POST /api/modalities/{id}/coaches/{coachId}` |
| Coach unassignment | `DELETE /api/modalities/{id}/coaches/{coachId}` |
| Coach list for dropdown | `GET /api/coaches` |

### `/staff/studios` — Estúdios

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Studio cards list | `GET /api/studios` |
| Occupation calendar (week grid) | `GET /api/staff/agenda?from={weekStart}&to={weekEnd}&studioId={id}` |
| Studio filter dropdown | re-calls agenda with `studioId` param |
| Calendar day click → detail list | uses data already in response, no new call |
| "Adicionar Estúdio" → form submit | `POST /api/studios` body: `{ name, capacity, address?, modalityIds[] }` |
| Modality checkboxes in form | `GET /api/modalities` (to populate checkboxes) |
| "Editar" button → form submit | `PUT /api/studios/{id}` |
| "Eliminar" button | `PATCH /api/studios/{id}/deactivate` |

### `/staff/events` — Gestão de Eventos

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Events list | `GET /api/events` |
| "Inserir Evento" → form submit | `POST /api/events` body: `{ title, description?, startDatetime?, endDatetime?, imageUrl? }` |
| "Editar" → form submit | `PUT /api/events/{id}` |
| Deactivate icon | `PATCH /api/events/{id}/deactivate` |
| Delete icon | `DELETE /api/events/{id}` |

> Note: Mockup shows event "type" tags (Espetáculo, Reunião, etc.) but there is no `event_type` column in the DB. The mockup itself flagged this as wrong — do not implement until the field is added.

### `/staff/inventory` — Inventário (3-tab page: Escolar, Comunidade, Requisições)

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Item grid (Escolar tab) | `GET /api/items?fromSchool=true&page=1&pageSize=12` |
| Item grid (Comunidade tab) | `GET /api/items?fromSchool=false&page=1&pageSize=12` |
| Category filter | `GET /api/item-categories` (populate dropdown) then re-call with `&categoryId={id}` |
| Search | re-call with `&search={query}` |
| Item card click → detail view | `GET /api/items/{id}` (returns images, variants, category) |
| "Novo Item Escolar" → form submit | `POST /api/items/school` body: `{ name, description?, idCategory?, contactPhone?, contactEmail?, contactAddress? }` |
| Edit item metadata | `PATCH /api/items/{id}` body: `{ name?, description?, idCategory?, contactPhone?, contactEmail?, contactAddress? }` |
| Deactivate item (soft-delete) | `DELETE /api/items/{id}` |
| Add image to item | `POST /api/items/{id}/images` body: `{ imageUrl }` |
| Remove image from item | `DELETE /api/items/{id}/images/{imageId}` |
| List variants | `GET /api/items/{id}/variants` |
| Add variant | `POST /api/items/{id}/variants` body: `{ color?, size?, quantity, price? }` |
| Edit variant (incl. activate/deactivate) | `PATCH /api/items/{id}/variants/{variantId}` body: `{ color?, size?, quantity?, price?, isActive? }` |
| Delete variant (hard-delete, fails if active requisitions) | `DELETE /api/items/{id}/variants/{variantId}` |
| Requisition list (Requisições tab) | `GET /api/requisitions` (staff sees all) |
| Approve/reject requisition | `PATCH /api/requisitions/{id}/review` body: `{ approve, expectedReturnDate?, note? }` |
| Record return | `PATCH /api/requisitions/{id}/return` body: `{ returnQuantity }` |

### `/staff/blocked-periods` — Bloqueios

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Monthly calendar — all blocked periods | `GET /api/blockedperiods?from={monthStart}&to={monthEnd}` |
| Month navigation | re-calls with new `from`/`to` |
| Scope filter dropdown (Estúdio, Professor, Feriado, etc.) | re-calls with `&scope={0-5}` |
| Coach dropdown populate | `GET /api/coaches` |
| Studio dropdown populate | `GET /api/studios` |
| "Novo Bloqueio" → form submit | `POST /api/blockedperiods` body: `{ scope, startDatetime, endDatetime, reason?, idStudio?, idCoach? }` |
| Edit blocked period | `PUT /api/blockedperiods/{id}` |
| Delete blocked period | `DELETE /api/blockedperiods/{id}` |

> Scope values: 0=Undefined, 1=NormalClass, 2=Studio, 3=Coach, 4=Event, 5=Holiday. Scope 0 (Indefinido) should NOT be selectable when creating — it's only a fallback value.

### `/staff/agenda` — Agenda Global

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Week calendar (initial = current week) | `GET /api/staff/agenda?from={weekStart}&to={weekEnd}` |
| Studio filter dropdown | re-calls with `&studioId={id}` |
| Status filter | re-calls with `&status={byte}` |
| Navigation arrows (prev/next week) | `GET /api/staff/agenda?from={newStart}&to={newEnd}` |
| Day click → detail list below | uses data already in response |
| Studio list for dropdown | `GET /api/studios` |

### `/staff/billing` — Faturação

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Tab "Tabela de Alunos" (default) | `GET /api/staff/billing/students?month={YYYY-MM}&page=1&pageSize=25` |
| Tab "Tabela de Professores" | `GET /api/staff/billing/coaches?month={YYYY-MM}&page=1&pageSize=25` |
| Month picker change | re-calls with new `month` param |
| Search box | re-calls with `&search={query}` |
| Status filter | re-calls with `&status={value}` |
| Pagination | re-calls with `&page={n}` |
| "Exportar Excel" button | `GET /api/staff/billing/students/export?month={YYYY-MM}` *(deferred — not yet implemented)* |

### `/staff/appsettings` — Configurações (not yet in mockup sidebar, but needed for US19)

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Settings form load | `GET /api/staff/appsettings` |
| Save a value | `PATCH /api/staff/appsettings/{key}` body: `{ value }` |

---

## Coach Role Pages (`/coach`)

### `/coach` — Dashboard

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` → gate: role = coach |
| Header name | `GET /api/coaches/me` |
| KPI cards + Próximas Aulas strip | `GET /api/coach/dashboard` |

### `/coach/availability` — Disponibilidade

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Week availability grid | `GET /api/coachavailability/coach/{coachId}` — coachId from `GET /api/coaches/me` |
| "Adicionar Bloco" → form submit | `POST /api/coachavailability` body: `{ coachId, weekday, startTime, endTime, validFrom?, validUntil? }` |
| Edit slot | `PUT /api/coachavailability/{id}` |
| Delete slot | `DELETE /api/coachavailability/{id}` |
| *(missing from mockup)* Blocked periods / absence sub-page | `POST /api/blockedperiods` body: `{ scope:3, idCoach, startDatetime, endDatetime, ... }` — staff only, so coach cannot self-block; this page is staff-facing |

> Note: Mockup flagged "missing create absence days subpage" and the form fields were wrong. The correct `CoachAvailability` fields are `weekday, startTime, endTime, validFrom, validUntil`. Creating coach-specific blocked periods (absences) is a **staff action** — the coach cannot set their own blocked periods.

### `/coach/validate` — Validar Aulas

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Tab "Pedidos de Aula" (default) | `GET /api/coach/validate?tab=requests&page=1&pageSize=10` |
| Tab "Validações" | `GET /api/coach/validate?tab=validations&page=1&pageSize=10` |
| Pagination | re-calls with `&page={n}` |
| "Aceitar Aula" button | `PATCH /api/coachclasses/{id}/coach-accept` |
| "Recusar Aula" button | `PATCH /api/coachclasses/{id}/coach-reject` body: `{ reason? }` |
| "Realizada" button (validations tab) | `PATCH /api/coachclasses/{id}/coach-validate` body: `{ didTeach: true }` |
| "Não Realizada" button (validations tab) | `PATCH /api/coachclasses/{id}/coach-validate` body: `{ didTeach: false }` |

### `/coach/agenda` — Agenda

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Week calendar (initial = current week) | `GET /api/coach/agenda?from={weekStart}&to={weekEnd}` |
| Navigation arrows | `GET /api/coach/agenda?from={newStart}&to={newEnd}` |
| Day click → detail below | uses data already in response |
| "Cancelar" button on Approved class | `PATCH /api/coachclasses/{id}/cancel` *(staff only — coach cannot cancel; remove this button from coach view)* |

### `/coach/events` — Eventos (read-only)

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Events list | `GET /api/events` |

---

## Parent (EE) Role Pages (`/ee`)

### `/ee` — Dashboard

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` → gate: role = parent |
| Header name | `GET /api/parents/me` |
| KPI cards + Próximas Aulas strip | `GET /api/ee/dashboard` |

### `/ee/classes` — Aulas (4-tab page)

#### Tab: Minhas Marcações (default)

| Element | Call |
|---|---|
| Tab mount | `GET /api/ee/classes/my?from={weekStart}&to={weekEnd}` |
| Navigation arrows (prev/next week) | `GET /api/ee/classes/my?from={newStart}&to={newEnd}` |
| Day click → detail list below | uses data already in response |

#### Tab: Criar Aula

| Element | Call |
|---|---|
| Modality dropdown populate | `GET /api/modalities` |
| Coach dropdown populate | `GET /api/coaches` *(currently staff-only — needs opening to parent role for this dropdown, or provide via a dedicated parent-facing coach list endpoint)* |
| Calendar slot grid (on dropdown change or initial) | `GET /api/ee/classes/available-slots?from={weekStart}&to={weekEnd}&modalityId={id}&coachId={id}` |
| Navigation arrows on slot calendar | re-calls with new date range |
| "Pedir Aula" button on a slot → modal open | no API call yet (just opens modal) |
| Modal — student dropdown | `GET /api/ee/students` (already loaded or re-fetch) |
| Modal — "Confirmar Pedido" submit | `POST /api/coachclasses` body: `{ coachId, modalityId, startDatetime, endDatetime, maxParticipants, studentIds[] }` |

> Gap: `GET /api/coaches` is currently `[Authorize(Roles = "staff")]`. The parent needs a read-only coach list to populate the dropdown. **Fix:** add `parent` to the roles on `GET /api/coaches`, OR create `GET /api/coaches/available` under `/api/ee/` that returns only active coaches with their modalities. The latter is cleaner.

#### Tab: Aulas Existentes

| Element | Call |
|---|---|
| Tab mount | `GET /api/ee/classes/open?page=1&pageSize=10` |
| Modality filter | `GET /api/ee/classes/open?modalityId={id}&page=1` |
| Modality dropdown populate | `GET /api/modalities` |
| Pagination | `GET /api/ee/classes/open?page={n}` |
| "Ver Detalhes" button | `GET /api/coachclasses/{id}` |
| "Inscrever" button | `POST /api/participants` body: `{ classId, studentId }` |
| Student picker (before Inscrever) | `GET /api/ee/students` (pick which student to enroll) |

#### Tab: Validar Aulas

| Element | Call |
|---|---|
| Tab mount | `GET /api/ee/classes/validate?page=1&pageSize=10` |
| Pagination | `GET /api/ee/classes/validate?page={n}` |
| "Confirmar Realização" button | `PATCH /api/participants/{participantId}/parent-validate` body: `{ attended: true }` |
| "Não Realizada" button | `PATCH /api/participants/{participantId}/parent-validate` body: `{ attended: false }` |

### `/ee/students` — Meus Estudantes

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Student list | `GET /api/ee/students` |
| "Adicionar Estudante" → form submit | `POST /api/students` body: `{ personInfo: { firstName, lastName, birthDate, phone, address, nif } }` |
| "Editar" → modal → form submit | `PUT /api/students/{id}` body: `{ firstName, lastName, birthDate, phone, address, nif }` |
| "Remover" button | `PATCH /api/students/{id}/deactivate` |

### `/ee/inventory` — Inventário (2-tab page)

#### Tab: Escolar

| Element | Call |
|---|---|
| Tab mount | `GET /api/ee/inventory/school?page=1&pageSize=12` |
| Category filter dropdown | `GET /api/item-categories` (populate) then re-call with `&categoryId={id}` |
| Search box | re-call with `&search={query}` |
| Pagination | `GET /api/ee/inventory/school?page={n}` |
| Item card click → item detail page | `GET /api/items/{id}` |
| "Pedir Empréstimo" on item detail | `POST /api/requisitions` body: `{ itemVariantId, quantity, needFrom?, needUntil?, note? }` |
| My requisitions | `GET /api/requisitions` (parent sees own only) |

#### Tab: Comunidade

| Element | Call |
|---|---|
| Tab mount | `GET /api/ee/inventory/community?page=1&pageSize=12` |
| Category filter | `GET /api/item-categories` then re-call with `&categoryId={id}` |
| Max price filter | re-call with `&maxPrice={value}` |
| Search | re-call with `&search={query}` |
| Pagination | `GET /api/ee/inventory/community?page={n}` |
| "Ligar/Contactar" button | No API call — shows `contactPhone`/`contactEmail` from item card data |
| "Anunciar Item" → form submit | `POST /api/items/personal` body: `{ name, description?, contactPhone?, contactEmail?, contactAddress?, idCategory? }` |
| "Itens Publicados" badge count | use `totalCount` from the community inventory response, filtered by `idOwner = me` |
| **Own item management (owner only):** | |
| Edit own community item | `PATCH /api/items/{id}` body: `{ name?, description?, ... }` (ownership checked server-side) |
| Deactivate own community item | `DELETE /api/items/{id}` (ownership checked server-side) |
| Add image to own item | `POST /api/items/{id}/images` body: `{ imageUrl }` |
| Remove image from own item | `DELETE /api/items/{id}/images/{imageId}` |
| Add variant to own item | `POST /api/items/{id}/variants` body: `{ color?, size?, quantity, price? }` |
| Edit variant on own item | `PATCH /api/items/{id}/variants/{variantId}` body: `{ color?, size?, quantity?, price?, isActive? }` |
| Delete variant on own item | `DELETE /api/items/{id}/variants/{variantId}` |

> Note: Parents can only manage community items they own (`IdOwner` matches their user ID). The API returns 403 Forbidden if a parent tries to modify another user's item or a school item. Staff can manage all items regardless.

### `/ee/events` — Eventos (read-only)

| Element | Call |
|---|---|
| Page mount | `GET /api/auth/me` |
| Events list | `GET /api/events` |

---

## Gaps Found During This Audit

| Gap | Impact | Fix |
|---|---|---|
| `GET /api/coaches` is staff-only | Parent cannot populate coach dropdown in "Criar Aula" | Add `parent` to the `[Authorize]` on `GET /api/coaches`, or add `GET /api/ee/coaches` returning only `{ coachId, name, modalities[] }` for active coaches |
| `GET /api/users/me` does not exist as a unified endpoint | Header must branch by role to call the correct `/me` | Either keep the 3 role-specific calls (recommended — they return richer data anyway), or add `GET /api/users/me` that resolves PersonInfo for any role |
| Coach cannot create blocked periods for own absences | Mockup shows "Adicionar Bloco" implies absences too | This is intentional — blocked periods are staff-managed. Coach UI should only show their availability slots, not blocked periods. Clarify in frontend design. |
| `PUT /api/students/{id}` — verify it exists | Parent "Editar" student modal | Was in prompt 5 as a task. Confirm it's in StudentController before implementing the modal. |
| `PATCH /api/users/{id}/personinfo` — verify it exists | Profile editing for coach/parent/staff | Was in prompt 6 task B. Confirm it exists in UserController. |
| Staff "cancel" on coach agenda | Mockup shows "Cancelar" button in coach agenda view | `PATCH /api/coachclasses/{id}/cancel` is staff-only — this button should not appear in coach view, or only appear on Requested/StaffApproved classes where coach-reject is the right action. |
| No `GET /api/newsposts` page in React | News posts API exists but no frontend page | Add a simple `/news` or integrate into home/dashboard. Not blocking. |