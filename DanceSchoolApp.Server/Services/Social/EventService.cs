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
            return await _context.Events
                .Include(e => e.CreatedByNavigation)
                    .ThenInclude(u => u!.PersonInfo)
                .Select(e => new EventListResponse
                {
                    EventId = e.EventId,
                    Title = e.Title,
                    Description = e.Description,
                    StartDatetime = e.StartDatetime,
                    EndDatetime = e.EndDatetime,
                    ImageUrl = e.ImageUrl,
                    IsActive = e.IsActive,
                    CreatedByName = ResolveAuthorName(e.CreatedByNavigation)
                })
                .ToListAsync();
        }

        public async Task<List<EventListResponse>> GetActiveAsync()
        {
            return await _context.Events
                .Include(e => e.CreatedByNavigation)
                    .ThenInclude(u => u!.PersonInfo)
                .Where(e => e.IsActive)
                .Select(e => new EventListResponse
                {
                    EventId = e.EventId,
                    Title = e.Title,
                    Description = e.Description,
                    StartDatetime = e.StartDatetime,
                    EndDatetime = e.EndDatetime,
                    ImageUrl = e.ImageUrl,
                    IsActive = e.IsActive,
                    CreatedByName = ResolveAuthorName(e.CreatedByNavigation)
                })
                .ToListAsync();
        }

        public async Task<EventDetailResponse> GetByIdAsync(int id)
        {
            var ev = await _context.Events
                .Include(e => e.CreatedByNavigation)
                    .ThenInclude(u => u!.PersonInfo)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev is null)
                throw new KeyNotFoundException($"Event with id {id} was not found.");

            return new EventDetailResponse
            {
                EventId = ev.EventId,
                Title = ev.Title,
                Description = ev.Description,
                StartDatetime = ev.StartDatetime,
                EndDatetime = ev.EndDatetime,
                ImageUrl = ev.ImageUrl,
                IsActive = ev.IsActive,
                CreatedByUserId = ev.CreatedBy,
                CreatedByName = ResolveAuthorName(ev.CreatedByNavigation)
            };
        }

        //  Commands 

        public async Task<int> CreateAsync(EventCreateRequest request, int createdByUserId)
        {
            bool userExists = await _context.Users
                .AnyAsync(u => u.UserId == createdByUserId);

            if (!userExists)
                throw new KeyNotFoundException(
                    $"User with id {createdByUserId} was not found.");

            var ev = new Event
            {
                Title = request.Title,
                Description = request.Description,
                StartDatetime = request.StartDatetime,
                EndDatetime = request.EndDatetime,
                ImageUrl = request.ImageUrl,
                IsActive = true,
                CreatedBy = createdByUserId
            };

            _context.Events.Add(ev);
            await _context.SaveChangesAsync();

            return ev.EventId;
        }

        public async Task UpdateAsync(int id, EventUpdateRequest request)
        {
            var ev = await _context.Events
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev is null)
                throw new KeyNotFoundException($"Event with id {id} was not found.");

            ev.Title = request.Title;
            ev.Description = request.Description;
            ev.StartDatetime = request.StartDatetime;
            ev.EndDatetime = request.EndDatetime;
            ev.ImageUrl = request.ImageUrl;

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
    }
}