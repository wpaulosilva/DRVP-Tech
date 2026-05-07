using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Services.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.Social
{
    [ApiController]
    [Route("api/notifications")]
    [EnableRateLimiting("api")]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _notificationService;

        public NotificationController(NotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        //  GET /api/notifications/user/{userId} 
        // Returns all non-deleted notifications for a user, newest first.
        // Includes both read and unread — client filters by IsRead if needed.
        [Authorize]
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(int userId, [FromQuery] PagedQuery query)
        {
            if (!IsStaff() && userId != GetUserId())
                return Forbid();

            try
            {
                var result = await _notificationService.GetByUserAsync(userId, query);
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

        //  POST /api/notifications 
        // Staff or system use — manual notification creation.
        // Internal services should call NotificationService.SendAsync() directly
        // rather than going through this HTTP endpoint.
        [Authorize(Roles = "staff")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NotificationCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var newId = await _notificationService.CreateAsync(request);
                return StatusCode(StatusCodes.Status201Created, new { notificationId = newId });
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

        //  PATCH /api/notifications/{id}/read 
        // Marks a single notification as read. Idempotent — safe to call twice.
        [Authorize]
        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                await _notificationService.MarkAsReadAsync(id);
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

        //  PATCH /api/notifications/user/{userId}/read-all 
        // Marks all unread notifications for a user as read in one operation.
        // The client calls this when the user opens the notification panel.
        [Authorize]
        [HttpPatch("user/{userId}/read-all")]
        public async Task<IActionResult> MarkAllAsRead(int userId)
        {
            if (!IsStaff() && userId != GetUserId())
                return Forbid();

            try
            {
                await _notificationService.MarkAllAsReadAsync(userId);
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

        //  Helpers 
        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private bool IsStaff() => User.IsInRole("staff");

        //  DELETE /api/notifications/{id} 
        // Soft delete — sets IsDeleted = true.
        // Deleted notifications are excluded from GET /user/{userId} results.
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _notificationService.SoftDeleteAsync(id);
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