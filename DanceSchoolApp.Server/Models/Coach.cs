using System;
using System.Collections.Generic;

namespace DanceSchoolApp.Server.Models;

public partial class Coach
{
    public int CoachId { get; set; }

    public string? Biography { get; set; }

    public virtual ICollection<BlockedPeriod> BlockedPeriods { get; set; } = new List<BlockedPeriod>();

    public virtual ICollection<CoachAvailability> CoachAvailabilities { get; set; } = new List<CoachAvailability>();

    public virtual ICollection<CoachClass> CoachClasses { get; set; } = new List<CoachClass>();

    public virtual User CoachNavigation { get; set; } = null!;

    public virtual ICollection<Modality> IdModalities { get; set; } = new List<Modality>();

    public virtual ICollection<Event> IdEvents { get; set; } = new List<Event>();
}
