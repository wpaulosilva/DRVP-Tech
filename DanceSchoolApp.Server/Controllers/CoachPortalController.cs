using DanceSchoolApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers
{
    [ApiController]
    [Route("api/coach")]
    [Authorize(Roles = "coach")]
    [EnableRateLimiting("api")]
    public class CoachPortalController : ControllerBase
    {
        private readonly CoachPortalService _coachPortalService;

        public CoachPortalController(CoachPortalService coachPortalService)
        {
            _coachPortalService = coachPortalService;
        }

        //  GET /api/coach/dashboard 
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var coach = await _coachPortalService.ResolveCoachAsync(GetUserId());
                var result = await _coachPortalService.GetDashboardAsync(coach.CoachId);
                return Ok(result);
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

        //  GET /api/coach/agenda 
        // Query: from, to (DateOnly, required). Range capped at 60 days.
        [HttpGet("agenda")]
        public async Task<IActionResult> GetAgenda(
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to)
        {
            if (to < from)
                return BadRequest("'to' must be on or after 'from'.");

            if ((to.DayNumber - from.DayNumber) > 60)
                return BadRequest("Date range must not exceed 60 days.");

            try
            {
                var coach = await _coachPortalService.ResolveCoachAsync(GetUserId());
                var result = await _coachPortalService.GetAgendaAsync(coach.CoachId, from, to);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
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

        //  GET /api/coach/validate 
        // Query: tab ("requests"|"validations", default "requests"),
        //        page (default 1), pageSize (default 10, max 50)
        [HttpGet("validate")]
        public async Task<IActionResult> GetValidate(
            [FromQuery] string tab      = "requests",
            [FromQuery] int    page     = 1,
            [FromQuery] int    pageSize = 10)
        {
            if (page < 1)      page     = 1;
            if (pageSize < 1)  pageSize = 1;
            if (pageSize > 50) pageSize = 50;

            try
            {
                var coach = await _coachPortalService.ResolveCoachAsync(GetUserId());

                if (tab.Equals("validations", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _coachPortalService
                        .GetPendingValidationsAsync(coach.CoachId, page, pageSize);

                    if (result.TotalCount == 0)
                        return NoContent();

                    return Ok(result);
                }
                else
                {
                    var result = await _coachPortalService
                        .GetPendingRequestsAsync(coach.CoachId, page, pageSize);

                    if (result.TotalCount == 0)
                        return NoContent();

                    return Ok(result);
                }
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

        //  GET /api/coach/students?modalityId={id}
        // Returns active+accepted students enrolled in the given modality.
        // Used by the coach to populate the student picker when creating a class.
        [HttpGet("students")]
        public async Task<IActionResult> GetStudentsByModality([FromQuery] int modalityId)
        {
            if (modalityId <= 0)
                return BadRequest("modalityId is required.");

            try
            {
                var result = await _coachPortalService.GetStudentsByModalityAsync(modalityId);
                if (!result.Any()) return NoContent();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  Private helpers

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
