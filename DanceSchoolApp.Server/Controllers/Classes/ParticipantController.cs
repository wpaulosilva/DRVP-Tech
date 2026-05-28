using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Services.Classes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.Classes
{
    [ApiController]
    [Route("api/participants")]
    [EnableRateLimiting("api")]
    public class ParticipantController : ControllerBase
    {
        private readonly ParticipantService _participantService;

        public ParticipantController(ParticipantService participantService)
        {
            _participantService = participantService;
        }

        //  GET /api/participants/class/{classId} 
        // Staff use — full enrollment list for a class with validation statuses.
        [Authorize(Roles = "staff,coach")]
        [HttpGet("class/{classId}")]
        public async Task<IActionResult> GetByClass(int classId, [FromQuery] PagedQuery query)
        {
            try
            {
                var result = await _participantService.GetByClassAsync(classId, query);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  POST /api/participants 
        // Parent use — enroll a student in an open class.
        // Checks: class is Approved + has space, student is active,
        // no duplicate enrollment, no time conflict with other classes.
        [Authorize(Roles = "parent")]
        [HttpPost]
        public async Task<IActionResult> JoinClass([FromBody] ParticipantJoinRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            try
            {
                var newId = await _participantService.JoinClassAsync(request, userId);
                return CreatedAtAction(
                    nameof(GetByClass),
                    new { classId = request.ClassId },
                    new { participantId = newId });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  POST /api/participants/invite-join
        // Parent use — enroll a student in a class received via invite.
        // Class can be Requested, CoachApproved, or Approved.
        // Not gated by join_class_enabled setting.
        [Authorize(Roles = "parent")]
        [HttpPost("invite-join")]
        public async Task<IActionResult> InviteJoin([FromBody] ParticipantJoinRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            try
            {
                var newId = await _participantService.InviteJoinAsync(request, userId);
                return CreatedAtAction(
                    nameof(GetByClass),
                    new { classId = request.ClassId },
                    new { participantId = newId });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  PATCH /api/participants/{id}/parent-validate
        // Parent use — confirm whether their student attended the class.
        // Only available when class status is Finished.
        // Sets ValidationStatus to ParentConfirmed (1) or Disputed (2).
        // Once all participants in the class have responded, the class is
        // automatically advanced to Pending for staff final review.
        [Authorize(Roles = "parent")]
        [HttpPatch("{id}/parent-validate")]
        public async Task<IActionResult> ParentValidate(int id, [FromBody] ParticipantValidateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _participantService.ParentValidateAsync(id, request.Attended, GetUserId());
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
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  PATCH /api/participants/{id}/parent-approve-enrollment
        // Parent use — approve or reject their student's enrollment in a coach-created class.
        // Only valid while the class is in Requested status.
        // When all parents have responded the class auto-advances to CoachApproved (or auto-cancels).
        [Authorize(Roles = "parent")]
        [HttpPatch("{id}/parent-approve-enrollment")]
        public async Task<IActionResult> ParentApproveEnrollment(
            int id, [FromBody] ParticipantEnrollmentApproveRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _participantService.ParentApproveEnrollmentAsync(id, request.Approve, GetUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception) { return StatusCode(500, "An unexpected error occurred."); }
        }

        //  DELETE /api/participants/{id} 
        // Parent or staff use — remove a student from a class.
        // Only allowed when class is Requested or Approved.
        // Blocked if this would leave the class with zero participants —
        // cancel the class instead.
        [Authorize(Roles = "staff,parent")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveParticipant(int id)
        {
            try
            {
                await _participantService.RemoveParticipantAsync(id);
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
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  Helpers 
        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}