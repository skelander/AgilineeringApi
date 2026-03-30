namespace AgilineeringApi.Models;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public ICollection<Post> Posts { get; set; } = [];
}
