using DanceSchoolApp.Server.DTOs.Scheduling;
using DanceSchoolApp.Server.Services.Scheduling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace DanceSchoolApp.Server.Controllers.Scheduling
{
    [ApiController]
    [Route("api/blockedperiods")]
    [EnableRateLimiting("api")]
    public class BlockedPeriodController : ControllerBase
    {
        private readonly BlockedPeriodService _blockedPeriodService;

        public BlockedPeriodController(BlockedPeriodService blockedPeriodService)
        {
            _blockedPeriodService = blockedPeriodService;
        }

        //  GET /api/blockedperiods 
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _blockedPeriodService.GetAllAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/blockedperiods/{id} 
        [Authorize(Roles = "staff")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _blockedPeriodService.GetByIdAsync(id);
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

        //  GET /api/blockedperiods/active 
        // Returns all blocks where StartDatetime <= now <= EndDatetime.
        // The booking controller will call this to know what's currently blocked.
        [Authorize]
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            try
            {
                var result = await _blockedPeriodService.GetActiveAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/blockedperiods/range?from=&to= 
        // Returns any block overlapping the given datetime window.
        // Postman: add 'from' and 'to' as query params in the Params tab.
        // Example: ?from=2025-03-01T00:00:00&to=2025-03-31T23:59:59
        [Authorize(Roles = "staff,coach")]
        [HttpGet("range")]
        public async Task<IActionResult> GetByRange([FromQuery] BlockedPeriodRangeQuery query)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _blockedPeriodService.GetByRangeAsync(query.From, query.To);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/blockedperiods/coach/{coachId} 
        [Authorize(Roles = "staff,coach")]
        [HttpGet("coach/{coachId}")]
        public async Task<IActionResult> GetByCoach(int coachId)
        {
            try
            {
                var result = await _blockedPeriodService.GetByCoachAsync(coachId);

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
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  GET /api/blockedperiods/studio/{studioId} 
        [Authorize(Roles = "staff")]
        [HttpGet("studio/{studioId}")]
        public async Task<IActionResult> GetByStudio(int studioId)
        {
            try
            {
                var result = await _blockedPeriodService.GetByStudioAsync(studioId);

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
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        //  POST /api/blockedperiods 
        [Authorize(Roles = "staff")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BlockedPeriodCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _blockedPeriodService.CreateAsync(request);
                return CreatedAtAction(nameof(GetById), new { id = newId }, new { blockedId = newId });
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

        //  PUT /api/blockedperiods/{id} 
        [Authorize(Roles = "staff")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BlockedPeriodUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _blockedPeriodService.UpdateAsync(id, request);
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

        //  DELETE /api/blockedperiods/{id} 
        // Hard delete — blocked periods are time-bounded administrative records.
        [Authorize(Roles = "staff")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _blockedPeriodService.DeleteAsync(id);
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
