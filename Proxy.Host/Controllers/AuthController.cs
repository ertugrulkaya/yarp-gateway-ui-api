using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Proxy.Host.Models;
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

    [HttpPost("login")]
    public IActionResult Login(LoginRequest request)
    {
        var users = _liteDbService.Database.GetCollection<User>("users");
        var user = users.FindOne(x => x.Username == request.Username);

        if (user == null || !VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Unauthorized("Invalid credentials.");
        }

        string token = CreateToken(user);
        return Ok(new { Token = token });
    }

    [HttpPost("change-password")]
    [Authorize]
    public IActionResult ChangePassword(ChangePasswordRequest request)
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        if (username == null) return Unauthorized();

        var users = _liteDbService.Database.GetCollection<User>("users");
        var user = users.FindOne(x => x.Username == username);

        if (user == null || !VerifyPasswordHash(request.OldPassword, user.PasswordHash, user.PasswordSalt))
        {
            return BadRequest("Invalid old password.");
        }

        CreatePasswordHash(request.NewPassword, out byte[] hash, out byte[] salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        users.Update(user);

        return Ok(new { Message = "Password updated successfully" });
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

    private string CreateToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSettings.GetValue<string>("Key") ?? "SuperSecretHmacKeyForProxyManager2026!++AndItNeedsToBeAtLeast64CharactersLongToWorkWithSha512");
        
        List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, "Admin")
        };

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
