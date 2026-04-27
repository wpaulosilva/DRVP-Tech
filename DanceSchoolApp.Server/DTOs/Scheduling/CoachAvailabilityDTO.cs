using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.Scheduling
{
    public enum Weekday : byte
    {
        Sunday = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 3,
        Thursday = 4,
        Friday = 5,
        Saturday = 6
    }

    //  Responses 

    public class CoachAvailabilityListResponse
    {
        public int CoachAvId { get; set; }
        public int CoachId { get; set; }
        public Weekday Weekday { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidUntil { get; set; }
    }

    // Detail response is the same shape for now — kept separate so it can
    // diverge later (e.g. include coach name, blocked period overlaps, etc.)
    public class CoachAvailabilityDetailResponse
    {
        public int CoachAvId { get; set; }
        public int CoachId { get; set; }
        public string CoachName { get; set; } = null!;  
        public Weekday Weekday { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidUntil { get; set; }
    }

    //  Requests 

    public class CoachAvailabilityCreateRequest
    {
        [Required]
        public int CoachId { get; set; }

        [Required]
        [Range(0, 6, ErrorMessage = "Weekday must be between 0 (Sunday) and 6 (Saturday).")]
        public byte Weekday { get; set; }

        [Required]
        public TimeOnly StartTime { get; set; }

        [Required]
        public TimeOnly EndTime { get; set; }

        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidUntil { get; set; }
    }

    public class CoachAvailabilityUpdateRequest
    {
        [Required]
        [Range(0, 6, ErrorMessage = "Weekday must be between 0 (Sunday) and 6 (Saturday).")]
        public byte Weekday { get; set; }

        [Required]
        public TimeOnly StartTime { get; set; }

        [Required]
        public TimeOnly EndTime { get; set; }

        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidUntil { get; set; }
    }
}
