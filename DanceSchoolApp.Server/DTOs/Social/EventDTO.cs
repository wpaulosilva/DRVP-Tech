using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.Social
{
    //  Responses 

    public class EventListResponse
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime? StartDatetime { get; set; }
        public DateTime? EndDatetime { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public string? CreatedByName { get; set; }
    }

    public class EventDetailResponse
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime? StartDatetime { get; set; }
        public DateTime? EndDatetime { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
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

        [MaxLength(256)]
        public string? ImageUrl { get; set; }

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

        [MaxLength(256)]
        public string? ImageUrl { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDatetime <= StartDatetime)
                yield return new ValidationResult(
                    "EndDatetime must be after StartDatetime.",
                    new[] { nameof(EndDatetime) });
        }
    }
}