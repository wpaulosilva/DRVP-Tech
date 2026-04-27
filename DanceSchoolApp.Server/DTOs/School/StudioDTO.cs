using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.School
{
    //  Responses 

    public class StudioListResponse
    {
        public int StudioId { get; set; }
        public string Name { get; set; } = null!;
        public int Capacity { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public int ModalityCount { get; set; }
    }

    public class StudioDetailResponse
    {
        public int StudioId { get; set; }
        public string Name { get; set; } = null!;
        public int Capacity { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public List<ModalitySummaryResponse> Modalities { get; set; } = new();
    }

    public class ModalitySummaryResponse
    {
        public int ModalityId { get; set; }
        public string Name { get; set; } = null!;
    }

    //  Requests 

    public class StudioCreateRequest
    {
        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = null!;

        [Required]
        [Range(1, 1000, ErrorMessage = "Capacity must be between 1 and 1000.")]
        public int Capacity { get; set; }

        [MaxLength(256)]
        public string? Address { get; set; }

        public List<int> ModalityIds { get; set; } = new();
    }

    public class StudioUpdateRequest
    {
        [Required]
        [MaxLength(64)]
        public string Name { get; set; } = null!;

        [Required]
        [Range(1, 1000, ErrorMessage = "Capacity must be between 1 and 1000.")]
        public int Capacity { get; set; }

        [MaxLength(256)]
        public string? Address { get; set; }

        public List<int> ModalityIds { get; set; } = new();
    }

}
