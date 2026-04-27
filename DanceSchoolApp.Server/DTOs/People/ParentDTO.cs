namespace DanceSchoolApp.Server.DTOs.People
{

    //  Responses 

    public class ChildSummary
    {
        public int    ChildId { get; set; }
        public string Name    { get; set; } = null!;
    }

    public class ParentMeResponse
    {
        public int     ParentId { get; set; }
        public string  Username { get; set; } = null!;
        public string  Name     { get; set; } = null!;
        public string? Email    { get; set; }
        public List<ChildSummary> Children { get; set; } = new();
    }

    public class ParentListResponse
    {
        public int ParentId { get; set; }
        public bool IsActive { get; set; }
        public PersonListResponse? PersonInfo { get; set; }
    }
    public class ParentDetailResponse
    {
        public int ParentId { get; set; }
        public bool IsActive { get; set; }
        public PersonDetailResponse? PersonInfo { get; set; }
    }

}
