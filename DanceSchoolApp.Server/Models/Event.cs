using System;
using System.Collections.Generic;

namespace DanceSchoolApp.Server.Models;

public partial class Event
{
    public int EventId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? SecretDescription { get; set; }

    public DateTime? StartDatetime { get; set; }

    public DateTime? EndDatetime { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; }

    public int? CreatedBy { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<Modality> IdModalities { get; set; } = new List<Modality>();

    public virtual ICollection<Coach> IdCoaches { get; set; } = new List<Coach>();
}
