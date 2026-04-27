using Azure.Core;
using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.People
{
    public class RoleService
    {
        private readonly AppDbContext _context;
        public RoleService(AppDbContext context)
        {
            _context = context;
        }

        //  Queries 

        public async Task<List<RoleListResponse>> GetRolesAsync()
        {
            return await _context.Roles
                .Include(r => r.IdUsers)
                .Select(r => new RoleListResponse
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    UserCount = r.IdUsers.Count
                })
                .ToListAsync();
        }

        public async Task<RoleDetailResponse> GetRoleAsync(byte id)
        {
            var role = await _context.Roles
                .Include(r => r.IdUsers)
                .FirstOrDefaultAsync(r => r.RoleId == id);

            if (role is null)
                throw new KeyNotFoundException($"Role with id {id} was not found.");

            return new RoleDetailResponse
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                Users = role.IdUsers.Select(u => new RoleUserSummary
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    Email = u.Email,
                    IsActive = u.IsActive
                }).ToList()
            };
        }

        //  Commands 

        // Disabled at controller level — roles are seeded, not freely created.
        // Kept here for potential future admin tooling.
        public async Task CreateRoleAsync(RoleCreateRequest request)
        {
            bool exists = await _context.Roles
                .AnyAsync(r => r.RoleId == request.RoleId || r.RoleName == request.RoleName);

            if (exists)
                throw new InvalidOperationException("A role with that id or name already exists.");

            var role = new Role
            {
                RoleId = request.RoleId,
                RoleName = request.RoleName
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();
        }

        public async Task AssignRoleAsync(RoleAssignRequest request)
        {
            var user = await _context.Users
                .Include(u => u.IdRoles)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId);

            if (user is null)
                throw new KeyNotFoundException($"User with id {request.UserId} was not found.");

            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleId == request.RoleId);

            if (role is null)
                throw new KeyNotFoundException($"Role with id {request.RoleId} was not found.");

            bool alreadyAssigned = user.IdRoles.Any(r => r.RoleId == request.RoleId);

            if (alreadyAssigned)
                throw new InvalidOperationException("User already has this role.");

            user.IdRoles.Add(role);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveRoleAsync(RoleAssignRequest request)
        {
            var user = await _context.Users
                .Include(u => u.IdRoles)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId);

            if (user is null)
                throw new KeyNotFoundException($"User with id {request.UserId} was not found.");

            var role = user.IdRoles.FirstOrDefault(r => r.RoleId == request.RoleId);

            if (role is null)
                throw new KeyNotFoundException("This role is not assigned to the user.");

            user.IdRoles.Remove(role);
            await _context.SaveChangesAsync();
        }
    }
}
