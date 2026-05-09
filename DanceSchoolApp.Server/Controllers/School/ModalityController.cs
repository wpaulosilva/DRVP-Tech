using DanceSchoolApp.Server.DTOs.School;
using DanceSchoolApp.Server.Services.School;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace DanceSchoolApp.Server.Controllers.School
{
    [ApiController]
    [Route("api/modalities")]
    [EnableRateLimiting("api")]
    public class ModalityController : ControllerBase
    {
        private readonly ModalityService _modalityService;

        public ModalityController(ModalityService modalityService)
        {
            _modalityService = modalityService;
        }

        //  GET /api/modalities 
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetModalities()
        {
            try
            {
                var result = await _modalityService.GetModalitiesAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/modalities/{id} 
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetModality(int id)
        {
            try
            {
                var result = await _modalityService.GetModalityAsync(id);
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

        //  POST /api/modalities 
        [Authorize(Roles = "staff")]
        [HttpPost]
        public async Task<IActionResult> CreateModality([FromBody] ModalityCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _modalityService.CreateModalityAsync(request);
                return CreatedAtAction(nameof(GetModality), new { id = newId }, new { modalityId = newId });
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

        //  PATCH /api/modalities/{id} 
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateModality(int id, [FromBody] ModalityUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _modalityService.UpdateModalityAsync(id, request);
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

        //  PATCH /api/modalities/{id}/activate 
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/activate")]
        public async Task<IActionResult> ActivateModality(int id)
        {
            try
            {
                await _modalityService.SetModalityStateAsync(id, isActive: true);
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

        //  PATCH /api/modalities/{id}/deactivate 
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateModality(int id)
        {
            try
            {
                await _modalityService.SetModalityStateAsync(id, isActive: false);
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

        //  POST /api/modalities/{id}/coaches/{coachId} 
        // Staff only — assign a coach to this modality.
        [Authorize(Roles = "staff")]
        [HttpPost("{id}/coaches/{coachId}")]
        public async Task<IActionResult> AssignCoach(int id, int coachId)
        {
            try
            {
                await _modalityService.AssignCoachAsync(id, coachId);
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

        //  DELETE /api/modalities/{id}/coaches/{coachId} 
        // Staff only — unassign a coach from this modality.
        [Authorize(Roles = "staff")]
        [HttpDelete("{id}/coaches/{coachId}")]
        public async Task<IActionResult> UnassignCoach(int id, int coachId)
        {
            try
            {
                await _modalityService.UnassignCoachAsync(id, coachId);
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
