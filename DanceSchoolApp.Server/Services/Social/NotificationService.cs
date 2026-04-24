using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Social
{
    public class NotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        // ─── Queries ──────────────────────────────────────────────────────────

        public async Task<NotificationPagedResult> GetByUserAsync(int userId, PagedQuery query)
        {
            bool userExists = await _context.Users
                .AnyAsync(u => u.UserId == userId);

            if (!userExists)
                throw new KeyNotFoundException($"User with id {userId} was not found.");

            var dbQuery = _context.Notifications
                .Where(n => n.IdUser == userId && (n.IsDeleted == null || n.IsDeleted == false));

            var total = await dbQuery.CountAsync();
            var totalUnread = await dbQuery.CountAsync(n => n.ReadAt == null);

            var items = await dbQuery
                .OrderByDescending(n => n.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(n => MapToResponse(n))
                .ToListAsync();

            return new NotificationPagedResult
            {
                Items = items,
                TotalCount = total,
                TotalUnread = totalUnread,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        // ─── Commands ─────────────────────────────────────────────────────────

        public async Task<int> CreateAsync(NotificationCreateRequest request)
        {
            // If UserId is provided, verify the user exists.
            if (request.UserId.HasValue)
            {
                bool userExists = await _context.Users
                    .AnyAsync(u => u.UserId == request.UserId.Value);

                if (!userExists)
                    throw new KeyNotFoundException(
                        $"User with id {request.UserId} was not found.");
            }

            var notification = new Notification
            {
                IdUser = request.UserId,
                Title = request.Title,
                Message = request.Message,
                Type = (byte)request.Type,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                CreatedAt = DateTime.UtcNow,
                IsSent = false,
                IsDeleted = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return notification.NotificationId;
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == notificationId &&
                    (n.IsDeleted == null || n.IsDeleted == false));

            if (notification is null)
                throw new KeyNotFoundException(
                    $"Notification with id {notificationId} was not found.");

            // Idempotent — marking an already-read notification as read is fine.
            if (notification.ReadAt is null)
            {
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            bool userExists = await _context.Users
                .AnyAsync(u => u.UserId == userId);

            if (!userExists)
                throw new KeyNotFoundException($"User with id {userId} was not found.");

            await _context.Notifications
                .Where(n =>
                    n.IdUser == userId &&
                    n.ReadAt == null &&
                    (n.IsDeleted == null || n.IsDeleted == false))
                .ExecuteUpdateAsync(n =>
                    n.SetProperty(x => x.ReadAt, DateTime.UtcNow));
        }

        public async Task SoftDeleteAsync(int notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == notificationId &&
                    (n.IsDeleted == null || n.IsDeleted == false));

            if (notification is null)
                throw new KeyNotFoundException(
                    $"Notification with id {notificationId} was not found.");

            notification.IsDeleted = true;
            await _context.SaveChangesAsync();
        }

        // ─── Internal helper ──────────────────────────────────────────────────
        // Called by other services (CoachClassService, ParticipantService, etc.)
        // to send notifications without going through the HTTP layer.
        // Example usage in CoachClassService.RejectAsync:
        //   await _notificationService.SendAsync(userId, "Class Rejected",
        //       reason, NotificationType.ClassUpdate, "CoachClass", classId);
        public async Task SendAsync(
            int? userId,
            string? title,
            string? message,
            NotificationType type,
            string? entityType = null,
            int? entityId = null)
        {
            var notification = new Notification
            {
                IdUser = userId,
                Title = title,
                Message = message,
                Type = (byte)type,
                EntityType = entityType,
                EntityId = entityId,
                CreatedAt = DateTime.UtcNow,
                IsSent = true,   // internally sent = already dispatched
                IsDeleted = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private static NotificationResponse MapToResponse(Notification n) =>
            new NotificationResponse
            {
                NotificationId = n.NotificationId,
                UserId = n.IdUser,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.HasValue ? (NotificationType)n.Type.Value : null,
                EntityType = n.EntityType,
                EntityId = n.EntityId,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt,
                IsSent = n.IsSent
            };
    }
}