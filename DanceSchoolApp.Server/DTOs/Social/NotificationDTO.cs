using System.ComponentModel.DataAnnotations;
using DanceSchoolApp.Server.DTOs;

namespace DanceSchoolApp.Server.DTOs.Social
{
    // ─── Type enum ────────────────────────────────────────────────────────────
    public enum NotificationType : byte
    {
        Info = 0,
        Success = 1,
        Warning = 2,
        ClassUpdate = 3,
        ValidationRequest = 4,
        System = 5
    }

    // ─── Responses ────────────────────────────────────────────────────────────

    public class NotificationResponse
    {
        public int NotificationId { get; set; }
        public int? UserId { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public NotificationType? Type { get; set; }

        // EntityType + EntityId form a polymorphic reference so the client
        // can navigate to the relevant resource (e.g. "CoachClass" + 42).
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsRead => ReadAt.HasValue;
        public bool IsSent { get; set; }
    }

    // Extends the generic paged result with a pre-computed unread count so
    // the client does not have to enumerate all pages to show the badge.
    public class NotificationPagedResult : PagedResult<NotificationResponse>
    {
        public int TotalUnread { get; set; }
    }

    // ─── Requests ─────────────────────────────────────────────────────────────

    // Used for manual staff creation and by internal services (e.g.
    // CoachClassService.RejectAsync) when wiring up the notification hook.
    public class NotificationCreateRequest
    {
        // Null = broadcast to all users (system-wide notification).
        public int? UserId { get; set; }

        [MaxLength(128)]
        public string? Title { get; set; }

        [MaxLength(256)]
        public string? Message { get; set; }

        [Required]
        public NotificationType Type { get; set; }

        // Polymorphic link to the entity this notification is about.
        // Both must be provided together or both omitted.
        [MaxLength(32)]
        public string? EntityType { get; set; }

        public int? EntityId { get; set; }
    }
}