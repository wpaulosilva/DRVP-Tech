using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Inventory;
using DanceSchoolApp.Server.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.IO;
using System;

namespace DanceSchoolApp.Server.Controllers.Inventory
{
    [ApiController]
    [Route("api/items")]
    [EnableRateLimiting("api")]
    public class ItemController : ControllerBase
    {
        private readonly ItemService _itemService;

        public ItemController(ItemService itemService)
        {
            _itemService = itemService;
        }

        //  Helpers 

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private bool IsStaff() =>
            User.IsInRole("staff");

        /// <summary>
        /// GERIR (criar/editar) — staff só em itens escola, parent só nos seus.
        /// </summary>
        private async Task EnsureCanManageItem(int itemId)
        {
            if (IsStaff())
            {
                var isSchoolItem = await _itemService.IsSchoolItemAsync(itemId);
                if (!isSchoolItem)
                    throw new UnauthorizedAccessException(
                        "Staff can only manage school items, not personal items from parents.");
                return;
            }

            if (!await _itemService.IsItemOwnerAsync(itemId, GetUserId()))
                throw new UnauthorizedAccessException(
                    "You can only manage your own community items.");
        }

        /// <summary>
        /// ELIMINAR — staff pode eliminar qualquer item (escola e community).
        /// Parent só pode eliminar os seus próprios itens.
        /// </summary>
        private async Task EnsureCanDeactivateItem(int itemId)
        {
            if (IsStaff()) return;

            if (!await _itemService.IsItemOwnerAsync(itemId, GetUserId()))
                throw new UnauthorizedAccessException(
                    "You can only deactivate your own items.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ITEMS
        // ═══════════════════════════════════════════════════════════════════════

        //  GET /api/items
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetItems(
            [FromQuery] bool? fromSchool = null,
            [FromQuery] PagedQuery? query = null,
            [FromQuery] int? categoryId = null)
        {
            try
            {
                var result = await _itemService.GetItemsAsync(fromSchool, query ?? new PagedQuery(), categoryId);

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/items/{id} 
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetItem(int id)
        {
            try
            {
                var result = await _itemService.GetItemAsync(id);
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

        //  GET /api/items/school 
        /// <summary>Returns all active school-owned items.</summary>
        [HttpGet("school")]
        [Authorize]
        public async Task<IActionResult> GetSchoolItems([FromQuery] PagedQuery? query = null)
        {
            try
            {
                var result = await _itemService.GetItemsAsync(true, query ?? new PagedQuery());

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/items/community 
        /// <summary>Returns all active community (parent-owned) items.</summary>
        [HttpGet("community")]
        [Authorize]
        public async Task<IActionResult> GetCommunityItems([FromQuery] PagedQuery? query = null)
        {
            try
            {
                var result = await _itemService.GetItemsAsync(false, query ?? new PagedQuery());

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  POST /api/items/school 
        /// <summary>Staff creates a school-owned item.</summary>
        [HttpPost("school")]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> CreateSchoolItem([FromBody] ItemCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _itemService.CreateItemAsync(request, GetUserId(), fromSchool: true);
                return CreatedAtAction(nameof(GetItem), new { id = newId }, new { itemId = newId });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/items/{source}/category/{categoryId} 
        [HttpGet("{source}/category/{categoryId:int}")]
        [Authorize]
        public async Task<IActionResult> GetItemsBySourceAndCategory(
            string source,
            int categoryId,
            [FromQuery] PagedQuery? query = null)
        {
            if (source != "school" && source != "community")
                return BadRequest("Source must be 'school' or 'community'.");

            var fromSchool = source == "school";

            try
            {
                var result = await _itemService.GetItemsByCategoryAsync(
                    categoryId, fromSchool, query ?? new PagedQuery());

                if (result.TotalCount == 0)
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

        //  POST /api/items/personal 
        /// <summary>Parent creates a personal item to share/sell.</summary>
        [HttpPost("personal")]
        [Authorize(Roles = "parent")]
        public async Task<IActionResult> CreatePersonalItem([FromBody] ItemCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _itemService.CreateItemAsync(request, GetUserId(), fromSchool: false);
                return CreatedAtAction(nameof(GetItem), new { id = newId }, new { itemId = newId });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  PATCH /api/items/{id} 
        /// <summary>Staff or item owner updates item metadata.</summary>
        [HttpPatch("{id:int}")]
        [Authorize(Roles = "staff,parent")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] ItemUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await EnsureCanManageItem(id);
                await _itemService.UpdateItemAsync(id, request);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
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

        //  DELETE /api/items/{id} 
        /// <summary>Staff or item owner deactivates an item (soft-delete).</summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "staff,parent")]
        public async Task<IActionResult> DeactivateItem(int id)
        {
            try
            {
                await EnsureCanDeactivateItem(id);
                await _itemService.DeactivateItemAsync(id);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
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

        // ═══════════════════════════════════════════════════════════════════════
        // IMAGES  –  /api/items/{id}/images
        // ═══════════════════════════════════════════════════════════════════════

        //  POST /api/items/{id}/images 
        /// <summary>Staff or item owner uploads an image file to an item.</summary>
        [HttpPost("{id:int}/images")]
        [Authorize(Roles = "staff,parent")]
        [EnableRateLimiting("uploads")]
        public async Task<IActionResult> UploadImage(int id, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var permitted = new[] { ".jpg", ".jpeg", ".png" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !permitted.Contains(ext))
                return BadRequest("Invalid file type.");

            string? filePath = null;

            try
            {
                await EnsureCanManageItem(id); 

                var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var webroot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var uploads = Path.Combine(webroot, "uploads", "items"); 
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                var fileName = $"{Guid.NewGuid()}{ext}";
                filePath = Path.Combine(uploads, fileName);

                using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/uploads/items/{fileName}"; 

                var imageId = await _itemService.AddImageFromFileAsync(id, relativePath);

                return Ok(new { imageId, path = relativePath }); 
            }
            catch (UnauthorizedAccessException ex)
            {
                if (filePath != null && System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath); 

                return Forbid(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                if (filePath != null && System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                if (filePath != null && System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  DELETE /api/items/{id}/images/{imageId} 
        /// <summary>Staff or item owner removes an image from an item.</summary>
        [HttpDelete("{id:int}/images/{imageId:int}")]
        [Authorize(Roles = "staff,parent")]
        public async Task<IActionResult> RemoveImage(int id, int imageId)
        {
            try
            {
                await EnsureCanDeactivateItem(id);
                await _itemService.RemoveImageAsync(id, imageId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
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

        // ═══════════════════════════════════════════════════════════════════════
        // VARIANTS  –  /api/items/{id}/variants
        // ═══════════════════════════════════════════════════════════════════════

        //  GET /api/items/{id}/variants 
        [HttpGet("{id:int}/variants")]
        [Authorize]
        public async Task<IActionResult> GetVariants(int id)
        {
            try
            {
                var result = await _itemService.GetVariantsAsync(id);

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

        //  POST /api/items/{id}/variants 
        /// <summary>Staff or item owner creates a variant.</summary>
        [HttpPost("{id:int}/variants")]
        [Authorize(Roles = "staff,parent")]
        public async Task<IActionResult> CreateVariant(int id, [FromBody] ItemVariantCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await EnsureCanManageItem(id);
                var variantId = await _itemService.CreateVariantAsync(id, request);
                return CreatedAtAction(nameof(GetVariants), new { id }, new { variantId });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
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

        //  PATCH /api/items/{id}/variants/{variantId} 
        /// <summary>Staff or item owner updates a variant (including activate/deactivate).</summary>
        [HttpPatch("{id:int}/variants/{variantId:int}")]
        [Authorize(Roles = "staff,parent")]
        public async Task<IActionResult> UpdateVariant(int id, int variantId, [FromBody] ItemVariantUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await EnsureCanManageItem(id);
                await _itemService.UpdateVariantAsync(id, variantId, request);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
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

        //  DELETE /api/items/{id}/variants/{variantId} 
        /// <summary>Staff or item owner hard-deletes a variant (if no active requisitions).</summary>
        [HttpDelete("{id:int}/variants/{variantId:int}")]
        [Authorize(Roles = "staff,parent")]
        public async Task<IActionResult> DeleteVariant(int id, int variantId)
        {
            try
            {
                await EnsureCanDeactivateItem(id);
                await _itemService.DeleteVariantAsync(id, variantId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
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
    }
}