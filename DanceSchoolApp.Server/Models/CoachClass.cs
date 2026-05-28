using System;
using System.Collections.Generic;

namespace DanceSchoolApp.Server.Models;

public partial class CoachClass
{
    public int ClassId { get; set; }

    public int IdModality { get; set; }

    public int IdStudio { get; set; }

    public int IdCoach { get; set; }

    public int CreatedBy { get; set; }

    public DateTime StartDatetime { get; set; }

    public DateTime EndDatetime { get; set; }

    public byte Status { get; set; }

    // 0 = ParentCreated (individual), 1 = CoachCreated (individual or group)
    public byte ClassOrigin { get; set; }

    public int MaxParticipants { get; set; }

    public DateOnly CreatedAt { get; set; }

    public byte CoachValidationStatus { get; set; }

    public DateTime? FinishedAt { get; set; }

    public DateTime? CoachValidatedAt { get; set; }

    public DateTime? StaffValidatedAt { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Coach IdCoachNavigation { get; set; } = null!;

    public virtual Modality IdModalityNavigation { get; set; } = null!;

    public virtual Studio IdStudioNavigation { get; set; } = null!;

    public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();
}
