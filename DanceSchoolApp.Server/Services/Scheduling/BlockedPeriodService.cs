using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Scheduling;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Scheduling
{
    public class BlockedPeriodService
    {
        private readonly AppDbContext _context;

        public BlockedPeriodService(AppDbContext context)
        {
            _context = context;
        }

        //  Queries 

        public async Task<List<BlockedPeriodListResponse>> GetAllAsync()
        {
            return await _context.BlockedPeriods
                .Include(b => b.IdStudioNavigation)
                .Include(b => b.IdCoachNavigation)
                    .ThenInclude(c => c!.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Select(b => MapToListResponse(b))
                .ToListAsync();
        }

        public async Task<BlockedPeriodDetailResponse> GetByIdAsync(int id)
        {
            var block = await _context.BlockedPeriods
                .Include(b => b.IdStudioNavigation)
                .Include(b => b.IdCoachNavigation)
                    .ThenInclude(c => c!.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .FirstOrDefaultAsync(b => b.BlockedId == id);

            if (block is null)
                throw new KeyNotFoundException($"Blocked period with id {id} was not found.");

            return new BlockedPeriodDetailResponse
            {
                BlockedId = block.BlockedId,
                Scope = (BlockedPeriodScope)block.Scope,
                StartDatetime = block.StartDatetime,
                EndDatetime = block.EndDatetime,
                Reason = block.Reason,
                StudioId = block.IdStudio,
                StudioName = block.IdStudioNavigation?.Name,
                CoachId = block.IdCoach,
                CoachName = ResolveCoachName(block.IdCoachNavigation)
            };
        }

        public async Task<List<BlockedPeriodListResponse>> GetActiveAsync()
        {
            var now = DateTime.Now;

            return await _context.BlockedPeriods
                .Include(b => b.IdStudioNavigation)
                .Include(b => b.IdCoachNavigation)
                    .ThenInclude(c => c!.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Where(b => b.StartDatetime <= now && b.EndDatetime >= now)
                .Select(b => MapToListResponse(b))
                .ToListAsync();
        }

        public async Task<List<BlockedPeriodListResponse>> GetByRangeAsync(DateTime from, DateTime to)
        {
            if (to <= from)
                throw new ArgumentException("'To' must be after 'From'.");

            // Returns any block that overlaps the requested range,
            // not just those fully contained within it.
            return await _context.BlockedPeriods
                .Include(b => b.IdStudioNavigation)
                .Include(b => b.IdCoachNavigation)
                    .ThenInclude(c => c!.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Where(b => b.StartDatetime < to && b.EndDatetime > from)
                .Select(b => MapToListResponse(b))
                .ToListAsync();
        }

        public async Task<List<BlockedPeriodListResponse>> GetByCoachAsync(int coachId)
        {
            bool coachExists = await _context.Coaches
                .AnyAsync(c => c.CoachId == coachId);

            if (!coachExists)
                throw new KeyNotFoundException($"Coach with id {coachId} was not found.");

            return await _context.BlockedPeriods
                .Include(b => b.IdStudioNavigation)
                .Include(b => b.IdCoachNavigation)
                    .ThenInclude(c => c!.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Where(b => b.IdCoach == coachId)
                .Select(b => MapToListResponse(b))
                .ToListAsync();
        }

        public async Task<List<BlockedPeriodListResponse>> GetByStudioAsync(int studioId)
        {
            bool studioExists = await _context.Studios
                .AnyAsync(s => s.StudioId == studioId);

            if (!studioExists)
                throw new KeyNotFoundException($"Studio with id {studioId} was not found.");

            return await _context.BlockedPeriods
                .Include(b => b.IdStudioNavigation)
                .Include(b => b.IdCoachNavigation)
                    .ThenInclude(c => c!.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Where(b => b.IdStudio == studioId)
                .Select(b => MapToListResponse(b))
                .ToListAsync();
        }

        //  Commands 

        public async Task<int> CreateAsync(BlockedPeriodCreateRequest request)
        {
            await ValidateScopeReferencesAsync(
                request.Scope, request.StudioId, request.CoachId);

            ValidateFutureDateRange(request.StartDatetime, request.EndDatetime);

            var block = new BlockedPeriod
            {
                Scope = (byte)request.Scope,
                StartDatetime = request.StartDatetime,
                EndDatetime = request.EndDatetime,
                Reason = request.Reason,
                IdStudio = request.StudioId,
                IdCoach = request.CoachId
            };

            _context.BlockedPeriods.Add(block);
            await _context.SaveChangesAsync();

            return block.BlockedId;
        }

        public async Task UpdateAsync(int id, BlockedPeriodUpdateRequest request)
        {
            var block = await _context.BlockedPeriods
                .FirstOrDefaultAsync(b => b.BlockedId == id);

            if (block is null)
                throw new KeyNotFoundException($"Blocked period with id {id} was not found.");

            await ValidateScopeReferencesAsync(
                request.Scope, request.StudioId, request.CoachId);

            ValidateFutureDateRange(request.StartDatetime, request.EndDatetime);

            block.Scope = (byte)request.Scope;
            block.StartDatetime = request.StartDatetime;
            block.EndDatetime = request.EndDatetime;
            block.Reason = request.Reason;
            block.IdStudio = request.StudioId;
            block.IdCoach = request.CoachId;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var rowsAffected = await _context.BlockedPeriods
                .Where(b => b.BlockedId == id)
                .ExecuteDeleteAsync();

            if (rowsAffected == 0)
                throw new KeyNotFoundException($"Blocked period with id {id} was not found.");
        }

        //  Private helpers 

        private static void ValidateFutureDateRange(DateTime start, DateTime end)
        {
            if (end <= start)
                throw new ArgumentException("EndDatetime must be after StartDatetime.");

            if (start <= DateTime.Now)
                throw new ArgumentException("Blocked periods must start in the future.");
        }

        // Verifies that the referenced studio/coach actually exist in the DB.
        // Cross-field presence is already validated by IValidatableObject on
        // the request DTO — this layer only checks referential integrity.
        private async Task ValidateScopeReferencesAsync(
            BlockedPeriodScope scope, int? studioId, int? coachId)
        {
            if (scope == BlockedPeriodScope.Studio && studioId.HasValue)
            {
                bool studioExists = await _context.Studios
                    .AnyAsync(s => s.StudioId == studioId.Value);

                if (!studioExists)
                    throw new KeyNotFoundException($"Studio with id {studioId} was not found.");
            }

            if (scope == BlockedPeriodScope.Coach && coachId.HasValue)
            {
                bool coachExists = await _context.Coaches
                    .AnyAsync(c => c.CoachId == coachId.Value);

                if (!coachExists)
                    throw new KeyNotFoundException($"Coach with id {coachId} was not found.");
            }
        }

        private static string? ResolveCoachName(Coach? coach)
        {
            if (coach is null) return null;

            var person = coach.CoachNavigation?.PersonInfo;
            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : coach.CoachNavigation?.Username;
        }

        // Extracted to a static method so it can be reused in both
        // GetAllAsync and the filtered query methods without duplication.
        // EF Core cannot translate this to SQL when used inside .Select(),
        // so all queries materialise first then map — acceptable here since
        // BlockedPeriods is a small administrative table.
        private static BlockedPeriodListResponse MapToListResponse(BlockedPeriod b)
        {
            return new BlockedPeriodListResponse
            {
                BlockedId = b.BlockedId,
                Scope = (BlockedPeriodScope)b.Scope,
                StartDatetime = b.StartDatetime,
                EndDatetime = b.EndDatetime,
                Reason = b.Reason,
                StudioId = b.IdStudio,
                StudioName = b.IdStudioNavigation?.Name,
                CoachId = b.IdCoach,
                CoachName = ResolveCoachName(b.IdCoachNavigation)
            };
        }
    }
}
