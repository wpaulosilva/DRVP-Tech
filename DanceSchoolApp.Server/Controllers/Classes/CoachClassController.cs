using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Classes;
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

        public CoachClassController(CoachClassService coachClassService)
        {
            _coachClassService = coachClassService;
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

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/coachclasses/{id} 
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _coachClassService.GetByIdAsync(id);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/coachclasses/open 
        // Parent use — returns Approved classes with available spots.
        [Authorize(Roles = "staff,parent")]
        [HttpGet("open")]
        public async Task<IActionResult> GetOpenClasses()
        {
            try
            {
                var result = await _coachClassService.GetOpenClassesAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/coachclasses/status/{status} 
        // Staff use — filter classes by status.
        // Status values: 0=Requested, 1=Approved, 2=Rejected,
        //                3=Cancelled, 4=Finished, 5=Validated, 6=Pending, 7=StaffApproved
        [Authorize(Roles = "staff")]
        [HttpGet("status/{status}")]
        public async Task<IActionResult> GetByStatus(byte status)
        {
            if (!Enum.IsDefined(typeof(CoachClassStatus), status))
                return BadRequest($"Invalid status value '{status}'. " +
                    "Valid values: 0=Requested, 1=Approved, 2=Rejected, " +
                    "3=Cancelled, 4=Finished, 5=Validated, 6=Pending, 7=StaffApproved.");

            try
            {
                var result = await _coachClassService
                    .GetByStatusAsync((CoachClassStatus)status);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/coachclasses/parent/{parentUserId} 
        // Parent use — returns all classes where this parent's students are enrolled.
        [Authorize(Roles = "staff,parent")]
        [HttpGet("parent/{parentUserId}")]
        public async Task<IActionResult> GetByParent(int parentUserId)
        {
            if (!IsStaff() && parentUserId != GetUserId())
                return Forbid();

            try
            {
                var result = await _coachClassService.GetByParentAsync(parentUserId);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/coachclasses/coach/{coachId} 
        // Coach use — view own schedule. Staff can view any coach's schedule.
        [Authorize(Roles = "staff,coach")]
        [HttpGet("coach/{coachId}")]
        public async Task<IActionResult> GetByCoach(int coachId)
        {
            if (!IsStaff() && coachId != GetUserId())
                return Forbid();

            try
            {
                var result = await _coachClassService.GetByCoachAsync(coachId);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  POST /api/coachclasses 
        // Parent use — creates a class request with at least one student.
        // Runs all conflict checks before inserting.
        [Authorize(Roles = "parent")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CoachClassCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _coachClassService.CreateAsync(request, GetUserId());
                return CreatedAtAction(nameof(GetById), new { id = newId },
                    new { classId = newId });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  Helpers 
        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private bool IsStaff() => User.IsInRole("staff");

        //  PATCH /api/coachclasses/{id}/staff-respond 
        // Staff use — Requested → StaffApproved (approve=true) or Rejected (approve=false).
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/staff-respond")]
        public async Task<IActionResult> StaffRespond(int id, [FromBody] StaffRespondRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _coachClassService.StaffRespondAsync(id, request.Approve, request.Reason);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        //  PATCH /api/coachclasses/{id}/coach-respond 
        // Coach use — StaffApproved → Approved (accept=true) or Rejected (accept=false).
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
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        //  PATCH /api/coachclasses/{id}/cancel 
        //  Staff use — transitions Requested or Approved → Cancelled.
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _coachClassService.CancelAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  PATCH /api/coachclasses/{id}/coach-validate 
        // Coach use — confirms or denies they taught the class.
        // Only available on Finished classes.
        [Authorize(Roles = "coach")]
        [HttpPatch("{id}/coach-validate")]
        public async Task<IActionResult> CoachValidate(int id,
            [FromBody] CoachValidateRequest request)
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
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        //  PATCH /api/coachclasses/{id}/staff-validate 
        // Staff use — final sign-off. Transitions Pending → Validated.
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/staff-validate")]
        public async Task<IActionResult> StaffValidate(int id, [FromBody] StaffValidateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _coachClassService.StaffValidateAsync(id, request.Confirmed, request.Reason);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }
    }
}