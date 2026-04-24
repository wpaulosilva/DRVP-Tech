using DanceSchoolApp.Server.DTOs.Classes;

namespace DanceSchoolApp.Server.DTOs.Coach
{
    // ─── Dashboard ────────────────────────────────────────────────────────────

    public class CoachDashboardResponse
    {
        public string CoachName { get; set; } = null!;
        public int ClassesTaughtThisMonth { get; set; }
        public int ClassesUpcoming { get; set; }
        public int ValidationsPending { get; set; }
        public List<CoachUpcomingClass> UpcomingClasses { get; set; } = new();
    }

    public class CoachUpcomingClass
    {
        public int ClassId { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string ModalityName { get; set; } = null!;
        public string StudioName { get; set; } = null!;
        public List<string> StudentNames { get; set; } = new();
        public CoachClassStatus Status { get; set; }
    }

    // ─── Validate / requests tab ──────────────────────────────────────────────

    public class CoachPendingRequestItem
    {
        public int ClassId { get; set; }
        public DateOnly RequestedAt { get; set; }
        public List<string> StudentNames { get; set; } = new();
        public string ParentName { get; set; } = null!;
        public DateTime PreferredDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public int DurationMinutes { get; set; }
        public string StudioName { get; set; } = null!;
        public int MaxParticipants { get; set; }
        public string ModalityName { get; set; } = null!;
    }

    // ─── Validate / validations tab ───────────────────────────────────────────

    public class CoachValidationItem
    {
        public int ClassId { get; set; }
        public List<ParticipantSummaryItem> Participants { get; set; } = new();
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string StudioName { get; set; } = null!;
        public string ModalityName { get; set; } = null!;
        public int MaxParticipants { get; set; }
        public DateTime ExpiresAt { get; set; }             // StartDatetime + 48h, computed
        public byte CoachValidationStatus { get; set; }
    }
}
