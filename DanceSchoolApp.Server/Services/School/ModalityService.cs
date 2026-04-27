using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.School;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.School
{
    public class ModalityService
    {
        private readonly AppDbContext _context;

        public ModalityService(AppDbContext context)
        {
            _context = context;
        }

        //  Queries 

        public async Task<List<ModalityListResponse>> GetModalitiesAsync()
        {
            return await _context.Modalities
                .Select(m => new ModalityListResponse
                {
                    ModalityId = m.ModalityId,
                    Name = m.Name,
                    Description = m.Description,
                    IsActive = m.IsActive
                })
                .ToListAsync();
        }

        public async Task<ModalityDetailResponse> GetModalityAsync(int id)
        {
            var modality = await _context.Modalities
                .Include(m => m.IdStudios)
                .Include(m => m.IdCoaches)
                .FirstOrDefaultAsync(m => m.ModalityId == id);

            if (modality is null)
                throw new KeyNotFoundException($"Modality with id {id} was not found.");

            return new ModalityDetailResponse
            {
                ModalityId = modality.ModalityId,
                Name = modality.Name,
                Description = modality.Description,
                IsActive = modality.IsActive,
                StudioCount = modality.IdStudios.Count,
                CoachCount = modality.IdCoaches.Count
            };
        }

        //  Commands 

        public async Task<int> CreateModalityAsync(ModalityCreateRequest request)
        {
            bool nameExists = await _context.Modalities
                .AnyAsync(m => m.Name == request.Name);

            if (nameExists)
                throw new InvalidOperationException($"A modality named '{request.Name}' already exists.");

            var modality = new Modality
            {
                Name = request.Name,
                Description = request.Description,
                IsActive = true
            };

            _context.Modalities.Add(modality);
            await _context.SaveChangesAsync();

            return modality.ModalityId;
        }

        public async Task UpdateModalityAsync(int id, ModalityUpdateRequest request)
        {
            var modality = await _context.Modalities
                .FirstOrDefaultAsync(m => m.ModalityId == id);

            if (modality is null)
                throw new KeyNotFoundException($"Modality with id {id} was not found.");

            bool nameConflict = await _context.Modalities
                .AnyAsync(m => m.Name == request.Name && m.ModalityId != id);

            if (nameConflict)
                throw new InvalidOperationException($"Another modality named '{request.Name}' already exists.");

            modality.Name = request.Name;
            modality.Description = request.Description;
            await _context.SaveChangesAsync();
        }

        public async Task AssignCoachAsync(int modalityId, int coachId)
        {
            bool modalityActive = await _context.Modalities
                .AnyAsync(m => m.ModalityId == modalityId && m.IsActive);

            if (!modalityActive)
                throw new KeyNotFoundException($"Modality with id {modalityId} was not found or is inactive.");

            bool coachExists = await _context.Coaches
                .AnyAsync(c => c.CoachId == coachId);

            if (!coachExists)
                throw new KeyNotFoundException($"Coach with id {coachId} was not found.");

            var modality = await _context.Modalities
                .Include(m => m.IdCoaches)
                .FirstAsync(m => m.ModalityId == modalityId);

            bool alreadyAssigned = modality.IdCoaches.Any(c => c.CoachId == coachId);
            if (alreadyAssigned)
                throw new InvalidOperationException(
                    $"Coach {coachId} is already assigned to modality {modalityId}.");

            var coach = await _context.Coaches.FindAsync(coachId);
            modality.IdCoaches.Add(coach!);
            await _context.SaveChangesAsync();
        }

        public async Task UnassignCoachAsync(int modalityId, int coachId)
        {
            var modality = await _context.Modalities
                .Include(m => m.IdCoaches)
                .FirstOrDefaultAsync(m => m.ModalityId == modalityId);

            if (modality is null)
                throw new KeyNotFoundException($"Modality with id {modalityId} was not found.");

            var coach = modality.IdCoaches.FirstOrDefault(c => c.CoachId == coachId);
            if (coach is null)
                throw new KeyNotFoundException(
                    $"Coach {coachId} is not assigned to modality {modalityId}.");

            modality.IdCoaches.Remove(coach);
            await _context.SaveChangesAsync();
        }

        public async Task SetModalityStateAsync(int modalityId, bool isActive)
        {
            if (!isActive)
            {
                bool hasActiveClasses = await _context.CoachClasses
                    .AnyAsync(c => c.IdModality == modalityId &&
                                   c.Status != (byte)CoachClassStatus.Rejected &&
                                   c.Status != (byte)CoachClassStatus.Cancelled &&
                                   c.Status != (byte)CoachClassStatus.Validated &&
                                   c.Status != (byte)CoachClassStatus.Finished);

                if (hasActiveClasses)
                    throw new InvalidOperationException(
                        "Cannot deactivate a modality that has active or pending classes. " +
                        "Finish or cancel those classes first.");
            }

            var rowsAffected = await _context.Modalities
                .Where(m => m.ModalityId == modalityId)
                .ExecuteUpdateAsync(m => m.SetProperty(x => x.IsActive, isActive));

            if (rowsAffected == 0)
                throw new KeyNotFoundException($"Modality with id {modalityId} was not found.");
        }
    }
}
