using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Services.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Controllers.People
{

    [ApiController]
    [Route("api/[controller]s")]
    [EnableRateLimiting("api")]
    public class RoleController : ControllerBase
    {

        private readonly RoleService _roleService;
        private readonly IWebHostEnvironment _env;

        public RoleController(RoleService roleService, IWebHostEnvironment env)
        {
            _roleService = roleService;
            _env = env;
        }

        //  GET /api/roles 
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            // Development-only endpoint: list of roles is for internal use
            if (!_env.IsDevelopment())
                return StatusCode(StatusCodes.Status403Forbidden, "This endpoint is available only in development.");

            try
            {
                var result = await _roleService.GetRolesAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/roles/{id} 
        [Authorize(Roles = "staff")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRole(byte id)
        {
            // Development-only endpoint: specific role fetch used for debugging
            if (!_env.IsDevelopment())
                return StatusCode(StatusCodes.Status403Forbidden, "This endpoint is available only in development.");

            try
            {
                var result = await _roleService.GetRoleAsync(id);
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

        //  POST /api/roles 
        // Intentionally disabled — roles are seeded at DB level.
        // Remove the early return when proper admin-only authorization is in place.
        [Authorize(Roles = "staff")]
        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] RoleCreateRequest request)
        {
            // Development-only endpoint: disabled in Production
            if (!_env.IsDevelopment())
                return StatusCode(StatusCodes.Status403Forbidden, "Role creation is disabled. Roles are managed at the system level.");

            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                await _roleService.CreateRoleAsync(request);
                return CreatedAtAction(nameof(GetRole), new { id = request.RoleId }, null);
            }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        //  POST /api/roles/assign 
        [Authorize(Roles = "staff")]
        [HttpPost("assign")]
        public async Task<IActionResult> AssignRole([FromBody] RoleAssignRequest request)
        {
            // Development-only endpoint: role assignment via API is for internal/testing use
            if (!_env.IsDevelopment())
                return StatusCode(StatusCodes.Status403Forbidden, "This endpoint is available only in development.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _roleService.AssignRoleAsync(request);
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

        //  DELETE /api/roles/remove 
        [Authorize(Roles = "staff")]
        [HttpDelete("remove")]
        public async Task<IActionResult> RemoveRole([FromBody] RoleAssignRequest request)
        {
            // Development-only endpoint: role removal via API is for internal/testing use
            if (!_env.IsDevelopment())
                return StatusCode(StatusCodes.Status403Forbidden, "This endpoint is available only in development.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _roleService.RemoveRoleAsync(request);
                return NoContent();
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
    }
}
