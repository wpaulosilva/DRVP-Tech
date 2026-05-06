namespace DanceSchoolApp.Server.Models;

public class RefreshToken
{
    public int TokenId { get; set; }
    public int IdUser { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public virtual User IdUserNavigation { get; set; } = null!;
}
