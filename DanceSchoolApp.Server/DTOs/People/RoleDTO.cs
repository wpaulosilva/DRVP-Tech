using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.People
{

    /*
    public class RoleCreateRequest
    {
        public byte RoleId { get; set; }
        public string RoleName { get; set; }
    }

    public class RoleAssign
    {
        public int UserId { get; set; }
        public byte RoleId { get; set; }
    }

    public class RoleResponse
    {
        public byte RoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public List<UserListResponse> Users { get; set; } = new();
    }
    
    public class RoleSummaryResponse
    {
        public byte RoleId { get; set; }
        public string RoleName { get; set; }
    }
    */

    //  Responses 

    public class RoleListResponse
    {
        public byte RoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public int UserCount { get; set; }
    }

    public class RoleDetailResponse
    {
        public byte RoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public List<RoleUserSummary> Users { get; set; } = new();
    }
    public class RoleUserSummary
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
    }

    public class RoleSummaryResponse
    {
        public byte RoleId { get; set; }
        public string RoleName { get; set; } = null!;
    }

    //  Requests 

    // NOTE: CreateRole is intentionally disabled in the controller.
    // Roles are seeded at DB level (admin, staff, coach, parent).
    // This request exists for completeness and future admin-only use.
    public class RoleCreateRequest
    {
        [Required]
        public byte RoleId { get; set; }

        [Required]
        [MaxLength(32)]
        public string RoleName { get; set; } = null!;
    }

    public class RoleAssignRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public byte RoleId { get; set; }
    }
}
