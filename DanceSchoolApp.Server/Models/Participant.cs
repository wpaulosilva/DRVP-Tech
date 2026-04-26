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

    public virtual CoachClass IdCoachClassNavigation { get; set; } = null!;

    public virtual Student IdStudentNavigation { get; set; } = null!;
}
