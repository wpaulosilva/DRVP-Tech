using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Services;
using DanceSchoolApp.Server.Services.Classes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.Classes
{
    [ApiController]
    [Route("api/coachclasses")]
    [EnableRateLimiting("api")]
    public class CoachClassController : ControllerBase
    {
        private readonly CoachClassService _coachClassService;
        private readonly AppSettingService _appSettingService;

        public CoachClassController(CoachClassService coachClassService, AppSettingService appSettingService)
        {
            _coachClassService = coachClassService;
            _appSettingService = appSettingService;
        }

        //  GET /api/coachclasses/default-price?date=YYYY-MM-DD
        // Returns the default per-participant price according to app settings (weekday/weekend).
        [Authorize(Roles = "staff")]
        [HttpGet("default-price")]
        public async Task<IActionResult> GetDefaultPrice([FromQuery] string? date)
        {
            try
            {
                DateTime classDate;
                if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParse(date, out classDate))
                    classDate = DateTime.Now;

                bool isSunday = classDate.DayOfWeek == DayOfWeek.Sunday;
                var weekdayRate = await _appSettingService.GetDecimalAsync("class_price_weekday", 36.00m);
                var weekendRate = await _appSettingService.GetDecimalAsync("class_price_weekend", 43.50m);
                var price = isSunday ? weekendRate : weekdayRate;
                return Ok(new { price });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/coachclasses
        // Staff use — returns all classes regardless of status.
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PagedQuery query)
        {
            try
            {
                var result = await _coachClassService.GetAllAsync(query);
                if (result.TotalCount == 0) return NoContent();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/coachclasses/{id}
        // Staff: unrestricted. Coach: own classes only. Parent: enrolled students only.
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _coachClassService.GetByIdAsync(id);

                if (!IsStaff())
                {
                    var callerId = GetUserId();
                    bool allowed = false;

                    if (User.IsInRole("coach"))
                        allowed = result.CoachId == callerId;
                    else if (User.IsInRole("parent"))
                        allowed = await _coachClassService.IsParentOfClassAsync(callerId, id);

                    if (!allowed) return Forbid();
                }

                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  GET /api/coachclasses/join-class-status
        [Authorize]
        [HttpGet("join-class-status")]
        public async Task<IActionResult> GetJoinClassStatus()
        {
            var enabled = await _appSettingService.GetBoolAsync("join_class_enabled", defaultValue: true);
            return Ok(new { enabled });
        }

        //  GET /api/coachclasses/max-participants
        // Coach use — returns the maximum group size allowed by the max_participants setting.
        [Authorize(Roles = "coach")]
        [HttpGet("max-participants")]
        public async Task<IActionResult> GetMaxParticipants()
        {
            var max = await _appSettingService.GetIntAsync("max_participants", defaultValue: 8);
            return Ok(new { maxParticipants = max });
        }

        //  GET /api/coachclasses/open
        // Parent use — Approved classes with available spots.
        [Authorize(Roles = "staff,parent")]
        [HttpGet("open")]
        public async Task<IActionResult> GetOpenClasses()
        {
            if (!await _appSettingService.GetBoolAsync("join_class_enabled", defaultValue: true))
                return StatusCode(StatusCodes.Status423Locked,
                    "A funcionalidade de inscrição em aulas está desativada.");

            try
            {
                var result = await _coachClassService.GetOpenClassesAsync();
                if (!result.Any()) return NoContent();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, "An unexpected error occurred.");
            }
        }

        //  GET /api/coachclasses/status/{status}
        [Authorize(Roles = "staff")]
        [HttpGet("status/{status}")]
        public async Task<IActionResult> GetByStatus(byte status)
        {
            if (!Enum.IsDefined(typeof(CoachClassStatus), status))
                return BadRequest($"Invalid status value '{status}'.");

            try
            {
                var result = await _coachClassService.GetByStatusAsync((CoachClassStatus)status);
                if (!result.Any()) return NoContent();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, "An unexpected error occurred.");
            }
        }

        //  GET /api/coachclasses/parent/{parentUserId}
        [Authorize(Roles = "staff,parent")]
        [HttpGet("parent/{parentUserId}")]
        public async Task<IActionResult> GetByParent(int parentUserId)
        {
            if (!IsStaff() && parentUserId != GetUserId())
                return Forbid();

            try
            {
                var result = await _coachClassService.GetByParentAsync(parentUserId);
                if (!result.Any()) return NoContent();
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  GET /api/coachclasses/coach/{coachId}
        [Authorize(Roles = "staff,coach")]
        [HttpGet("coach/{coachId}")]
        public async Task<IActionResult> GetByCoach(int coachId)
        {
            if (!IsStaff() && coachId != GetUserId())
                return Forbid();

            try
            {
                var result = await _coachClassService.GetByCoachAsync(coachId);
                if (!result.Any()) return NoContent();
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  POST /api/coachclasses
        // Parent use — requests an individual class for one of their own students.
        // MaxParticipants is enforced to 1 server-side.
        [Authorize(Roles = "parent")]
        [HttpPost]
        public async Task<IActionResult> ParentCreate([FromBody] CoachClassParentCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _coachClassService.ParentCreateAsync(request, GetUserId());
                return CreatedAtAction(nameof(GetById), new { id = newId }, new { classId = newId });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  POST /api/coachclasses/coach-create
        // Coach use — creates an individual or group class, selecting students with the modality.
        // Parents of enrolled students must then approve enrollment before staff review.
        [Authorize(Roles = "coach")]
        [HttpPost("coach-create")]
        public async Task<IActionResult> CoachCreate([FromBody] CoachClassCoachCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _coachClassService.CoachCreateAsync(request, GetUserId());
                return CreatedAtAction(nameof(GetById), new { id = newId }, new { classId = newId });
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  PATCH /api/coachclasses/{id}/coach-respond
        // Coach use — Requested → CoachApproved or Rejected (parent-created classes only).
        [Authorize(Roles = "coach")]
        [HttpPatch("{id}/coach-respond")]
        public async Task<IActionResult> CoachRespond(int id, [FromBody] CoachRespondRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                if (request?.Accept is null)
                    return BadRequest("O campo accept não chegou corretamente.");
                await _coachClassService.CoachRespondAsync(id, GetUserId(), request.Accept, request.Reason);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  PATCH /api/coachclasses/{id}/staff-respond
        // Staff use — CoachApproved → Approved or Rejected.
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/staff-respond")]
        public async Task<IActionResult> StaffRespond(int id, [FromBody] StaffRespondRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _coachClassService.StaffRespondAsync(id, request.Approve, request.Reason, request.PerParticipantPrice);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  PATCH /api/coachclasses/{id}/update-details
        // Staff use — update studio, start/end datetime; notifies coach and parents on change.
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/update-details")]
        public async Task<IActionResult> UpdateDetails(int id, [FromBody] CoachClassUpdateDetailsRequest request)
        {
            try
            {
                await _coachClassService.UpdateDetailsAsync(id, request);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  PATCH /api/coachclasses/{id}/cancel
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _coachClassService.CancelAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  PATCH /api/coachclasses/{id}/coach-validate
        [Authorize(Roles = "coach")]
        [HttpPatch("{id}/coach-validate")]
        public async Task<IActionResult> CoachValidate(int id, [FromBody] CoachValidateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _coachClassService.CoachValidateAsync(id, GetUserId(), request.DidTeach);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  PATCH /api/coachclasses/{id}/staff-validate
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/staff-validate")]
        public async Task<IActionResult> StaffValidate(int id, [FromBody] StaffValidateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _coachClassService.StaffValidateAsync(id, request.Confirmed, request.Reason, request.PerParticipantPrice);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private bool IsStaff() => User.IsInRole("staff");
    }
}
