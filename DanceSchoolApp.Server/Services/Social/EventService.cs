using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Social
{
    public class EventService
    {
        private readonly AppDbContext _context;

        public EventService(AppDbContext context)
        {
            _context = context;
        }

        //  Queries

        public async Task<List<EventListResponse>> GetAllAsync()
        {
            var events = await _context.Events
                .Include(e => e.CreatedByNavigation)
                    .ThenInclude(u => u!.PersonInfo)
                .Include(e => e.IdModalities)
                .Include(e => e.IdCoaches)
                    .ThenInclude(c => c.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .ToListAsync();

            return events.Select(e => MapToListResponse(e)).ToList();
        }

        public async Task<List<EventListResponse>> GetActiveAsync(string callerRole, int callerUserId)
        {
            var events = await _context.Events
                .Include(e => e.CreatedByNavigation)
                    .ThenInclude(u => u!.PersonInfo)
                .Include(e => e.IdModalities)
                .Include(e => e.IdCoaches)
                    .ThenInclude(c => c.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Where(e => e.IsActive)
                .ToListAsync();

            // Pre-compute the set of modality IDs the parent's students are enrolled in
            // so we only hit the DB once instead of once per event.
            HashSet<int>? parentModalityIds = null;
            if (callerRole == "parent")
            {
                parentModalityIds = (await _context.Students
                    .Where(s => s.ParentUserId == callerUserId && s.IsActive)
                    .SelectMany(s => s.IdModalities.Select(m => m.ModalityId))
                    .ToListAsync()).ToHashSet();
            }

            return events.Select(e =>
            {
                string? secret = null;

                if (callerRole == "staff")
                    secret = e.SecretDescription;
                else if (callerRole == "coach" && e.IdCoaches.Any(c => c.CoachId == callerUserId))
                    secret = e.SecretDescription;
                else if (callerRole == "parent" && parentModalityIds is not null
                    && e.IdModalities.Any(m => parentModalityIds.Contains(m.ModalityId)))
                    secret = e.SecretDescription;

                return MapToListResponse(e, secret);
            }).ToList();
        }

        // callerRole: "staff" | "coach" | "parent" | other
        // callerUserId: the user id of whoever is calling
        public async Task<EventDetailResponse> GetByIdAsync(int id, string callerRole, int callerUserId)
        {
            var ev = await _context.Events
                .Include(e => e.CreatedByNavigation)
                    .ThenInclude(u => u!.PersonInfo)
                .Include(e => e.IdModalities)
                .Include(e => e.IdCoaches)
                    .ThenInclude(c => c.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev is null)
                throw new KeyNotFoundException($"Event with id {id} was not found.");

            string? secretDescription = null;

            if (callerRole == "staff")
            {
                secretDescription = ev.SecretDescription;
            }
            else if (callerRole == "coach")
            {
                bool isAssigned = ev.IdCoaches.Any(c => c.CoachId == callerUserId);
                if (isAssigned)
                    secretDescription = ev.SecretDescription;
            }
            else if (callerRole == "parent")
            {
                var eventModalityIds = ev.IdModalities.Select(m => m.ModalityId).ToHashSet();

                bool hasMatchingStudent = await _context.Students
                    .Where(s => s.ParentUserId == callerUserId && s.IsActive)
                    .AnyAsync(s => s.IdModalities.Any(m => eventModalityIds.Contains(m.ModalityId)));

                if (hasMatchingStudent)
                    secretDescription = ev.SecretDescription;
            }

            return new EventDetailResponse
            {
                EventId = ev.EventId,
                Title = ev.Title,
                Description = ev.Description,
                SecretDescription = secretDescription,
                StartDatetime = ev.StartDatetime,
                EndDatetime = ev.EndDatetime,
                IsActive = ev.IsActive,
                CreatedByUserId = ev.CreatedBy,
                CreatedByName = ResolveAuthorName(ev.CreatedByNavigation),
                Modalities = ev.IdModalities.Select(m => new EventModalitySummary
                {
                    ModalityId = m.ModalityId,
                    Name = m.Name
                }).ToList(),
                Coaches = ev.IdCoaches.Select(c => new EventCoachSummary
                {
                    CoachId = c.CoachId,
                    Name = ResolveAuthorName(c.CoachNavigation) ?? c.CoachId.ToString()
                }).ToList()
            };
        }

        //  Commands

        public async Task<int> CreateAsync(EventCreateRequest request, int createdByUserId)
        {
            bool userExists = await _context.Users.AnyAsync(u => u.UserId == createdByUserId);
            if (!userExists)
                throw new KeyNotFoundException($"User with id {createdByUserId} was not found.");

            var modalities = await _context.Set<Modality>()
                .Where(m => request.ModalityIds.Contains(m.ModalityId))
                .ToListAsync();

            if (modalities.Count != request.ModalityIds.Distinct().Count())
                throw new KeyNotFoundException("One or more ModalityIds are invalid.");

            var coaches = request.CoachIds.Count > 0
                ? await _context.Set<Coach>()
                    .Where(c => request.CoachIds.Contains(c.CoachId))
                    .ToListAsync()
                : new List<Coach>();

            if (coaches.Count != request.CoachIds.Distinct().Count())
                throw new KeyNotFoundException("One or more CoachIds are invalid.");

            var ev = new Event
            {
                Title = request.Title,
                Description = request.Description,
                StartDatetime = request.StartDatetime,
                EndDatetime = request.EndDatetime,
                IsActive = true,
                CreatedBy = createdByUserId
            };

            ev.IdModalities = modalities;
            ev.IdCoaches = coaches;

            _context.Events.Add(ev);
            await _context.SaveChangesAsync();

            return ev.EventId;
        }

        public async Task UpdateAsync(int id, EventUpdateRequest request)
        {
            var ev = await _context.Events
                .Include(e => e.IdModalities)
                .Include(e => e.IdCoaches)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev is null)
                throw new KeyNotFoundException($"Event with id {id} was not found.");

            var modalities = await _context.Set<Modality>()
                .Where(m => request.ModalityIds.Contains(m.ModalityId))
                .ToListAsync();

            if (modalities.Count != request.ModalityIds.Distinct().Count())
                throw new KeyNotFoundException("One or more ModalityIds are invalid.");

            var coaches = request.CoachIds.Count > 0
                ? await _context.Set<Coach>()
                    .Where(c => request.CoachIds.Contains(c.CoachId))
                    .ToListAsync()
                : new List<Coach>();

            if (coaches.Count != request.CoachIds.Distinct().Count())
                throw new KeyNotFoundException("One or more CoachIds are invalid.");

            ev.Title = request.Title;
            ev.Description = request.Description;
            ev.StartDatetime = request.StartDatetime;
            ev.EndDatetime = request.EndDatetime;
            ev.IdModalities = modalities;
            ev.IdCoaches = coaches;

            await _context.SaveChangesAsync();
        }

        // Coach endpoint: update secret description only if coach is assigned to the event.
        public async Task UpdateSecretDescriptionAsync(int id, int coachUserId, string? secretDescription)
        {
            var ev = await _context.Events
                .Include(e => e.IdCoaches)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev is null)
                throw new KeyNotFoundException($"Event with id {id} was not found.");

            if (!ev.IdCoaches.Any(c => c.CoachId == coachUserId))
                throw new UnauthorizedAccessException("Coach is not assigned to this event.");

            ev.SecretDescription = secretDescription;
            await _context.SaveChangesAsync();
        }

        public async Task SetActiveStateAsync(int id, bool isActive)
        {
            var rowsAffected = await _context.Events
                .Where(e => e.EventId == id)
                .ExecuteUpdateAsync(e => e.SetProperty(x => x.IsActive, isActive));

            if (rowsAffected == 0)
                throw new KeyNotFoundException($"Event with id {id} was not found.");
        }

        public async Task DeleteAsync(int id)
        {
            var rowsAffected = await _context.Events
                .Where(e => e.EventId == id)
                .ExecuteDeleteAsync();

            if (rowsAffected == 0)
                throw new KeyNotFoundException($"Event with id {id} was not found.");
        }

        //  Private helpers

        private static string? ResolveAuthorName(User? user)
        {
            if (user is null) return null;
            var person = user.PersonInfo;
            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : user.Username;
        }

        private static EventListResponse MapToListResponse(Event e, string? secretDescription = null) => new()
        {
            EventId = e.EventId,
            Title = e.Title,
            Description = e.Description,
            SecretDescription = secretDescription,
            StartDatetime = e.StartDatetime,
            EndDatetime = e.EndDatetime,
            IsActive = e.IsActive,
            CreatedByName = ResolveAuthorName(e.CreatedByNavigation),
            Modalities = e.IdModalities.Select(m => new EventModalitySummary
            {
                ModalityId = m.ModalityId,
                Name = m.Name
            }).ToList(),
            Coaches = e.IdCoaches.Select(c => new EventCoachSummary
            {
                CoachId = c.CoachId,
                Name = ResolveAuthorName(c.CoachNavigation) ?? c.CoachId.ToString()
            }).ToList()
        };
    }
}