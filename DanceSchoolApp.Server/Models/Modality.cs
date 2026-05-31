using System;
using System.Collections.Generic;

namespace DanceSchoolApp.Server.Models;

public partial class Modality
{
    public int ModalityId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<CoachClass> CoachClasses { get; set; } = new List<CoachClass>();

    public virtual ICollection<Coach> IdCoaches { get; set; } = new List<Coach>();

    public virtual ICollection<Event> IdEvents { get; set; } = new List<Event>();

    public virtual ICollection<Studio> IdStudios { get; set; } = new List<Studio>();

    public virtual ICollection<Student> IdStudents { get; set; } = new List<Student>();
}
