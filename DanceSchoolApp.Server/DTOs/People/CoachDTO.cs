namespace DanceSchoolApp.Server.DTOs.People
{
    //  Responses 

    public class ModalitySummary
    {
        public int    ModalityId { get; set; }
        public string Name       { get; set; } = null!;
    }

    public class CoachMeResponse
    {
        public int     CoachId    { get; set; }
        public string? Biography  { get; set; }
        public string? Title      { get; set; }
        public string? PhotoUrl   { get; set; }
        public bool    IsActive   { get; set; }
        public string  Name       { get; set; } = null!;
        public string? Email      { get; set; }
        public List<ModalitySummary> Modalities { get; set; } = new();
    }

    public class CoachAvailableResponse
    {
        public int    CoachId    { get; set; }
        public string Name       { get; set; } = null!;
        public List<ModalitySummary> Modalities { get; set; } = new();
    }

    public class CoachListResponse
    {
        public int CoachId { get; set; }
        public string Name { get; set; } = null!;
        public string? Biography { get; set; }
        public string? PhotoUrl { get; set; }
        public bool IsActive { get; set; }
        public PersonListResponse? PersonInfo { get; set; }
    }
    public class CoachDetailResponse
    {
        public int CoachId { get; set; }
        public string? Biography { get; set; }
        public string? PhotoUrl { get; set; }
        public bool IsActive { get; set; }
        public PersonDetailResponse? PersonInfo { get; set; }
    }

}
