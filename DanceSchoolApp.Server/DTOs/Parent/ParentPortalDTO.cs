using DanceSchoolApp.Server.DTOs.Classes;

namespace DanceSchoolApp.Server.DTOs.Parent
{

    // ─── Dashboard ────────────────────────────────────────────────────────────

    public class ParentDashboardResponse
    {
        public string ParentName { get; set; } = null!;
        public int TotalScheduledClasses { get; set; }
        public int ClassesAwaitingValidation { get; set; }
        public List<ParentUpcomingClass> UpcomingClasses { get; set; } = new();
    }

    public class ParentUpcomingClass
    {
        public int ClassId { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string ModalityName { get; set; } = null!;
        public string CoachName { get; set; } = null!;
        public string StudioName { get; set; } = null!;
        public CoachClassStatus Status { get; set; }
        public string StudentName { get; set; } = null!;   // comma-joined when multiple
    }

    // ─── Open classes ─────────────────────────────────────────────────────────

    public class OpenClassItem
    {
        public int ClassId { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public int DurationMinutes { get; set; }
        public string ModalityName { get; set; } = null!;
        public string CoachName { get; set; } = null!;
        public string StudioName { get; set; } = null!;
        public int CurrentParticipants { get; set; }
        public int MaxParticipants { get; set; }
        public int SpotsAvailable { get; set; }
        public List<string> EnrolledStudents { get; set; } = new();
    }

    // ─── Validate ─────────────────────────────────────────────────────────────

    // One entry per CLASS (not per participant). Contains all of the parent's
    // students enrolled in that class, each with their current validation status.
    public class ParentValidateItem
    {
        public int ClassId { get; set; }
        public string ModalityName { get; set; } = null!;
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string CoachName { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }            // StartDatetime + 48h, computed
        public int MaxParticipants { get; set; }
        public int TotalParticipants { get; set; }
        public string CreatedByName { get; set; } = null!;
        public List<ParticipantSummaryItem> Participants { get; set; } = new();
    }

    // ─── Students ─────────────────────────────────────────────────────────────

    public class ParentStudentItem
    {
        public int StudentId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int? Age { get; set; }                      // computed from BirthDate
        public DateOnly? BirthDate { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Nif { get; set; }
        public byte AcceptanceStatus { get; set; }
        public bool IsActive { get; set; }
        public List<int> ModalityIds { get; set; } = new();
        public List<string> ModalityNames { get; set; } = new();
    }

    // ─── Inventory / school ───────────────────────────────────────────────────

    public class InventoryItemCard
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? CategoryName { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? LowestPrice { get; set; }
        public int VariantCount { get; set; }
    }

    // ─── Inventory / community ────────────────────────────────────────────────

    public class CommunityItemCard
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? CategoryName { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? LowestPrice { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? OwnerName { get; set; }
    }
}
