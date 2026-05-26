using System;
using System.Collections.Generic;

namespace DanceSchoolApp.Server.Models;

public partial class Student
{
    public int StudentId { get; set; }

    public int ParentUserId { get; set; }

    public int PersonInfoId { get; set; }

    public bool IsActive { get; set; }

    public byte AcceptanceStatus { get; set; }

    public virtual User ParentUser { get; set; } = null!;

    public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();

    public virtual PersonInfo PersonInfo { get; set; } = null!;

    public virtual ICollection<Modality> IdModalities { get; set; } = new List<Modality>();
}
