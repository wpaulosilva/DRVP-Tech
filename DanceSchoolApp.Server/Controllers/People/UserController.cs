using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Services.People;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DanceSchoolApp.Server.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.People
{
    [ApiController]
    [Route("api/users")]
    [EnableRateLimiting("api")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        //  GET /api/users 
        [HttpGet]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var result = await _userService.GetUsersAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);

            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/users/{id} 
        [HttpGet("{id}")]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var result = await _userService.GetUserAsync(id);
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

        //  GET /api/users/{id}/roles 
        [HttpGet("{id}/roles")]
        [Authorize(Roles = "staff")]

        public async Task<IActionResult> GetUserRoles(int id)
        {
            try
            {
                var result = await _userService.GetUserRolesAsync(id);
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

        //  POST /api/users 
        [HttpPost]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if(User.IsInRole("staff") && (request.FirstRole == Roles.Admin || request.FirstRole == Roles.Staff))
                return Forbid("Staff cannot create users with Admin or Staff roles.");

            try
            {
                var newId = await _userService.CreateUserAsync(request);
                return CreatedAtAction(nameof(GetUser), new { id = newId }, new { userId = newId });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
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

        //  PATCH /api/users/{id}/personinfo 
        // Ownership: caller must be {id} OR staff. All fields optional.
        // Creates PersonInfo if the user has none yet.
        [HttpPatch("{id}/personinfo")]
        [Authorize]
        public async Task<IActionResult> UpdatePersonInfo(
            int id, [FromBody] UpdatePersonInfoRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            int callerId = GetUserId();
            if (callerId != id && !User.IsInRole("staff"))
                return Forbid();

            try
            {
                await _userService.UpsertPersonInfoAsync(id, request);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        //  PATCH /api/users/{id}/activate 
        [HttpPatch("{id}/activate")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> ActivateUser(int id)
        {
            try
            {
                await _userService.SetUserStateAsync(id, isActive: true);
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

        //  Helpers 
        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        //  PATCH /api/users/{id}/deactivate 
        [Authorize(Roles = "staff,admin")]
        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateUser(int id)
        {
            try
            {
                await _userService.SetUserStateAsync(id, isActive: false);
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
