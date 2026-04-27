using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.School
{
    //  Responses 

    public class ModalityListResponse
    {
        public int ModalityId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class ModalityDetailResponse
    {
        public int ModalityId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int StudioCount { get; set; }
        public int CoachCount { get; set; }
    }

    //  Requests 

    public class ModalityCreateRequest
    {
        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = null!;

        [MaxLength(256)]
        public string? Description { get; set; }
    }

    public class ModalityUpdateRequest
    {
        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = null!;

        [MaxLength(256)]
        public string? Description { get; set; }
    }
}
