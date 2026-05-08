using Azure.Core;
using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;


namespace DanceSchoolApp.Server.Services.People
{
    public class CoachService
    {
        private readonly AppDbContext _context;

        public CoachService(AppDbContext context)
        {
            _context = context;
        }

        //  Queries 

        public async Task<List<CoachAvailableResponse>> GetAvailableCoachesAsync()
        {
            var coaches = await _context.Coaches
                .Include(c => c.CoachNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .Include(c => c.IdModalities)
                .Where(c => c.CoachNavigation.IsActive)
                .ToListAsync();

            return coaches
                .Select(c =>
                {
                    var p = c.CoachNavigation.PersonInfo;
                    return new CoachAvailableResponse
                    {
                        CoachId    = c.CoachId,
                        Name       = p is not null
                            ? $"{p.FirstName} {p.LastName}".Trim()
                            : c.CoachNavigation.Username,
                        Modalities = c.IdModalities
                            .Select(m => new ModalitySummary
                            {
                                ModalityId = m.ModalityId,
                                Name       = m.Name
                            })
                            .OrderBy(m => m.Name)
                            .ToList()
                    };
                })
                .OrderBy(r => r.Name)
                .ToList();
        }

        public async Task<List<CoachListResponse>> GetCoachsAsync()
        {
            var users = await _context.Users
                .Include(u => u.PersonInfo)
                .Include(u => u.IdRoles)
                .Include(u => u.Coach)
                .Where(u => u.IdRoles.Any(r => r.RoleId == Roles.Coach))
                .ToListAsync();

            return users.Select(r => new CoachListResponse
            {
                CoachId   = r.UserId,
                Name      = r.PersonInfo is not null
                    ? $"{r.PersonInfo.FirstName} {r.PersonInfo.LastName}".Trim()
                    : r.Username,
                Biography = r.Coach?.Biography,
                PhotoUrl  = r.Coach?.PhotoUrl,
                IsActive  = r.IsActive,
                PersonInfo = r.PersonInfo is null ? null : new PersonListResponse
                {
                    PersonId  = r.PersonInfo.PersonId,
                    FirstName = r.PersonInfo.FirstName,
                    LastName  = r.PersonInfo.LastName
                }
            }).ToList();
        }

        public async Task<CoachMeResponse> GetCoachMeAsync(int userId)
        {
            var coach = await _context.Coaches
                .Include(c => c.CoachNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .Include(c => c.IdModalities)
                .FirstOrDefaultAsync(c => c.CoachId == userId);

            if (coach is null)
                throw new KeyNotFoundException(
                    "Coach profile not found for the authenticated user.");

            var user = coach.CoachNavigation;
            var p    = user.PersonInfo;

            return new CoachMeResponse
            {
                CoachId    = coach.CoachId,
                Biography  = coach.Biography,
                Title      = null,
                PhotoUrl   = coach.PhotoUrl,
                IsActive   = user.IsActive,
                Name       = p is not null
                    ? $"{p.FirstName} {p.LastName}".Trim()
                    : user.Username,
                Email      = user.Email,
                Modalities = coach.IdModalities
                    .Select(m => new ModalitySummary
                    {
                        ModalityId = m.ModalityId,
                        Name       = m.Name
                    })
                    .OrderBy(m => m.Name)
                    .ToList()
            };
        }

        public async Task<CoachDetailResponse> GetCoachAsync(int id)
        {
            var coach = await _context.Users
               .Include(u => u.PersonInfo)
               .Include(u => u.IdRoles)
               .Include(u => u.Coach)
               .FirstOrDefaultAsync(u => u.UserId == id && u.IdRoles.Any(r => r.RoleId == Roles.Coach));


            if (coach is null)
                throw new KeyNotFoundException($"Coach with id {id} was not found.");

            return new CoachDetailResponse
            {
                CoachId = coach.UserId,
                Biography = coach.Coach == null ? null : coach.Coach.Biography,
                PhotoUrl = coach.Coach == null ? null : coach.Coach.PhotoUrl,
                IsActive = coach.IsActive,
                PersonInfo = coach.PersonInfo is null ? null : new PersonDetailResponse
                {
                    PersonId = coach.PersonInfo.PersonId,
                    FirstName = coach.PersonInfo.FirstName,
                    LastName = coach.PersonInfo.LastName,
                    BirthDate = coach.PersonInfo.BirthDate,
                    Phone = coach.PersonInfo.Phone,
                    Address = coach.PersonInfo.Address,
                    Nif = coach.PersonInfo.Nif
                }
            };
        }

        public async Task<bool> CreateCoachAsync(int coachid)
        {

            bool coachExists = await _context.Coaches
                .AnyAsync(u => u.CoachId == coachid);

            if (coachExists)
                throw new InvalidOperationException("Coach already exists.");

            var coach = new Coach
            {
                CoachId = coachid,
            };
            _context.Coaches.Add(coach);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task SetPhotoAsync(int coachId, string? photoUrl)
        {
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.CoachId == coachId);

            if (coach is null)
                throw new KeyNotFoundException($"Coach with id {coachId} was not found.");

            coach.PhotoUrl = photoUrl?.Trim();
            await _context.SaveChangesAsync();
        }

        public async Task ReplacePhotoFileAsync(int coachId, string? newPhotoUrl, string webRootPath)
        {
            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.CoachId == coachId);

            if (coach is null)
                throw new KeyNotFoundException($"Coach with id {coachId} was not found.");

            // if existing photo exists, attempt to delete physical file
            if (!string.IsNullOrEmpty(coach.PhotoUrl))
            {
                var existing = coach.PhotoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var existingFull = Path.Combine(webRootPath, existing);
                try
                {
                    if (File.Exists(existingFull)) File.Delete(existingFull);
                }
                catch
                {
                    // swallow exceptions for file delete to avoid failing the request
                }
            }

            coach.PhotoUrl = newPhotoUrl?.Trim();
            await _context.SaveChangesAsync();
        }


    }
}
