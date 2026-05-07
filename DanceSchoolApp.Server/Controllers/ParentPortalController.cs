using DanceSchoolApp.Server.Services;
using DanceSchoolApp.Server.Services.People;
using DanceSchoolApp.Server.Services.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers
{
    [ApiController]
    [Route("api/ee")]
    [Authorize(Roles = "parent")]
    [EnableRateLimiting("api")]
    public class ParentPortalController : ControllerBase
    {
        private readonly ParentPortalService _parentService;
        private readonly BookingService      _bookingService;
        private readonly CoachService        _coachService;

        public ParentPortalController(
            ParentPortalService parentService,
            BookingService      bookingService,
            CoachService        coachService)
        {
            _parentService  = parentService;
            _bookingService = bookingService;
            _coachService   = coachService;
        }

        //  GET /api/ee/dashboard 
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var result = await _parentService.GetDashboardAsync(GetUserId());
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/ee/classes/my 
        // Query: from (DateOnly, required), to (DateOnly, required)
        [HttpGet("classes/my")]
        public async Task<IActionResult> GetMyClasses(
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to)
        {
            if (to < from)
                return BadRequest("'to' must be on or after 'from'.");

            try
            {
                var result = await _parentService.GetMyClassesAsync(GetUserId(), from, to);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/ee/classes/available-slots 
        // Query: from (DateOnly), to (DateOnly), coachId? (int), modalityId? (int)
        // Validate: to >= from, max 31 days.
        [HttpGet("classes/available-slots")]
        public async Task<IActionResult> GetAvailableSlots(
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            [FromQuery] int? coachId    = null,
            [FromQuery] int? modalityId = null)
        {
            if (to < from)
                return BadRequest("'to' must be greater than or equal to 'from'.");

            if ((to.DayNumber - from.DayNumber) > 31)
                return BadRequest("Date range must not exceed 31 days.");

            try
            {
                var result = await _bookingService.GetAvailableSlotsAsync(
                    from, to, coachId, modalityId);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/ee/classes/open
        // Query: from (DateOnly, required), to (DateOnly, required), modalityId? (int)
        [HttpGet("classes/open")]
        public async Task<IActionResult> GetOpenClasses(
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            [FromQuery] int? modalityId = null)
        {
            if (to < from)
                return BadRequest("'to' must be on or after 'from'.");

            try
            {
                var result = await _parentService.GetOpenClassesAsync(modalityId, from, to);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/ee/classes/validate 
        // Query: page (default 1), pageSize (default 10, max 50)
        [HttpGet("classes/validate")]
        public async Task<IActionResult> GetValidate(
            [FromQuery] int page     = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1)      page     = 1;
            if (pageSize < 1)  pageSize = 1;
            if (pageSize > 50) pageSize = 50;

            try
            {
                var result = await _parentService.GetPendingValidationsAsync(
                    GetUserId(), page, pageSize);

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/ee/students 
        [HttpGet("students")]
        public async Task<IActionResult> GetStudents()
        {
            try
            {
                var result = await _parentService.GetMyStudentsAsync(GetUserId());

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/ee/inventory/school 
        // Query: categoryId? (int), search? (string), page (default 1), pageSize (default 12)
        [HttpGet("inventory/school")]
        public async Task<IActionResult> GetSchoolInventory(
            [FromQuery] int?    categoryId = null,
            [FromQuery] string? search     = null,
            [FromQuery] int     page       = 1,
            [FromQuery] int     pageSize   = 12)
        {
            if (page < 1)     page     = 1;
            if (pageSize < 1) pageSize = 1;

            try
            {
                var result = await _parentService.GetSchoolInventoryAsync(
                    categoryId, search, page, pageSize);

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/ee/inventory/community 
        // Query: categoryId? (int), maxPrice? (decimal), search? (string),
        //        page (default 1), pageSize (default 12)
        [HttpGet("inventory/community")]
        public async Task<IActionResult> GetCommunityInventory(
            [FromQuery] int?     categoryId = null,
            [FromQuery] decimal? maxPrice   = null,
            [FromQuery] string?  search     = null,
            [FromQuery] int      page       = 1,
            [FromQuery] int      pageSize   = 12)
        {
            if (page < 1)     page     = 1;
            if (pageSize < 1) pageSize = 1;

            try
            {
                var result = await _parentService.GetCommunityInventoryAsync(
                    categoryId, maxPrice, search, page, pageSize);

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/ee/coaches 
        // Slim coach list for booking dropdowns (active coaches + modalities only).
        [HttpGet("coaches")]
        public async Task<IActionResult> GetCoaches()
        {
            try
            {
                var result = await _coachService.GetAvailableCoachesAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  Private helpers 

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
