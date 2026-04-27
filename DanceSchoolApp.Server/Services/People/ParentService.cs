using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.People
{
    public class ParentService
    {

        private readonly AppDbContext _context;

        public ParentService(AppDbContext context)
        {
            _context = context;
        }

        //  Queries 

        public async Task<List<ParentListResponse>> GetParentsAsync()
        {
            return await _context.Users
                .Include(u => u.PersonInfo)
                .Include(u => u.IdRoles)
                .Where(u => u.IdRoles.Any(r => r.RoleId == Roles.Parent))
                .Select(r => new ParentListResponse
                {
                    ParentId = r.UserId,
                    IsActive = r.IsActive,
                    PersonInfo = r.PersonInfo == null ? null : new PersonListResponse
                    {
                        PersonId = r.PersonInfo.PersonId,
                        FirstName = r.PersonInfo.FirstName,
                        LastName = r.PersonInfo.LastName
                    }
                })
                .ToListAsync();
        }

        public async Task<ParentMeResponse> GetParentMeAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.PersonInfo)
                .Include(u => u.Students)
                    .ThenInclude(s => s.PersonInfo)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user is null)
                throw new KeyNotFoundException("User not found.");

            var p = user.PersonInfo;

            return new ParentMeResponse
            {
                ParentId = user.UserId,
                Username = user.Username,
                Name     = p is not null
                    ? $"{p.FirstName} {p.LastName}".Trim()
                    : user.Username,
                Email    = user.Email,
                Children = user.Students
                    .Select(s => new ChildSummary
                    {
                        ChildId = s.StudentId,
                        Name    = s.PersonInfo is not null
                            ? $"{s.PersonInfo.FirstName} {s.PersonInfo.LastName}".Trim()
                            : $"Student {s.StudentId}"
                    })
                    .ToList()
            };
        }

        public async Task<ParentDetailResponse> GetParentAsync(int id)
        {
            var user = await _context.Users
                .Include(u => u.PersonInfo)
                .Include(u => u.IdRoles)
                .FirstOrDefaultAsync(u => u.UserId == id && u.IdRoles.Any(r => r.RoleId == Roles.Parent));

            if (user is null)
                throw new KeyNotFoundException($"Parent with id {id} was not found.");

            return new ParentDetailResponse
            {
                ParentId = user.UserId,
                IsActive = user.IsActive,
                PersonInfo = user.PersonInfo == null ? null : new PersonDetailResponse
                {
                    PersonId = user.PersonInfo.PersonId,
                    FirstName = user.PersonInfo.FirstName,
                    LastName = user.PersonInfo.LastName,
                    BirthDate = user.PersonInfo.BirthDate,
                    Phone = user.PersonInfo.Phone,
                    Address = user.PersonInfo.Address,
                    Nif = user.PersonInfo.Nif,
                }
            };
        }

    }
}
