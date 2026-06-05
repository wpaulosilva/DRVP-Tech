using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.Services.People;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DanceSchoolApp.Server.Controllers.People
{
    [Route("api/[controller]es")]
    [ApiController]
    [EnableRateLimiting("api")]
    public class CoachController : ControllerBase
    {

        private readonly CoachService _CoachService;
        public CoachController(CoachService CoachService)
        {
            _CoachService = CoachService;
        }

        //  GET /api/coaches/available 
        // Active coaches with their modalities — slim read for booking dropdowns.
        [Authorize(Roles = "parent,staff")]
        [HttpGet("available")]
        public async Task<IActionResult> GetAvailable()
        {
            try
            {
                var result = await _CoachService.GetAvailableCoachesAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/coaches/me 
        [Authorize(Roles = "coach")]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            try
            {
                var result = await _CoachService.GetCoachMeAsync(GetUserId());
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

        //  GET /api/coaches
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetCoachs()
        {
            try
            {
                var result = await _CoachService.GetCoachsAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/coaches/{id} 
        [Authorize(Roles = "staff")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCoach(int id)
        {
            try
            {
                var result = await _CoachService.GetCoachAsync(id);
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
