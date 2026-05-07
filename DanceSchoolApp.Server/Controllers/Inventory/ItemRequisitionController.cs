using DanceSchoolApp.Server.DTOs.Inventory;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.Inventory
{
    // ═══════════════════════════════════════════════════════════════════════════
    // REQUISITIONS  –  /api/requisitions
    // ═══════════════════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/requisitions")]
    [EnableRateLimiting("api")]
    public class ItemRequisitionController : ControllerBase
    {
        private readonly ItemRequisitionService _requisitionService;

        public ItemRequisitionController(ItemRequisitionService requisitionService)
        {
            _requisitionService = requisitionService;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private bool IsStaff() =>
            User.IsInRole("staff");

        //  GET /api/requisitions 
        /// <summary>Staff sees all requisitions. Parent sees only their own.</summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetRequisitions()
        {
            try
            {
                var result = IsStaff()
                    ? await _requisitionService.GetAllAsync()
                    : await _requisitionService.GetByParentAsync(GetUserId());

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/requisitions/parent/{parentId} 
        /// <summary>Get all requisitions for a specific parent. Staff may fetch any parent; a parent may fetch their own only.</summary>
        [HttpGet("parent/{parentId}")]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> GetRequisitionsByParent(int parentId)
        {
            try
            {
                if (!IsStaff() && parentId != GetUserId())
                    return Forbid();

                var result = await _requisitionService.GetByParentAsync(parentId);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/requisitions/{id} 
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetRequisition(int id)
        {
            try
            {
                var result = await _requisitionService.GetByIdAsync(id);

                // Parents can only read their own requisitions
                if (!IsStaff() && result.IdParent != GetUserId())
                    return Forbid();

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

        //  POST /api/requisitions 
        [HttpPost]
        [Authorize(Roles = "parent")]
        public async Task<IActionResult> CreateRequisition([FromBody] ItemRequisitionCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _requisitionService.CreateAsync(request, GetUserId());
                return CreatedAtAction(nameof(GetRequisition), new { id = newId }, new { requisitionId = newId });
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  PATCH /api/requisitions/{id}/review 
        [HttpPatch("{id}/review")]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> ReviewRequisition(int id, [FromBody] ItemRequisitionReviewRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _requisitionService.ReviewAsync(id, request);
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  PATCH /api/requisitions/{id}/return 
        [HttpPatch("{id}/return")]
        [Authorize(Roles = "parent,staff")]
        public async Task<IActionResult> ReturnRequisition(int id, [FromBody] ItemRequisitionReturnRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _requisitionService.ReturnAsync(id, request);
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  DELETE /api/requisitions/{id} 
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> CancelRequisition(int id)
        {
            try
            {
                await _requisitionService.CancelAsync(id, GetUserId(), IsStaff());
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CATEGORIES  –  /api/item-categories
    // ═══════════════════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/item-categories")]
    [EnableRateLimiting("api")]
    public class ItemCategoryController : ControllerBase
    {
        private readonly ItemCategoryService _categoryService;

        public ItemCategoryController(ItemCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        //  GET /api/item-categories 
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var result = await _categoryService.GetAllAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/item-categories/{id} 
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetCategory(int id)
        {
            try
            {
                var result = await _categoryService.GetByIdAsync(id);
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

        //  POST /api/item-categories 
        [HttpPost]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> CreateCategory([FromBody] ItemCategoryCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _categoryService.CreateAsync(request);
                return CreatedAtAction(nameof(GetCategories), new { }, new { categoryId = newId });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  DELETE /api/item-categories/{id} 
        [HttpDelete("{id}")]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> DeactivateCategory(int id)
        {
            try
            {
                await _categoryService.DeactivateAsync(id);
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