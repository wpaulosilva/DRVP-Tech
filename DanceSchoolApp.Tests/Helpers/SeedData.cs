using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.Models;

namespace DanceSchoolApp.Tests.Helpers;

public static class SeedData
{
    public static void SeedRoles(AppDbContext db)
    {
        if (db.Roles.Any()) return;

        db.Roles.AddRange(
            new Role { RoleId = 0, RoleName = "admin" },
            new Role { RoleId = 1, RoleName = "staff" },
            new Role { RoleId = 2, RoleName = "coach" },
            new Role { RoleId = 3, RoleName = "parent" }
        );
        db.SaveChanges();
    }

    public static void SeedAppSettings(AppDbContext db)
    {
        if (db.AppSettings.Any()) return;

        db.AppSettings.AddRange(
            new AppSetting { SettingKey = "class_price_weekday", SettingValue = "36.00" },
            new AppSetting { SettingKey = "class_price_weekend", SettingValue = "43.20" }
        );
        db.SaveChanges();
    }

    public static Modality SeedModality(AppDbContext db, string name = "Ballet Clássico")
    {
        var modality = new Modality { Name = name, IsActive = true };
        db.Modalities.Add(modality);
        db.SaveChanges();
        return modality;
    }

    public static Studio SeedStudio(AppDbContext db, string name = "Estúdio A")
    {
        var studio = new Studio { Name = name, Capacity = 10, IsActive = true };
        db.Studios.Add(studio);
        db.SaveChanges();
        return studio;
    }

    public static User SeedUserWithRole(AppDbContext db, string username, string roleName)
    {
        var role = db.Roles.Single(r => r.RoleName == roleName);

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234!"),
            IsActive = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        user.IdRoles.Add(role);
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static Coach SeedCoach(AppDbContext db, User user)
    {
        var coach = new Coach { CoachId = user.UserId };
        db.Coaches.Add(coach);
        db.SaveChanges();
        return coach;
    }

    public static Student SeedStudent(AppDbContext db, User parentUser, string firstName = "Ana")
    {
        var personInfo = new PersonInfo { FirstName = firstName, LastName = "Silva" , Nif = "384093574" };
        db.PersonInfos.Add(personInfo);
        db.SaveChanges();

        var student = new Student
        {
            ParentUserId = parentUser.UserId,
            PersonInfoId = personInfo.PersonId,
            IsActive = true,
            AcceptanceStatus = 1,
        };
        db.Students.Add(student);
        db.SaveChanges();
        return student;
    }

    public static CoachAvailability SeedCoachAvailability(
        AppDbContext db,
        Coach coach,
        byte weekday,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly? validFrom  = null,
        DateOnly? validUntil = null)
    {
        var av = new CoachAvailability
        {
            IdCoach    = coach.CoachId,
            Weekday    = weekday,
            StartTime  = startTime,
            EndTime    = endTime,
            ValidFrom  = validFrom,
            ValidUntil = validUntil
        };
        db.CoachAvailabilities.Add(av);
        db.SaveChanges();
        return av;
    }

    public static CoachClass SeedCoachClass(
        AppDbContext db,
        Coach coach,
        Modality modality,
        Studio studio,
        User createdByUser,
        DateTime? start = null,
        int durationMinutes = 60,
        byte status = 0)
    {
        if (start is null)
        {
            var today = DateTime.UtcNow.Date;
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            start = today.AddDays(daysUntilMonday).AddHours(10);
        }

        var coachClass = new CoachClass
        {
            IdCoach = coach.CoachId,
            IdModality = modality.ModalityId,
            IdStudio = studio.StudioId,
            CreatedBy = createdByUser.UserId,
            StartDatetime = start.Value,
            EndDatetime = start.Value.AddMinutes(durationMinutes),
            Status = status,
            MaxParticipants = 10,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.CoachClasses.Add(coachClass);
        db.SaveChanges();
        return coachClass;
    }
}
