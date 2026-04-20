using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Proxy.Host.Models;
using Microsoft.IdentityModel.Tokens;
using Proxy.Host.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Proxy.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly LiteDbService _liteDbService;
    private readonly IConfiguration _configuration;

    public AuthController(LiteDbService liteDbService, IConfiguration configuration)
    {
        _liteDbService = liteDbService;
        _configuration = configuration;
    }

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    [HttpPost("login")]
    [EnableRateLimiting("LoginPolicy")]
    public IActionResult Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new ApiError("BAD_REQUEST", "Username is required."));
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new ApiError("BAD_REQUEST", "Password is required."));

        var users = _liteDbService.Database.GetCollection<User>("users");
        var user = users.FindOne(x => x.Username == request.Username);

        // Unknown user — return generic message (no user enumeration)
        if (user == null)
            return Unauthorized(new ApiError("UNAUTHORIZED", "Invalid credentials."));

        // Account locked?
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes);
            return StatusCode(429, new ApiError("TOO_MANY_REQUESTS", $"Account locked. Try again in {remaining} minute(s)."));
        }

        if (!VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.Add(LockoutDuration);
                users.Update(user);
                return StatusCode(429, new ApiError("TOO_MANY_REQUESTS", $"Too many failed attempts. Account locked for {(int)LockoutDuration.TotalMinutes} minutes."));
            }
            users.Update(user);
            return Unauthorized(new ApiError("UNAUTHORIZED", "Invalid credentials."));
        }

        // Success — reset lockout state
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        users.Update(user);

        string token = CreateToken(user);
        return Ok(new { Token = token, MustChangePassword = user.MustChangePassword });
    }

    [HttpPost("change-password")]
    [Authorize]
    public IActionResult ChangePassword(ChangePasswordRequest request)
    {
        if (!IsPasswordComplexEnough(request.NewPassword))
            return BadRequest(new ApiError("BAD_REQUEST", "Password must be at least 8 characters and contain at least one letter and one digit."));

        var username = User.FindFirstValue(ClaimTypes.Name);
        if (username == null) return Unauthorized(new ApiError("UNAUTHORIZED", "Token is missing or invalid."));

        var users = _liteDbService.Database.GetCollection<User>("users");
        var user = users.FindOne(x => x.Username == username);

        if (user == null || !VerifyPasswordHash(request.OldPassword, user.PasswordHash, user.PasswordSalt))
            return BadRequest(new ApiError("BAD_REQUEST", "Invalid old password."));

        CreatePasswordHash(request.NewPassword, out byte[] hash, out byte[] salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = false;
        users.Update(user);

        string newToken = CreateToken(user);
        return Ok(new { Message = "Password updated successfully", Token = newToken });
    }

    private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
    {
        using (var hmac = new HMACSHA512(passwordSalt))
        {
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return computedHash.SequenceEqual(passwordHash);
        }
    }

    private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using (var hmac = new HMACSHA512())
        {
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }

    private static bool IsPasswordComplexEnough(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return false;
        bool hasLetter = false, hasDigit = false;
        foreach (var c in password)
        {
            if (char.IsLetter(c)) hasLetter = true;
            else if (char.IsDigit(c)) hasDigit = true;
            if (hasLetter && hasDigit) return true;
        }
        return false;
    }

    private string CreateToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSettings.GetValue<string>("Key")!);
        
        List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, "Admin")
        };
        if (user.MustChangePassword)
            claims.Add(new Claim("must_change_password", "true"));

        var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha512Signature);

        var token = new JwtSecurityToken(
            issuer: jwtSettings.GetValue<string>("Issuer") ?? "AntiGravityProxyAuth",
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return jwt;
    }
}
