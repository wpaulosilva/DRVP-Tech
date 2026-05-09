using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.Services.People;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using System.IO;
using System;
using System.Linq;
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

        [HttpPost("{id}/photo")]
        [Authorize(Roles = "coach,staff")]
        [EnableRateLimiting("uploads")]
        public async Task<IActionResult> UploadPhoto(int id, [FromForm] IFormFile file)
        {
            // A coach may only update their own photo; staff may update any.
            if (User.IsInRole("coach") && GetUserId() != id)
                return Forbid();

            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            if (file.Length > 5_000_000)
                return BadRequest("File exceeds the 5 MB limit.");

            var permitted = new[] { ".jpg", ".jpeg", ".png" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !permitted.Contains(ext))
                return BadRequest("Invalid file type.");

            // Validate magic bytes — extension alone can be spoofed.
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;
            var header = new byte[4];
            _ = await ms.ReadAsync(header);

            bool isJpeg = header[0] == 0xFF && header[1] == 0xD8;
            bool isPng  = header[0] == 0x89 && header[1] == 0x50
                       && header[2] == 0x4E && header[3] == 0x47;
            if (!isJpeg && !isPng)
                return BadRequest("File content does not match the declared image type.");

            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var webroot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var coachFolder = Path.Combine(webroot, "uploads", "coach");
            if (!Directory.Exists(coachFolder)) Directory.CreateDirectory(coachFolder);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(coachFolder, fileName);

            ms.Position = 0;
            using (var stream = System.IO.File.Create(filePath))
            {
                await ms.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/coach/{fileName}";

            try
            {
                await _CoachService.ReplacePhotoFileAsync(id, relativePath, webroot);
            }
            catch (KeyNotFoundException ex)
            {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
                return NotFound(ex.Message);
            }

            return Ok(new { path = relativePath });
        }

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
