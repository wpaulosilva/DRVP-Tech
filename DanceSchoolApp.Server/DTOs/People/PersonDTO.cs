using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace DanceSchoolApp.Server.DTOs.People
{

    public class PersonRequest
    {
        [Required]
        public string? FirstName { get; set; } = null!;
        [Required]
        public string? LastName { get; set; } = null!;
        [Required]
        public DateOnly? BirthDate { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        [MaxLength(9)]
        [Required]
        public string? Nif { get; set; }
    }

    public class PersonDetailResponse
    {
        public int? PersonId { get; set; }
        public string? FirstName { get; set; } = null!;
        public string? LastName { get; set; } = null!;
        public DateOnly? BirthDate { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Nif { get; set; }
    }
    public class PersonListResponse
    {
        public int? PersonId { get; set; }
        public string? FirstName { get; set; } = null!;
        public string? LastName { get; set; } = null!;
    }

}
