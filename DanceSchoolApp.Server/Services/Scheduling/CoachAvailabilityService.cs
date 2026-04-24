using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Scheduling;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Scheduling
{
    public class CoachAvailabilityService
    {
        private readonly AppDbContext _context;

        public CoachAvailabilityService(AppDbContext context)
        {
            _context = context;
        }

        // ─── Queries ──────────────────────────────────────────────────────────

        public async Task<List<CoachAvailabilityListResponse>> GetAllAsync()
        {
            return await _context.CoachAvailabilities
                .Select(a => new CoachAvailabilityListResponse
                {
                    CoachAvId = a.CoachavId,
                    CoachId = a.IdCoach,
                    Weekday = (Weekday)a.Weekday,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    ValidFrom = a.ValidFrom,
                    ValidUntil = a.ValidUntil
                })
                .ToListAsync();
        }

        public async Task<CoachAvailabilityDetailResponse> GetByIdAsync(int id)
        {
            var slot = await _context.CoachAvailabilities
                .Include(a => a.IdCoachNavigation)
                    .ThenInclude(c => c.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .FirstOrDefaultAsync(a => a.CoachavId == id);

            if (slot is null)
                throw new KeyNotFoundException($"Availability slot with id {id} was not found.");

            // Build a readable coach name from PersonInfo if available,
            // falling back to username — coach detail view needs this context.
            var personInfo = slot.IdCoachNavigation.CoachNavigation.PersonInfo;
            var coachName = personInfo is not null
                ? $"{personInfo.FirstName} {personInfo.LastName}".Trim()
                : slot.IdCoachNavigation.CoachNavigation.Username;

            return new CoachAvailabilityDetailResponse
            {
                CoachAvId = slot.CoachavId,
                CoachId = slot.IdCoach,
                CoachName = coachName,
                Weekday = (Weekday)slot.Weekday,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                ValidFrom = slot.ValidFrom,
                ValidUntil = slot.ValidUntil
            };
        }

        public async Task<List<CoachAvailabilityListResponse>> GetByCoachAsync(int coachId)
        {
            bool coachExists = await _context.Coaches
                .AnyAsync(c => c.CoachId == coachId);

            if (!coachExists)
                throw new KeyNotFoundException($"Coach with id {coachId} was not found.");

            return await _context.CoachAvailabilities
                .Where(a => a.IdCoach == coachId)
                .Select(a => new CoachAvailabilityListResponse
                {
                    CoachAvId = a.CoachavId,
                    CoachId = a.IdCoach,
                    Weekday = (Weekday)a.Weekday,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    ValidFrom = a.ValidFrom,
                    ValidUntil = a.ValidUntil
                })
                .ToListAsync();
        }

        // ─── Commands ─────────────────────────────────────────────────────────

        public async Task<int> CreateAsync(CoachAvailabilityCreateRequest request)
        {
            bool coachExists = await _context.Coaches
                .AnyAsync(c => c.CoachId == request.CoachId);

            if (!coachExists)
                throw new KeyNotFoundException($"Coach with id {request.CoachId} was not found.");

            ValidateTimeRange(request.StartTime, request.EndTime);
            ValidateDateRange(request.ValidFrom, request.ValidUntil);
            ValidateNotInPast(request.StartTime, request.ValidFrom);

            await CheckOverlapAsync(
                request.CoachId,
                request.Weekday,
                request.StartTime,
                request.EndTime,
                request.ValidFrom,
                request.ValidUntil,
                excludeId: null
            );

            var slot = new CoachAvailability
            {
                IdCoach = request.CoachId,
                Weekday = request.Weekday,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                ValidFrom = request.ValidFrom,
                ValidUntil = request.ValidUntil
            };

            _context.CoachAvailabilities.Add(slot);
            await _context.SaveChangesAsync();

            return slot.CoachavId;
        }

        public async Task UpdateAsync(int id, CoachAvailabilityUpdateRequest request)
        {
            var slot = await _context.CoachAvailabilities
                .FirstOrDefaultAsync(a => a.CoachavId == id);

            if (slot is null)
                throw new KeyNotFoundException($"Availability slot with id {id} was not found.");

            ValidateTimeRange(request.StartTime, request.EndTime);
            ValidateDateRange(request.ValidFrom, request.ValidUntil);
            ValidateNotInPast(request.StartTime, request.ValidFrom);

            await CheckOverlapAsync(
                slot.IdCoach,       // coach doesn't change on update
                request.Weekday,
                request.StartTime,
                request.EndTime,
                request.ValidFrom,
                request.ValidUntil,
                excludeId: id       // exclude self from overlap check
            );

            slot.Weekday = request.Weekday;
            slot.StartTime = request.StartTime;
            slot.EndTime = request.EndTime;
            slot.ValidFrom = request.ValidFrom;
            slot.ValidUntil = request.ValidUntil;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var rowsAffected = await _context.CoachAvailabilities
                .Where(a => a.CoachavId == id)
                .ExecuteDeleteAsync();

            if (rowsAffected == 0)
                throw new KeyNotFoundException($"Availability slot with id {id} was not found.");
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private static void ValidateTimeRange(TimeOnly start, TimeOnly end)
        {
            if (end <= start)
                throw new ArgumentException("EndTime must be after StartTime.");
        }

        private static void ValidateDateRange(DateOnly? validFrom, DateOnly? validUntil)
        {
            if (validFrom.HasValue && validUntil.HasValue && validUntil <= validFrom)
                throw new ArgumentException("ValidUntil must be after ValidFrom.");
        }

        // Ensure the availability start (combination of ValidFrom or today and StartTime)
        // is not in the past relative to UTC now. If ValidFrom is null the check uses
        // today's date so creating a slot for earlier today will be rejected.
        private static void ValidateNotInPast(TimeOnly startTime, DateOnly? validFrom)
        {
            var effectiveDate = validFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var startDateTimeUtc = effectiveDate.ToDateTime(startTime, DateTimeKind.Utc);

            if (startDateTimeUtc < DateTime.UtcNow)
                throw new ArgumentException("Start time cannot be in the past.");
        }

        // Checks that the new/updated slot does not overlap an existing slot
        // for the same coach on the same weekday within overlapping date ranges.
        // excludeId allows the record being updated to be skipped.
        private async Task CheckOverlapAsync(
            int coachId,
            byte weekday,
            TimeOnly startTime,
            TimeOnly endTime,
            DateOnly? validFrom,
            DateOnly? validUntil,
            int? excludeId)
        {
            var existing = await _context.CoachAvailabilities
                .Where(a =>
                    a.IdCoach == coachId &&
                    a.Weekday == weekday &&
                    (excludeId == null || a.CoachavId != excludeId))
                .ToListAsync();

            foreach (var slot in existing)
            {
                // Time overlap: two ranges overlap if start1 < end2 && start2 < end1
                bool timesOverlap = startTime < slot.EndTime && slot.StartTime < endTime;
                if (!timesOverlap) continue;

                // Date range overlap: treat null as open-ended (±infinity)
                // Two date ranges overlap if start1 < end2 && start2 < end1,
                // where null start = DateOnly.MinValue and null end = DateOnly.MaxValue.
                var newFrom = validFrom ?? DateOnly.MinValue;
                var newUntil = validUntil ?? DateOnly.MaxValue;
                var slotFrom = slot.ValidFrom ?? DateOnly.MinValue;
                var slotUntil = slot.ValidUntil ?? DateOnly.MaxValue;

                bool datesOverlap = newFrom < slotUntil && slotFrom < newUntil;
                if (!datesOverlap) continue;

                throw new InvalidOperationException(
                    $"This slot overlaps an existing availability record " +
                    $"(id {slot.CoachavId}) for this coach on the same weekday.");
            }
        }
    }
}
