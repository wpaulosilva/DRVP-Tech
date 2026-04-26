using System;
using System.Collections.Generic;

namespace DanceSchoolApp.Server.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateOnly CreatedAt { get; set; }

    public string? Email { get; set; }

    public int? PersonInfoId { get; set; }

    public virtual Coach? Coach { get; set; }

    public virtual ICollection<CoachClass> CoachClasses { get; set; } = new List<CoachClass>();

    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    public virtual ICollection<ItemRequisition> ItemRequisitions { get; set; } = new List<ItemRequisition>();

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual PersonInfo? PersonInfo { get; set; }

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();

    public virtual ICollection<Role> IdRoles { get; set; } = new List<Role>();
}
