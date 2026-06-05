using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.Social
{
    //  Responses

    public class EventModalitySummary
    {
        public int ModalityId { get; set; }
        public string Name { get; set; } = null!;
    }

    public class EventCoachSummary
    {
        public int CoachId { get; set; }
        public string Name { get; set; } = null!;
    }

    public class EventListResponse
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        // Null when caller is not authorised to read it.
        public string? SecretDescription { get; set; }
        public DateTime? StartDatetime { get; set; }
        public DateTime? EndDatetime { get; set; }
        public bool IsActive { get; set; }
        public string? CreatedByName { get; set; }
        public List<EventModalitySummary> Modalities { get; set; } = new();
        public List<EventCoachSummary> Coaches { get; set; } = new();
    }

    public class EventDetailResponse
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        // Null when caller is not authorised to read it.
        public string? SecretDescription { get; set; }
        public DateTime? StartDatetime { get; set; }
        public DateTime? EndDatetime { get; set; }
        public bool IsActive { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public List<EventModalitySummary> Modalities { get; set; } = new();
        public List<EventCoachSummary> Coaches { get; set; } = new();
    }

    //  Requests

    public class EventCreateRequest : IValidatableObject
    {
        [Required]
        [MaxLength(64)]
        public string Title { get; set; } = null!;

        [MaxLength(256)]
        public string? Description { get; set; }

        // Enforced as required on create even though the model allows null —
        // a published event should always have dates.
        [Required]
        public DateTime StartDatetime { get; set; }

        [Required]
        public DateTime EndDatetime { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one modality is required.")]
        public List<int> ModalityIds { get; set; } = new();

        public List<int> CoachIds { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDatetime <= StartDatetime)
                yield return new ValidationResult(
                    "EndDatetime must be after StartDatetime.",
                    new[] { nameof(EndDatetime) });
        }
    }

    public class EventUpdateRequest : IValidatableObject
    {
        [Required]
        [MaxLength(64)]
        public string Title { get; set; } = null!;

        [MaxLength(256)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartDatetime { get; set; }

        [Required]
        public DateTime EndDatetime { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one modality is required.")]
        public List<int> ModalityIds { get; set; } = new();

        public List<int> CoachIds { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDatetime <= StartDatetime)
                yield return new ValidationResult(
                    "EndDatetime must be after StartDatetime.",
                    new[] { nameof(EndDatetime) });
        }
    }

    // Coach-only: update the secret description of an event they are assigned to.
    public class EventSecretDescriptionRequest
    {
        public string? SecretDescription { get; set; }
    }
}