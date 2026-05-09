using DanceSchoolApp.Server.DTOs.School;
using DanceSchoolApp.Server.Services.School;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace DanceSchoolApp.Server.Controllers.School
{
    [ApiController]
    [Route("api/studios")]
    [EnableRateLimiting("api")]
    public class StudioController : ControllerBase
    {
        private readonly StudioService _studioService;

        public StudioController(StudioService studioService)
        {
            _studioService = studioService;
        }

        //  GET /api/studios 
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetStudios()
        {
            try
            {
                var result = await _studioService.GetStudiosAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/studios/{id} 
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudio(int id)
        {
            try
            {
                var result = await _studioService.GetStudioAsync(id);
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

        //  POST /api/studios 
        [Authorize(Roles = "staff")]
        [HttpPost]
        public async Task<IActionResult> CreateStudio([FromBody] StudioCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _studioService.CreateStudioAsync(request);
                return CreatedAtAction(nameof(GetStudio), new { id = newId }, new { studioId = newId });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  PATCH /api/studios/{id} 
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateStudio(int id, [FromBody] StudioUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _studioService.UpdateStudioAsync(id, request);
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
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  PATCH /api/studios/{id}/activate 
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/activate")]
        public async Task<IActionResult> ActivateStudio(int id)
        {
            try
            {
                await _studioService.SetStudioStateAsync(id, true);
                return NoContent();
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

        //  PATCH /api/studios/{id}/deactivate 
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateStudio(int id)
        {
            try
            {
                await _studioService.SetStudioStateAsync(id,false);
                return NoContent();
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

        //  POST /api/studios/{studioId}/modalities/{modalityId} 
        [Authorize(Roles = "staff")]
        [HttpPost("{studioId}/modalities/{modalityId}")]
        public async Task<IActionResult> AddModality(int studioId, int modalityId)
        {
            try
            {
                await _studioService.AddModalityAsync(studioId, modalityId);
                return Ok($"Modality {modalityId} added to studio {studioId}.");
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
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  DELETE /api/studios/{studioId}/modalities/{modalityId} 
        [Authorize(Roles = "staff")]
        [HttpDelete("{studioId}/modalities/{modalityId}")]
        public async Task<IActionResult> RemoveModality(int studioId, int modalityId)
        {
            try
            {
                await _studioService.RemoveModalityAsync(studioId, modalityId);
                return NoContent();
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
