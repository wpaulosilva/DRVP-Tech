namespace DanceSchoolApp.Server.DTOs.People
{

    //  Responses 

    public class StaffMeResponse
    {
        public int     StaffId  { get; set; }
        public string  Username { get; set; } = null!;
        public string  Name     { get; set; } = null!;
        public string? Email    { get; set; }
    }

    public class StaffListResponse
    {
        public int StaffId { get; set; }
        public bool IsActive { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public PersonListResponse? PersonInfo { get; set; }
    }
    public class StaffDetailResponse
    {
        public int StaffId { get; set; }
        public bool IsActive { get; set; }
        public PersonDetailResponse? PersonInfo { get; set; }
    }

}
