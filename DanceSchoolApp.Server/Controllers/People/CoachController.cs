using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.Services.People;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.People
{
    [Route("api/[controller]es")]
    [ApiController]
    public class CoachController : ControllerBase
    {

        private readonly CoachService _CoachService;
        public CoachController(CoachService CoachService)
        {
            _CoachService = CoachService;
        }

        // ─── GET /api/coaches/available ───────────────────────────────────────
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // ─── GET /api/coaches/me ───────────────────────────────────────────────
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("{id}/photo")]
        [Authorize(Roles = "coach,staff")]
        public async Task<IActionResult> UploadPhoto(int id, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var permitted = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !permitted.Contains(ext))
                return BadRequest("Invalid file type.");

            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var webroot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsRoot = Path.Combine(webroot, "uploads");
            var coachFolder = Path.Combine(uploadsRoot, "coach");
            if (!Directory.Exists(coachFolder)) Directory.CreateDirectory(coachFolder);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(coachFolder, fileName);

            using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/coach/{fileName}";

            try
            {
                // before setting, if coach had previous photo, service should remove existing file
                await _CoachService.ReplacePhotoFileAsync(id, relativePath, webroot);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }

            return Ok(new { path = relativePath });
        }

        // ─── GET /api/coaches ──────────────────────────────────────────────────
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // ─── GET /api/coaches/{id} ─────────────────────────────────────────────
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
       
    }
}
