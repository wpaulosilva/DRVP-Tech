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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  Helpers 
        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}