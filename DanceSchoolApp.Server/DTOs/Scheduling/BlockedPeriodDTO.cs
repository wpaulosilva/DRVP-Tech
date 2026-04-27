using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.Scheduling
{
    //  Scope enum 
    // Matches the byte values stored in BlockedPeriod.Scope.
    public enum BlockedPeriodScope : byte
    {
        Undefined = 0,
        NormalClass = 1,  // Regular school classes occupy studios — no extra bookings
        Studio = 2,  // Specific studio is blocked
        Coach = 3,  // Specific coach is unavailable
        Event = 4,  // School event — may block everything
        Holiday = 5   // School break / public holiday
    }

    //  Responses 

    public class BlockedPeriodListResponse
    {
        public int BlockedId { get; set; }
        public BlockedPeriodScope Scope { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string? Reason { get; set; }

        // Null unless Scope = Studio
        public int? StudioId { get; set; }
        public string? StudioName { get; set; }

        // Null unless Scope = Coach
        public int? CoachId { get; set; }
        public string? CoachName { get; set; }
    }

    public class BlockedPeriodDetailResponse
    {
        public int BlockedId { get; set; }
        public BlockedPeriodScope Scope { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string? Reason { get; set; }
        public int? StudioId { get; set; }
        public string? StudioName { get; set; }
        public int? CoachId { get; set; }
        public string? CoachName { get; set; }
    }

    //  Requests 

    public class BlockedPeriodCreateRequest : IValidatableObject
    {
        [Required]
        public BlockedPeriodScope Scope { get; set; }

        [Required]
        public DateTime StartDatetime { get; set; }

        [Required]
        public DateTime EndDatetime { get; set; }

        [MaxLength(128)]
        public string? Reason { get; set; }

        // Required when Scope = Studio
        public int? StudioId { get; set; }

        // Required when Scope = Coach
        public int? CoachId { get; set; }

        // IValidatableObject lets us express cross-field rules that
        // [Required] and [Range] attributes cannot handle on their own.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDatetime <= StartDatetime)
                yield return new ValidationResult(
                    "EndDatetime must be after StartDatetime.",
                    new[] { nameof(EndDatetime) });

            if (Scope == BlockedPeriodScope.Studio && StudioId is null)
                yield return new ValidationResult(
                    "StudioId is required when Scope is Studio.",
                    new[] { nameof(StudioId) });

            if (Scope == BlockedPeriodScope.Coach && CoachId is null)
                yield return new ValidationResult(
                    "CoachId is required when Scope is Coach.",
                    new[] { nameof(CoachId) });

            if (Scope != BlockedPeriodScope.Studio && StudioId is not null)
                yield return new ValidationResult(
                    "StudioId should only be provided when Scope is Studio.",
                    new[] { nameof(StudioId) });

            if (Scope != BlockedPeriodScope.Coach && CoachId is not null)
                yield return new ValidationResult(
                    "CoachId should only be provided when Scope is Coach.",
                    new[] { nameof(CoachId) });
        }
    }

    public class BlockedPeriodUpdateRequest : IValidatableObject
    {
        [Required]
        public BlockedPeriodScope Scope { get; set; }

        [Required]
        public DateTime StartDatetime { get; set; }

        [Required]
        public DateTime EndDatetime { get; set; }

        [MaxLength(128)]
        public string? Reason { get; set; }

        public int? StudioId { get; set; }
        public int? CoachId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDatetime <= StartDatetime)
                yield return new ValidationResult(
                    "EndDatetime must be after StartDatetime.",
                    new[] { nameof(EndDatetime) });

            if (Scope == BlockedPeriodScope.Studio && StudioId is null)
                yield return new ValidationResult(
                    "StudioId is required when Scope is Studio.",
                    new[] { nameof(StudioId) });

            if (Scope == BlockedPeriodScope.Coach && CoachId is null)
                yield return new ValidationResult(
                    "CoachId is required when Scope is Coach.",
                    new[] { nameof(CoachId) });

            if (Scope != BlockedPeriodScope.Studio && StudioId is not null)
                yield return new ValidationResult(
                    "StudioId should only be provided when Scope is Studio.",
                    new[] { nameof(StudioId) });

            if (Scope != BlockedPeriodScope.Coach && CoachId is not null)
                yield return new ValidationResult(
                    "CoachId should only be provided when Scope is Coach.",
                    new[] { nameof(CoachId) });
        }
    }

    // Used for GET /api/blockedperiods/range?from=&to=
    public class BlockedPeriodRangeQuery
    {
        [Required]
        public DateTime From { get; set; }

        [Required]
        public DateTime To { get; set; }
    }
}
