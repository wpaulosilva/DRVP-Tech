using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DanceSchoolApp.Server.Controllers
{
    [ApiController]
    [Route("api/appsettings")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("api")]
    public class AppSettingController : ControllerBase
    {
        private readonly AppSettingService _appSettingService;

        public AppSettingController(AppSettingService appSettingService)
        {
            _appSettingService = appSettingService;
        }

        //  GET /api/appsettings 
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _appSettingService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  PATCH /api/appsettings/{key} 
        [HttpPatch("{key}")]
        public async Task<IActionResult> Update(string key,
            [FromBody] AppSettingUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _appSettingService.UpdateAsync(key, request.Value);
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
