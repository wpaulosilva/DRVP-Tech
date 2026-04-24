using System.ComponentModel.DataAnnotations;
using DanceSchoolApp.Server.DTOs.Classes;

namespace DanceSchoolApp.Server.DTOs.People
{
    public enum StudentAcceptanceStatus : byte
    {
        Pending  = 0,
        Accepted = 1,
        Rejected = 2
    }

    // ─── Responses ────────────────────────────────────────────────────────────

    public class StudentListResponse
    {
        public int StudentId { get; set; }
        public int ParentUserId { get; set; }
        public bool IsActive { get; set; }
        public StudentAcceptanceStatus AcceptanceStatus { get; set; }
        public PersonListResponse? PersonInfo { get; set; }
    }

    public class StudentDetailResponse
    {
        public int StudentId { get; set; }
        public int ParentUserId { get; set; }
        public bool IsActive { get; set; }
        public StudentAcceptanceStatus AcceptanceStatus { get; set; }
        public PersonDetailResponse? PersonInfo { get; set; }
    }


    // ─── Requests ─────────────────────────────────────────────────────────────

    public class StudentCreateRequest
    {
        public int ParentId { get; set; }

        [Required]
        [MaxLength(64)]
        public string FirstName { get; set; } = null!;

        [Required]
        [MaxLength(64)]
        public string LastName { get; set; } = null!;

        public DateOnly? BirthDate { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(128)]
        public string? Address { get; set; }
        [Required]

        [MaxLength(9)]
        public string Nif { get; set; }
    }

    public class StudentUpdateRequest
    {
        [Required]
        [MaxLength(64)]
        public string FirstName { get; set; } = null!;

        [Required]
        [MaxLength(64)]
        public string LastName { get; set; } = null!;

        public DateOnly? BirthDate { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(128)]
        public string? Address { get; set; }
        [Required]
        [MaxLength(9)]
        public string Nif { get; set; }
    }

    public class StudentAcceptanceRejectRequest
    {
        [MaxLength(256)]
        public string? Reason { get; set; }
    }

    public class StudentClassHistoryResponse
    {
        public int ParticipantId { get; set; }
        public int ClassId { get; set; }
        public CoachClassStatus Status { get; set; }
        public DateTime StartDatetime { get; set; }
        public DateTime EndDatetime { get; set; }
        public string ModalityName { get; set; } = null!;
        public string StudioName { get; set; } = null!;
        public string CoachName { get; set; } = null!;
        public DateOnly JoinedAt { get; set; }
        public ParticipantValidationStatus ValidationStatus { get; set; }
        public DateTime? ParentValidatedAt { get; set; }
        public decimal ClassPrice { get; set; }
    }

}
