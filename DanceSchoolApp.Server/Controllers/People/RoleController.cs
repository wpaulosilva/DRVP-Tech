using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Services.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Controllers.People
{

    [ApiController]
    [Route("api/[controller]s")]
    public class RoleController : ControllerBase
    {

        private readonly RoleService _roleService;

        public RoleController(RoleService roleService)
        {
            _roleService = roleService;
        }

        //  GET /api/roles 
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
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
            return StatusCode(StatusCodes.Status403Forbidden, "Role creation is disabled. Roles are managed at the system level.");

            // Unreachable until admin auth guard is added:
            // if (!ModelState.IsValid) return BadRequest(ModelState);
            // try
            // {
            //     await _roleService.CreateRoleAsync(request);
            //     return CreatedAtAction(nameof(GetRole), new { id = request.RoleId }, null);
            // }
            // catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            // catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        //  POST /api/roles/assign 
        [Authorize(Roles = "staff")]
        [HttpPost("assign")]
        public async Task<IActionResult> AssignRole([FromBody] RoleAssignRequest request)
        {
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
