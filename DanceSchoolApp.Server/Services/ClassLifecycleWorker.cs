using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Classes;
using DanceSchoolApp.Server.Services.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DanceSchoolApp.Server.Services
{
    /// <summary>
    /// Background worker that runs every 5 minutes and handles two automated
    /// lifecycle transitions:
    ///
    ///   1. Approved → Finished: any Approved class whose EndDatetime has passed
    ///      and is not Cancelled is automatically marked Finished and the
    ///      48-hour dual-validation window is opened (notifications sent to
    ///      coach and all participant parents).
    ///
    ///   2. Finished → Pending: any Finished class whose FinishedAt is older
    ///      than the validation_window_hours AppSetting (default 48h) is
    ///      automatically advanced to Pending, even if some parties did not
    ///      respond. Staff are notified. This enforces the window deadline.
    ///
    /// Cancelled classes are never touched by either rule.
    /// </summary>
    public sealed class ClassLifecycleWorker : BackgroundService
    {
        private static readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ClassLifecycleWorker> _logger;

        public ClassLifecycleWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<ClassLifecycleWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_interval);

            // Run once immediately at startup, then every 5 minutes.
            do
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ClassLifecycleWorker encountered an error during cycle.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
            var appSettings = scope.ServiceProvider.GetRequiredService<AppSettingService>();

            // StartDatetime/EndDatetime are stored in server local time (as sent by the client).
            // Use DateTime.Now so the comparison matches the stored timezone.
            var now = DateTime.Now;

            await AutoFinishApprovedClassesAsync(db, notifications, now, ct);
            await AutoAdvanceExpiredFinishedClassesAsync(db, notifications, appSettings, now, ct);
        }

        //  Rule 1: Approved → Finished 
        // Picks up any Approved class whose EndDatetime <= now.
        // Cancelled classes are excluded by the status filter.
        private static async Task AutoFinishApprovedClassesAsync(
            AppDbContext db,
            NotificationService notifications,
            DateTime now,
            CancellationToken ct)
        {
            var classes = await db.CoachClasses
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                .Where(c =>
                    c.Status == (byte)CoachClassStatus.Approved &&
                    c.EndDatetime <= now)
                .ToListAsync(ct);

            if (classes.Count == 0) return;

            foreach (var cls in classes)
            {
                cls.Status = (byte)CoachClassStatus.Finished;
                cls.FinishedAt = now;
            }

            // Persist status changes before sending notifications so a notification
            // failure never leaves classes stuck in Approved indefinitely.
            await db.SaveChangesAsync(ct);

            foreach (var cls in classes)
            {
                await notifications.SendAsync(
                    userId: cls.IdCoach,
                    title: "Validação de Aula Necessária",
                    message: $"Por favor, confirme que lecionou a aula de {cls.StartDatetime:dd/MM/yyyy HH:mm}. Tem 48 horas para responder.",
                    type: NotificationType.ValidationRequest,
                    entityType: "CoachClass",
                    entityId: cls.ClassId);

                var parentIds = cls.Participants
                    .Select(p => p.IdStudentNavigation.ParentUserId)
                    .Distinct();

                foreach (var parentId in parentIds)
                {
                    await notifications.SendAsync(
                        userId: parentId,
                        title: "Validação de Aula Necessária",
                        message: $"Por favor, confirme a presença na aula de {cls.StartDatetime:dd/MM/yyyy HH:mm}. Tem 48 horas para responder.",
                        type: NotificationType.ValidationRequest,
                        entityType: "CoachClass",
                        entityId: cls.ClassId);
                }
            }
        }

        //  Rule 2: Finished → Pending (window expired) 
        // Any Finished class where FinishedAt + validation_window_hours <= now
        // is forced to Pending so staff can still do final sign-off.
        // Staff notifications include the names of everyone who failed to respond.
        private static async Task AutoAdvanceExpiredFinishedClassesAsync(
            AppDbContext db,
            NotificationService notifications,
            AppSettingService appSettings,
            DateTime now,
            CancellationToken ct)
        {
            int windowHours = await appSettings.GetIntAsync("validation_window_hours", 48);
            var cutoff = now.AddHours(-windowHours);

            var classes = await db.CoachClasses
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.ParentUser)
                            .ThenInclude(u => u.PersonInfo)
                .Where(c =>
                    c.Status == (byte)CoachClassStatus.Finished &&
                    c.FinishedAt != null &&
                    c.FinishedAt <= cutoff)
                .ToListAsync(ct);

            if (classes.Count == 0) return;

            var staffIds = await db.Users
                .Include(u => u.IdRoles)
                .Where(u => u.IdRoles.Any(r => r.RoleId == 1) && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync(ct);

            foreach (var cls in classes)
            {
                
                cls.Status = (byte)CoachClassStatus.Pending;

                // Build list of individuals who did not submit a validation response.
                var nonRespondents = new List<string>();

                if (cls.CoachValidationStatus == (byte)CoachValidationStatus.Pending)
                {
                    var coachPerson = cls.IdCoachNavigation?.CoachNavigation?.PersonInfo;
                    var coachName   = coachPerson is not null
                        ? $"{coachPerson.FirstName} {coachPerson.LastName}".Trim()
                        : cls.IdCoachNavigation?.CoachNavigation?.Username
                          ?? $"Coach #{cls.IdCoach}";
                    nonRespondents.Add($"Coach: {coachName}");
                }

                var silentParents = cls.Participants
                    .Where(p => p.ValidationStatus == 0)   // ParticipantValidationStatus.Pending
                    .Select(p => p.IdStudentNavigation?.ParentUser)
                    .Where(u => u is not null)
                    .DistinctBy(u => u!.UserId)
                    .ToList();

                foreach (var parent in silentParents)
                {
                    var pi = parent!.PersonInfo;
                    var name = pi is not null
                        ? $"{pi.FirstName} {pi.LastName}".Trim()
                        : parent.Username;
                    nonRespondents.Add($"EE: {name}");
                }

                var nonRespondersSummary = nonRespondents.Count > 0
                    ? $" Sem resposta: {string.Join(", ", nonRespondents)}."
                    : string.Empty;

                foreach (var staffId in staffIds)
                {
                    await notifications.SendAsync(
                        userId: staffId,
                        title: "Janela de Validação Expirada",
                        message: $"A janela de validação de 48 horas para a aula ID {cls.ClassId}" +
                                 $"(agendada para {cls.StartDatetime:dd/MM/yyyy HH:mm}) expirou." +
                                 nonRespondersSummary,
                        type: NotificationType.Warning,
                        entityType: "CoachClass",
                        entityId: cls.ClassId);
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
