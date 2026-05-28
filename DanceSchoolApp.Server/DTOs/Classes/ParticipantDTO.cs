using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.Classes
{
    //  Validation status enum
    // Per-participant post-class attendance confirmation.
    public enum ParticipantValidationStatus : byte
    {
        Pending         = 0,
        ParentConfirmed = 1,
        Disputed        = 2
    }

    // Pre-class enrollment approval — only used when ClassOrigin = CoachCreated.
    // 0=NotRequired: used for parent-created classes (no separate approval needed).
    // 1=Pending: coach-created, waiting for parent response.
    // 2=Approved: parent approved the child's enrollment.
    // 3=Rejected: parent rejected the child's enrollment.
    public enum ParentEnrollmentStatus : byte
    {
        NotRequired = 0,
        Pending     = 1,
        Approved    = 2,
        Rejected    = 3
    }

    //  Responses

    public class ParticipantListResponse
    {
        public int ParticipantId { get; set; }
        public int ClassId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = null!;
        public int ParentUserId { get; set; }
        public DateOnly JoinedAt { get; set; }
        public ParticipantValidationStatus ValidationStatus { get; set; }
        public DateTime? ParentValidatedAt { get; set; }
        public ParentEnrollmentStatus ParentEnrollmentStatus { get; set; }
        public DateTime? ParentEnrollmentAt { get; set; }
    }

    //  Requests

    public class ParticipantJoinRequest
    {
        [Required]
        public int ClassId { get; set; }

        [Required]
        public int StudentId { get; set; }
    }

    public class ParticipantValidateRequest
    {
        [Required]
        public bool Attended { get; set; }
    }

    public class ParticipantEnrollmentApproveRequest
    {
        [Required]
        public bool Approve { get; set; }
    }
}
