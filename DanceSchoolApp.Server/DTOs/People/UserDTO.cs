using System.ComponentModel.DataAnnotations;

namespace DanceSchoolApp.Server.DTOs.People
{
    //  Responses 

    public class UserListResponse
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public DateOnly CreatedAt { get; set; }
        public List<RoleSummaryResponse> Roles { get; set; } = new();
    }

    public class UserDetailResponse
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public DateOnly CreatedAt { get; set; }
        public PersonDetailResponse? PersonInfo { get; set; }
        public List<RoleSummaryResponse> Roles { get; set; } = new();
    }

    public class UserRolesResponse
    {
        public int UserId { get; set; }
        public List<RoleSummaryResponse> Roles { get; set; } = new();
    }


    //  Requests 

    public class UpdatePersonInfoRequest
    {
        [Required]
        public required string  FirstName { get; set; }
        [Required]
        public required string  LastName  { get; set; }
        public string?  Phone     { get; set; }
        public string?  Address   { get; set; }
        [Required]
        public DateOnly BirthDate { get; set; }
        [MaxLength(9)]
        [Required]
        public required string  Nif       { get; set; }
    }

    public class UserCreateRequest
    {
        [Required]
        [MaxLength(64)]
        public string Username { get; set; } = null!;

        [Required]
        [EmailAddress]
        [MaxLength(254)]
        public string Email { get; set; } = null!;

        [Required]
        public byte? FirstRole { get; set; } = null;

        public PersonRequest? PersonInfo { get; set; } = null;
    }
}
