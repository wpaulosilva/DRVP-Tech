using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.Classes
{
    //  Coach validation status enum 
    public enum CoachValidationStatus : byte
    {
        Pending   = 0,
        Confirmed = 1,
        Denied    = 2
    }

    //  Status enum 
    public enum CoachClassStatus : byte
    {
        Requested     = 0,
        Approved      = 1,  // coach accepted
        Rejected      = 2,
        Cancelled     = 3,
        Finished      = 4,
        Validated     = 5,
        Pending       = 6,  // awaiting staff final sign-off
        StaffApproved = 7   // staff approved, awaiting coach acceptance
    }

    //  Responses 

    public class CoachClassListResponse
    {
        public int ClassId { get; set; }
        public CoachClassStatus Status { get; set; }
        public CoachValidationStatus CoachValidationStatus { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string ModalityName { get; set; } = null!;
        public string StudioName { get; set; } = null!;
        public string CoachName { get; set; } = null!;
        public int MaxParticipants { get; set; }
        public int CurrentParticipants { get; set; }
        public DateOnly CreatedAt { get; set; }
    }

    public class CoachClassDetailResponse
    {
        public int ClassId { get; set; }
        public CoachClassStatus Status { get; set; }
        public CoachValidationStatus CoachValidationStatus { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }

        public int ModalityId { get; set; }
        public string ModalityName { get; set; } = null!;

        public int StudioId { get; set; }
        public string StudioName { get; set; } = null!;

        public int CoachId { get; set; }
        public string CoachName { get; set; } = null!;

        public int CreatedByUserId { get; set; }

        public int MaxParticipants { get; set; }
        public int CurrentParticipants { get; set; }
        public DateOnly CreatedAt { get; set; }

        public DateTime? CoachValidatedAt { get; set; }
        public DateTime? StaffValidatedAt { get; set; }

        public List<ClassParticipantSummary> Participants { get; set; } = new();
    }

    // Slim participant view embedded in class detail — full participant
    // detail belongs in the Participant controller.
    public class ClassParticipantSummary
    {
        public int ParticipantId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = null!;
        public DateOnly JoinedAt { get; set; }
        public byte ValidationStatus { get; set; }
    }

    // Lightweight participant entry for portal validation views.
    // ParentName is only populated in the staff view.
    public class ParticipantSummaryItem
    {
        public int ParticipantId { get; set; }
        public string StudentName { get; set; } = null!;
        public byte ValidationStatus { get; set; }
        public string? ParentName { get; set; }
    }

    // Used by GET /open — only shows what a parent needs to decide to join.
    public class OpenClassResponse
    {
        public int ClassId { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string ModalityName { get; set; } = null!;
        public string StudioName { get; set; } = null!;
        public string CoachName { get; set; } = null!;
        public int MaxParticipants { get; set; }
        public int SpotsAvailable { get; set; }
    }

    //  Requests 

    public class CoachClassCreateRequest : IValidatableObject
    {
        [Required]
        public int ModalityId { get; set; }

        [Required]
        public int CoachId { get; set; }

        [Required]
        public DateTime StartDatetime { get; set; }

        [Required]
        public DateTime EndDatetime { get; set; }

        [Required]
        [Range(1, 8, ErrorMessage = "MaxParticipants must be between 1 and 8.")]
        public int MaxParticipants { get; set; }

        // Must contain at least 1 student — a class cannot be created empty.
        // Size is validated against MaxParticipants in IValidatableObject.
        [Required]
        [MinLength(1, ErrorMessage = "At least one student is required to create a class.")]
        public List<int> StudentIds { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDatetime <= StartDatetime)
                yield return new ValidationResult(
                    "EndDatetime must be after StartDatetime.",
                    new[] { nameof(EndDatetime) });

            if (StartDatetime < DateTime.Now)
                yield return new ValidationResult(
                    "StartDatetime cannot be in the past.",
                    new[] { nameof(StartDatetime) });

            if (StudentIds.Count > MaxParticipants)
                yield return new ValidationResult(
                    $"Number of students ({StudentIds.Count}) exceeds MaxParticipants ({MaxParticipants}).",
                    new[] { nameof(StudentIds) });

            if (StudentIds.Distinct().Count() != StudentIds.Count)
                yield return new ValidationResult(
                    "StudentIds contains duplicates.",
                    new[] { nameof(StudentIds) });
        }
    }
    public class StaffRespondRequest
    {
        [Required]
        public bool Approve { get; set; }
        public string? Reason { get; set; }
    }

    public class CoachRespondRequest
    {
        [Required]
        public bool Accept { get; set; }
        public string? Reason { get; set; }
    }


    public class CoachValidateRequest
    {
        [Required]
        public bool DidTeach { get; set; }
    }

    public class StaffValidateRequest
    {
        [Required]
        public bool Confirmed { get; set; }

        public string? Reason { get; set; }
    }
}
