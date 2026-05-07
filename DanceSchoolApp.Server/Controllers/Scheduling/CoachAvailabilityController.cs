using DanceSchoolApp.Server.DTOs.Scheduling;
using DanceSchoolApp.Server.Services.Scheduling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace DanceSchoolApp.Server.Controllers.Scheduling
{
    [ApiController]
    [Route("api/[Controller]")]
    [EnableRateLimiting("api")]
    public class CoachAvailabilityController : ControllerBase
    {
        private readonly CoachAvailabilityService _availabilityService;

        public CoachAvailabilityController(CoachAvailabilityService availabilityService)
        {
            _availabilityService = availabilityService;
        }

        //  GET /api/coachavailability 
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _availabilityService.GetAllAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/coachavailability/{id} 
        [Authorize(Roles = "staff,coach")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _availabilityService.GetByIdAsync(id);
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

        //  GET /api/coachavailability/coach/{coachId} 
        // Returns all weekly availability slots defined for a specific coach.
        // The complex "what slots are free on date X" query belongs in
        // BookingController once CoachClass is built.
        [Authorize(Roles = "staff,coach")]
        [HttpGet("coach/{coachId}")]
        public async Task<IActionResult> GetByCoach(int coachId)
        {
            try
            {
                var result = await _availabilityService.GetByCoachAsync(coachId);

                if (!result.Any())
                    return NoContent();

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

        //  POST /api/coachavailability 
        [Authorize(Roles = "staff,coach")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CoachAvailabilityCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _availabilityService.CreateAsync(request);
                return CreatedAtAction(nameof(GetById), new { id = newId }, new { coachAvId = newId });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  PUT /api/coachavailability/{id} 
        [Authorize(Roles = "staff,coach")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CoachAvailabilityUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _availabilityService.UpdateAsync(id, request);
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
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  DELETE /api/coachavailability/{id} 
        // Hard delete — availability slots are configuration data, not
        // transactional records, so soft delete is not needed here.
        [Authorize(Roles = "staff,coach")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _availabilityService.DeleteAsync(id);
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
