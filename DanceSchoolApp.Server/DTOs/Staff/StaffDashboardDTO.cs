using DanceSchoolApp.Server.DTOs.Classes;


namespace DanceSchoolApp.Server.DTOs.Staff
{
    // ─── Dashboard ────────────────────────────────────────────────────────────

    public class StaffDashboardResponse
    {
        public int TotalParents { get; set; }
        public int TotalCoaches { get; set; }
        public int TotalActiveStudents { get; set; }
        public int TotalActiveEvents { get; set; }
        public int ClassesScheduledThisMonth { get; set; }
        public int ClassesCompletedThisMonth { get; set; }
        public int ClassesPendingValidation { get; set; }
        public List<EventSummary> UpcomingEvents { get; set; } = new();
    }

    public class EventSummary
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public DateTime? StartDatetime { get; set; }
        public DateTime? EndDatetime { get; set; }
    }

    // ─── Agenda ───────────────────────────────────────────────────────────────

    public class AgendaClassItem
    {
        public int ClassId { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string ModalityName { get; set; } = null!;
        public string StudioName { get; set; } = null!;
        public string CoachName { get; set; } = null!;
        public List<string> StudentNames { get; set; } = new();
        public CoachClassStatus Status { get; set; }
        public int CurrentParticipants { get; set; }
        public int MaxParticipants { get; set; }
    }

    // ─── Validate classes ─────────────────────────────────────────────────────

    public class ValidateClassItem
    {
        public int ClassId { get; set; }
        public string ModalityName { get; set; } = null!;
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public int DurationMinutes { get; set; }
        public string CoachName { get; set; } = null!;
        public int MaxParticipants { get; set; } = 1;
        public string ParentName { get; set; } = null!;
        public byte CoachValidationStatus { get; set; }
        public ParticipantSummaryDto ParticipantSummary { get; set; } = null!;
        // Full per-participant list with individual validation status and parent name
        public List<ParticipantSummaryItem> Participants { get; set; } = new();
    }

    public class ParticipantSummaryDto
    {
        public int Total { get; set; }
        public int Confirmed { get; set; }
        public int Disputed { get; set; }
        public int Pending { get; set; }
    }

    // ─── Validate students ────────────────────────────────────────────────────

    public class ValidateStudentItem
    {
        public int StudentId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateOnly? BirthDate { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Nif { get; set; }          // last 2 digits only; rest masked
        public byte AcceptanceStatus { get; set; }
        public DateOnly SubmittedAt { get; set; }  // TODO: Student has no created_at column — always DateOnly.MinValue
        public string? ParentName { get; set; }
        public string? ParentEmail { get; set; }
    }
}
