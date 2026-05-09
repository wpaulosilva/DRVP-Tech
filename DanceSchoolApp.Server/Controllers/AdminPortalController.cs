using DanceSchoolApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DanceSchoolApp.Server.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("api")]
    public class AdminPortalController : ControllerBase
    {
        private readonly AdminPortalService _adminService;

        public AdminPortalController(AdminPortalService adminService)
        {
            _adminService = adminService;
        }

        //  GET /api/admin/dashboard 
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var result = await _adminService.GetDashboardAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/admin/users 
        // Query: search? (string), page (default 1), pageSize (default 20)
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortDir = "asc")
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;

            try
            {
                var result = await _adminService.GetStaffUsersAsync(search, page, pageSize, sortBy, sortDir);

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
