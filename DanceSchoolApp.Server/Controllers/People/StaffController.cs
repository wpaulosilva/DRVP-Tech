using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.Services.People;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.People
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("api")]
    public class StaffController : ControllerBase
    {
        private readonly StaffService _StaffService;
        private readonly IWebHostEnvironment _env;
        public StaffController(StaffService StaffService, IWebHostEnvironment env)
        {
            _StaffService = StaffService;
            _env = env;
        }

        //  GET /api/staff/me 
        [Authorize(Roles = "staff")]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var result = await _StaffService.GetStaffMeAsync(GetUserId());
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

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        //  GET /api/staff 
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetStaffs(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 7,
    [FromQuery] string? search = "",
    [FromQuery] string? sortBy = "",
    [FromQuery] string? sortDir = "asc")
        {
            // Development-only endpoint: staff listing is internal; frontend uses /api/staff/me and specific staff pages
            if (!_env.IsDevelopment())
                return StatusCode(StatusCodes.Status403Forbidden, "This endpoint is available only in development.");

            try
            {
                var result = await _StaffService.GetStaffsAsync(
                    page,
                    pageSize,
                    search,
                    sortBy,
                    sortDir
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
        //  GET /api/staff/{id} 
        [Authorize(Roles = "staff")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStaff(int id)
        {
            // Development-only endpoint: only allowed in Development environment
            if (!_env.IsDevelopment())
                return StatusCode(StatusCodes.Status403Forbidden, "This endpoint is available only in development.");

            try
            {
                var result = await _StaffService.GetStaffAsync(id);
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
    }
}
