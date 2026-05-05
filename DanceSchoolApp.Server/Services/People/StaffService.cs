using Azure.Core;
using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.People
{
    public class StaffService
    {
        private readonly AppDbContext _context;

        public StaffService(AppDbContext context)
        {
            _context = context;
        }

        //  Queries 

        public async Task<PagedResponse<StaffListResponse>> GetStaffsAsync(
            int page = 1,
            int pageSize = 7,
            string? search = "",
            string? sortBy = "",
            string? sortDir = "asc")
        {
            var query = _context.Users
                .Include(u => u.PersonInfo)
                .Include(u => u.IdRoles)
                .Where(u =>
                    u.IdRoles.Any(r => r.RoleId == Roles.Coach) ||
                    u.IdRoles.Any(r => r.RoleId == Roles.Parent)
                );
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();

                query = query.Where(u =>
                    u.Email.ToLower().Contains(search) ||
                    u.Username.ToLower().Contains(search) ||
                    (u.PersonInfo != null &&
                        (
                            u.PersonInfo.FirstName.ToLower().Contains(search) ||
                            u.PersonInfo.LastName.ToLower().Contains(search)
                        )
                    )
                );
            }

            query = sortBy switch
            {
                "name" => sortDir == "desc"
                    ? query.OrderByDescending(u => u.PersonInfo!.FirstName).ThenByDescending(u => u.PersonInfo!.LastName)
                    : query.OrderBy(u => u.PersonInfo!.FirstName).ThenBy(u => u.PersonInfo!.LastName),

                "email" => sortDir == "desc"
                    ? query.OrderByDescending(u => u.Email)
                    : query.OrderBy(u => u.Email),

                "status" => sortDir == "desc"
                    ? query.OrderByDescending(u => u.IsActive)
                    : query.OrderBy(u => u.IsActive),

                "role" => sortDir == "desc"
                    ? query.OrderByDescending(u =>
                        u.IdRoles.Any(r => r.RoleId == Roles.Coach) ? 1 :
                        u.IdRoles.Any(r => r.RoleId == Roles.Parent) ? 2 : 3)
                    : query.OrderBy(u =>
                        u.IdRoles.Any(r => r.RoleId == Roles.Coach) ? 1 :
                        u.IdRoles.Any(r => r.RoleId == Roles.Parent) ? 2 : 3),

                _ => query.OrderBy(u => u.UserId)
            };

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new StaffListResponse
                {
                    StaffId = u.UserId,
                    IsActive = u.IsActive,
                    Email = u.Email,
                    Role = u.IdRoles.Any(r => r.RoleId == Roles.Coach)
                    ? "Professor"
                    : u.IdRoles.Any(r => r.RoleId == Roles.Parent)
                        ? "Encarregado"
                        : "—",
                    PersonInfo = u.PersonInfo == null ? null : new PersonListResponse
                    {
                        PersonId = u.PersonInfo.PersonId,
                        FirstName = u.PersonInfo.FirstName,
                        LastName = u.PersonInfo.LastName
                    }
                })
                .ToListAsync();

            return new PagedResponse<StaffListResponse>
            {
                Items = items,
                TotalCount = totalCount
            };
        }

        public async Task<StaffMeResponse> GetStaffMeAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.PersonInfo)
                .Include(u => u.IdRoles)
                .FirstOrDefaultAsync(u => u.UserId == userId &&
                                          u.IdRoles.Any(r => r.RoleId == Roles.Staff));

            if (user is null)
                throw new KeyNotFoundException("Staff profile not found.");

            var p = user.PersonInfo;

            return new StaffMeResponse
            {
                StaffId = user.UserId,
                Username = user.Username,
                Name = p is not null
                    ? $"{p.FirstName} {p.LastName}".Trim()
                    : user.Username,
                Email = user.Email
            };
        }

        public async Task<StaffDetailResponse> GetStaffAsync(int id)
        {
            var staff = await _context.Users
                .Include(u => u.PersonInfo)
                .Include(u => u.IdRoles)
                .FirstOrDefaultAsync(u => u.UserId == id && u.IdRoles.Any(r => r.RoleId == Roles.Staff));

            if (staff is null)
                throw new KeyNotFoundException($"Staff with id {id} was not found.");

            return new StaffDetailResponse
            {
                StaffId = staff.UserId,
                IsActive = staff.IsActive,
                PersonInfo = staff.PersonInfo == null ? null : new PersonDetailResponse
                {
                    PersonId = staff.PersonInfo.PersonId,
                    FirstName = staff.PersonInfo.FirstName,
                    LastName = staff.PersonInfo.LastName,
                    BirthDate = staff.PersonInfo.BirthDate,
                    Phone = staff.PersonInfo.Phone,
                    Address = staff.PersonInfo.Address,
                    Nif = staff.PersonInfo.Nif
                }
            };
        }
    }

    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }
}