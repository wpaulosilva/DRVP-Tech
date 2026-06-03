using System;
using System.Collections.Generic;

namespace DanceSchoolApp.Server.Models;

public partial class Participant
{
    public int ParticipantId { get; set; }

    public int IdCoachClass { get; set; }

    public int IdStudent { get; set; }

    public DateOnly JoinedAt { get; set; }

    public DateTime? ParentValidatedAt { get; set; }

    public byte ValidationStatus { get; set; }

    // Enrollment approval — only meaningful when coach_class.class_origin = 1 (coach-created).
    // 0=NotRequired, 1=Pending, 2=Approved, 3=Rejected
    public byte ParentEnrollmentStatus { get; set; }

    public DateTime? ParentEnrollmentAt { get; set; }

    // Price snapshot assigned at enrolment from app settings (weekday/weekend rate).
    // Staff can override per participant at approval/validation time.
    // Null on legacy rows — billing falls back to app settings rates.
    public decimal? PerParticipantPrice { get; set; }

    public virtual CoachClass IdCoachClassNavigation { get; set; } = null!;

    public virtual Student IdStudentNavigation { get; set; } = null!;
}
