using System.ComponentModel.DataAnnotations;

namespace Proxy.Host.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [MinLength(1, ErrorMessage = "Username is required.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(1, ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Old password is required.")]
    public string OldPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
    public string NewPassword { get; set; } = string.Empty;
}
