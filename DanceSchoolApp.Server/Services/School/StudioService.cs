using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.School;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.School
{
    public class StudioService
    {
        private readonly AppDbContext _context;

        public StudioService(AppDbContext context)
        {
            _context = context;
        }

        //  Queries 

        public async Task<List<StudioListResponse>> GetStudiosAsync()
        {
            return await _context.Studios
                .Include(s => s.IdModalities)
                .Select(s => new StudioListResponse
                {
                    StudioId = s.StudioId,
                    Name = s.Name,
                    Capacity = s.Capacity,
                    Address = s.Address,
                    IsActive = s.IsActive,
                    ModalityCount = s.IdModalities.Count
                })
                .ToListAsync();
        }

        public async Task<StudioDetailResponse> GetStudioAsync(int id)
        {
            var studio = await _context.Studios
                .Include(s => s.IdModalities)
                .FirstOrDefaultAsync(s => s.StudioId == id);

            if (studio is null)
                throw new KeyNotFoundException($"Studio with id {id} was not found.");

            return new StudioDetailResponse
            {
                StudioId = studio.StudioId,
                Name = studio.Name,
                Capacity = studio.Capacity,
                Address = studio.Address,
                IsActive = studio.IsActive,
                Modalities = studio.IdModalities.Select(m => new ModalitySummaryResponse
                {
                    ModalityId = m.ModalityId,
                    Name = m.Name
                }).ToList()
            };
        }

        //  Commands 

        public async Task<int> CreateStudioAsync(StudioCreateRequest request)
        {
            bool nameExists = await _context.Studios
                .AnyAsync(s => s.Name == request.Name);

            if (nameExists)
                throw new InvalidOperationException($"A studio named '{request.Name}' already exists.");

            var studio = new Studio
            {
                Name = request.Name,
                Capacity = request.Capacity,
                Address = request.Address,
                IsActive = true
            };

            if (request.ModalityIds.Count > 0)
            {
                var modalities = await _context.Modalities
                    .Where(m => request.ModalityIds.Contains(m.ModalityId) && m.IsActive)
                    .ToListAsync();
                foreach (var m in modalities)
                    studio.IdModalities.Add(m);
            }

            _context.Studios.Add(studio);
            await _context.SaveChangesAsync();

            return studio.StudioId;
        }

        public async Task UpdateStudioAsync(int id, StudioUpdateRequest request)
        {
            var studio = await _context.Studios
                .Include(s => s.IdModalities)
                .FirstOrDefaultAsync(s => s.StudioId == id);

            if (studio is null)
                throw new KeyNotFoundException($"Studio with id {id} was not found.");

            bool nameConflict = await _context.Studios
                .AnyAsync(s => s.Name == request.Name && s.StudioId != id);

            if (nameConflict)
                throw new InvalidOperationException($"Another studio named '{request.Name}' already exists.");

            studio.Name = request.Name;
            studio.Capacity = request.Capacity;
            studio.Address = request.Address;

            studio.IdModalities.Clear();
            if (request.ModalityIds.Count > 0)
            {
                var modalities = await _context.Modalities
                    .Where(m => request.ModalityIds.Contains(m.ModalityId) && m.IsActive)
                    .ToListAsync();
                foreach (var m in modalities)
                    studio.IdModalities.Add(m);
            }

            await _context.SaveChangesAsync();
        }

        public async Task SetStudioStateAsync(int id, bool state)
        {
            if (!state)
            {
                bool hasActiveClasses = await _context.CoachClasses
                    .AnyAsync(c => c.IdStudio == id &&
                                   c.Status != (byte)CoachClassStatus.Rejected &&
                                   c.Status != (byte)CoachClassStatus.Cancelled &&
                                   c.Status != (byte)CoachClassStatus.Validated &&
                                   c.Status != (byte)CoachClassStatus.Finished);

                if (hasActiveClasses)
                    throw new InvalidOperationException(
                        "Cannot deactivate a studio that has active or pending classes. " +
                        "Finish or cancel those classes first.");
            }

            var rowsAffected = await _context.Studios
                .Where(s => s.StudioId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, state));

            if (rowsAffected == 0)
                throw new KeyNotFoundException($"Studio with id {id} was not found.");
        }

        //  Studio–Modality compatibility 

        public async Task AddModalityAsync(int studioId, int modalityId)
        {
            var studio = await _context.Studios
                .Include(s => s.IdModalities)
                .FirstOrDefaultAsync(s => s.StudioId == studioId);

            if (studio is null)
                throw new KeyNotFoundException($"Studio with id {studioId} was not found.");

            bool modalityExists = await _context.Modalities
                .AnyAsync(m => m.ModalityId == modalityId);

            if (!modalityExists)
                throw new KeyNotFoundException($"Modality with id {modalityId} was not found.");

            bool alreadyLinked = studio.IdModalities
                .Any(m => m.ModalityId == modalityId);

            if (alreadyLinked)
                throw new InvalidOperationException("This modality is already assigned to the studio.");

            var modality = await _context.Modalities.FindAsync(modalityId);
            studio.IdModalities.Add(modality!);

            await _context.SaveChangesAsync();
        }

        public async Task RemoveModalityAsync(int studioId, int modalityId)
        {
            var studio = await _context.Studios
                .Include(s => s.IdModalities)
                .FirstOrDefaultAsync(s => s.StudioId == studioId);

            if (studio is null)
                throw new KeyNotFoundException($"Studio with id {studioId} was not found.");

            var modality = studio.IdModalities
                .FirstOrDefault(m => m.ModalityId == modalityId);

            if (modality is null)
                throw new KeyNotFoundException("This modality is not assigned to the studio.");

            studio.IdModalities.Remove(modality);
            await _context.SaveChangesAsync();
        }
    }
}
