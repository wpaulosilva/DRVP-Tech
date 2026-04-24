using System;
using System.Collections.Generic;

namespace DanceSchoolApp.Server.Models;

public partial class PersonInfo
{
    public int PersonId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public DateOnly BirthDate { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string Nif { get; set; } = null!;

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
