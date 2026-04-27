using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.Services.People;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.People
{
    [Route("api/[controller]")]
    [ApiController]
    public class StaffController : ControllerBase
    {
        private readonly StaffService _StaffService;
        public StaffController(StaffService StaffService)
        {
            _StaffService = StaffService;
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        //  GET /api/staff 
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetStaffs()
        {
            try
            {
                var result = await _StaffService.GetStaffsAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
        //  GET /api/staff/{id} 
        [Authorize(Roles = "staff")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStaff(int id)
        {
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
