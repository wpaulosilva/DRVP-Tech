using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Services.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.Social
{
    [ApiController]
    [Route("api/events")]
    [EnableRateLimiting("api")]
    public class EventController : ControllerBase
    {
        private readonly EventService _eventService;

        public EventController(EventService eventService)
        {
            _eventService = eventService;
        }

        //  GET /api/events 
        // Staff use — all events including inactive.
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _eventService.GetAllAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/events/active
        // All authenticated roles — only active events. SecretDescription populated per caller eligibility.
        [Authorize]
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                var result = await _eventService.GetActiveAsync(role, GetUserId());

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/events/{id}
        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                var result = await _eventService.GetByIdAsync(id, role, GetUserId());
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

        //  POST /api/events 
        // Staff use — create a new event. Created as active by default.
        [Authorize(Roles = "staff")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EventCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _eventService.CreateAsync(request, GetUserId());
                return CreatedAtAction(nameof(GetById), new { id = newId },
                    new { eventId = newId });
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

        //  PUT /api/events/{id} 
        // Staff use — update event details. IsActive managed separately below.
        [Authorize(Roles = "staff")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] EventUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _eventService.UpdateAsync(id, request);
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

        //  PATCH /api/events/{id}/secret-description
        // Coach use — update secret description only if the coach is assigned to this event.
        [Authorize(Roles = "coach")]
        [HttpPatch("{id}/secret-description")]
        public async Task<IActionResult> UpdateSecretDescription(int id, [FromBody] EventSecretDescriptionRequest request)
        {
            try
            {
                await _eventService.UpdateSecretDescriptionAsync(id, GetUserId(), request.SecretDescription);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  PATCH /api/events/{id}/activate
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            try
            {
                await _eventService.SetActiveStateAsync(id, isActive: true);
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

        //  PATCH /api/events/{id}/deactivate 
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            try
            {
                await _eventService.SetActiveStateAsync(id, isActive: false);
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

        //  Helpers 
        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        //  DELETE /api/events/{id} 
        // Staff use — hard delete.
        [Authorize(Roles = "staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _eventService.DeleteAsync(id);
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