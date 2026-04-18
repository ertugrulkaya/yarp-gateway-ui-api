namespace Proxy.Host.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
    public bool MustChangePassword { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
}
