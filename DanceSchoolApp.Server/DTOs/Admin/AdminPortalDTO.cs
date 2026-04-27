namespace DanceSchoolApp.Server.DTOs.Admin
{
    //  Dashboard 

    public class AdminDashboardResponse
    {
        public int TotalStaffAccounts  { get; set; }
        public int ActiveStaffAccounts { get; set; }
    }

    //  Users list 

    public class AdminUserRow
    {
        public int     UserId   { get; set; }
        public string  Name     { get; set; } = null!;
        public string? Email    { get; set; }
        public bool    IsActive { get; set; }
    }
}
