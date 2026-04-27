using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.Scheduling;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Scheduling
{
    public class BookingService
    {
        private readonly AppDbContext _context;

        public BookingService(AppDbContext context)
        {
            _context = context;
        }

        // Returns free coaching time windows grouped by date, after subtracting
        // existing bookings and blocked periods from each coach's availability.
        public async Task<List<DaySlotResponse>> GetAvailableSlotsAsync(
            DateOnly from,
            DateOnly to,
            int? coachId,
            int? modalityId)
        {
            //  1. Determine which weekday bytes are present in the range 
            var weekdaysInRange = new HashSet<byte>();
            for (var d = from; d <= to; d = d.AddDays(1))
                weekdaysInRange.Add((byte)d.DayOfWeek);

            //  2. Load CoachAvailability rows for those weekdays 
            // Include the coach's user info (for name) and their modalities.
            var availQuery = _context.CoachAvailabilities
                .Include(a => a.IdCoachNavigation)
                    .ThenInclude(c => c.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(a => a.IdCoachNavigation)
                    .ThenInclude(c => c.IdModalities)
                .Where(a => weekdaysInRange.Contains(a.Weekday));

            if (coachId.HasValue)
                availQuery = availQuery.Where(a => a.IdCoach == coachId.Value);

            if (modalityId.HasValue)
                availQuery = availQuery.Where(a =>
                    a.IdCoachNavigation.IdModalities
                        .Any(m => m.ModalityId == modalityId.Value));

            var availabilities = await availQuery.ToListAsync();

            if (!availabilities.Any())
                return new List<DaySlotResponse>();

            //  3. Load existing bookings for the relevant coaches in this range 
            var involvedCoachIds = availabilities.Select(a => a.IdCoach).Distinct().ToList();

            // Use an exclusive upper bound so we catch classes that start right at 00:00
            // on the day after `to` (they wouldn't overlap anyway, but be precise).
            var rangeStartDt = from.ToDateTime(TimeOnly.MinValue);
            var rangeEndDt   = to.ToDateTime(new TimeOnly(23, 59, 59));

            var bookings = await _context.CoachClasses
                .Where(c =>
                    involvedCoachIds.Contains(c.IdCoach) &&
                    c.Status != (byte)CoachClassStatus.Rejected &&
                    c.Status != (byte)CoachClassStatus.Cancelled &&
                    c.StartDatetime < rangeEndDt &&
                    c.EndDatetime   > rangeStartDt)
                .Select(c => new { c.IdCoach, c.StartDatetime, c.EndDatetime })
                .ToListAsync();

            //  4. Load BlockedPeriods that overlap the range 
            // Scope 0,1,4,5 = global  |  Scope 3 = coach-specific
            // Scope 2 (studio-specific) intentionally excluded — studio is chosen
            // at booking time, not during slot discovery.
            var blockedPeriods = await _context.BlockedPeriods
                .Where(b =>
                    (b.Scope == 0 || b.Scope == 1 || b.Scope == 3 ||
                     b.Scope == 4 || b.Scope == 5) &&
                    b.StartDatetime < rangeEndDt &&
                    b.EndDatetime   > rangeStartDt)
                .Select(b => new { b.Scope, b.IdCoach, b.StartDatetime, b.EndDatetime })
                .ToListAsync();

            var globalBlocks     = blockedPeriods.Where(b => b.Scope != 3).ToList();
            var coachSpecBlocks  = blockedPeriods.Where(b => b.Scope == 3).ToList();

            //  5. Expand availability to actual dates and subtract busy windows 
            var resultByDate = new SortedDictionary<DateOnly, List<CoachSlot>>();

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var slotsForDay = new List<CoachSlot>();

                var dayAvailabilities = availabilities
                    .Where(a =>
                        a.Weekday == (byte)date.DayOfWeek &&
                        (a.ValidFrom  is null || a.ValidFrom  <= date) &&
                        (a.ValidUntil is null || a.ValidUntil >= date))
                    .ToList();

                foreach (var avail in dayAvailabilities)
                {
                    var slotStart = date.ToDateTime(avail.StartTime);
                    var slotEnd   = date.ToDateTime(avail.EndTime);

                    // Guard against zero-length or inverted slots in source data.
                    if (slotEnd <= slotStart) continue;

                    var freeWindows = new List<(DateTime Start, DateTime End)>
                    {
                        (slotStart, slotEnd)
                    };

                    // Subtract existing class bookings for this coach.
                    foreach (var b in bookings.Where(b => b.IdCoach == avail.IdCoach))
                        freeWindows = Subtract(freeWindows, b.StartDatetime, b.EndDatetime);

                    // Subtract global blocked periods (apply to all coaches).
                    foreach (var b in globalBlocks)
                        freeWindows = Subtract(freeWindows, b.StartDatetime, b.EndDatetime);

                    // Subtract coach-specific blocked periods.
                    foreach (var b in coachSpecBlocks.Where(b => b.IdCoach == avail.IdCoach))
                        freeWindows = Subtract(freeWindows, b.StartDatetime, b.EndDatetime);

                    if (!freeWindows.Any()) continue;

                    // Build modality lists for this coach.
                    var allModalities = avail.IdCoachNavigation.IdModalities.ToList();
                    var modalityIds   = allModalities.Select(m => m.ModalityId).ToList();
                    var modalityNames = allModalities.Select(m => m.Name).ToList();

                    var coachName = ResolveCoachName(avail.IdCoachNavigation);

                    foreach (var (wStart, wEnd) in freeWindows)
                    {
                        slotsForDay.Add(new CoachSlot
                        {
                            CoachId       = avail.IdCoach,
                            CoachName     = coachName,
                            StartTime     = TimeOnly.FromDateTime(wStart),
                            EndTime       = TimeOnly.FromDateTime(wEnd),
                            ModalityIds   = modalityIds,
                            ModalityNames = modalityNames
                        });
                    }
                }

                if (slotsForDay.Any())
                    resultByDate[date] = slotsForDay;
            }

            return resultByDate
                .Select(kv => new DaySlotResponse { Date = kv.Key, Slots = kv.Value })
                .ToList();
        }

        //  Interval subtraction 
        // Removes the window [busyStart, busyEnd) from every free interval in the
        // list. Returns the remaining free windows (may be empty, the same, or
        // split into up to two new intervals per input interval).
        private static List<(DateTime Start, DateTime End)> Subtract(
            List<(DateTime Start, DateTime End)> free,
            DateTime busyStart,
            DateTime busyEnd)
        {
            var result = new List<(DateTime, DateTime)>(free.Count);

            foreach (var (fs, fe) in free)
            {
                // No overlap — keep as-is.
                if (busyEnd <= fs || busyStart >= fe)
                {
                    result.Add((fs, fe));
                    continue;
                }

                // Busy fully contains this free window — drop it entirely.
                if (busyStart <= fs && busyEnd >= fe)
                    continue;

                // Busy clips the beginning.
                if (busyStart <= fs)
                {
                    result.Add((busyEnd, fe));
                    continue;
                }

                // Busy clips the end.
                if (busyEnd >= fe)
                {
                    result.Add((fs, busyStart));
                    continue;
                }

                // Busy is in the middle — split into two.
                result.Add((fs, busyStart));
                result.Add((busyEnd, fe));
            }

            return result;
        }

        private static string ResolveCoachName(Coach coach)
        {
            var person = coach.CoachNavigation?.PersonInfo;
            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : coach.CoachNavigation?.Username ?? $"Coach {coach.CoachId}";
        }
    }
}
