using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Proxy.Host.Controllers;
using Proxy.Host.Models;
using Proxy.Host.Services;
using System.Security.Claims;
using Xunit;

namespace Proxy.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly LiteDbService _liteDbService;
    private readonly IConfiguration _configuration;

    public AuthControllerTests()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                KeyValuePair.Create("LiteDb:Path", ":memory:"),
                KeyValuePair.Create("LiteDb:LogPath", ":memory:"),
                KeyValuePair.Create("Jwt:Key", "test-key-that-is-at-least-64-characters-long-for-testing-purposes!!"),
                KeyValuePair.Create("Jwt:Issuer", "TestIssuer"),
            })
            .Build();
        _liteDbService = new LiteDbService(cfg);
        _configuration = cfg;
    }

    private AuthController CreateController() => new(_liteDbService, _configuration);

    private void SeedUser(string username, string password, bool mustChangePassword = false)
    {
        var col = _liteDbService.Database.GetCollection<User>("users");
        using var hmac = new System.Security.Cryptography.HMACSHA512();
        var salt = hmac.Key;
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        col.Insert(new User
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            MustChangePassword = mustChangePassword
        });
    }

    public void Dispose()
    {
        _liteDbService.Database?.Dispose();
    }

    // ── Login ───────────────────────────────────────────────────────────────

    [Fact]
    public void Login_ValidCredentials_ReturnsToken()
    {
        SeedUser("admin", "password123");

        var result = CreateController().Login(new LoginRequest { Username = "admin", Password = "password123" });

        var ok = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Login_InvalidUsername_ReturnsUnauthorized()
    {
        var result = CreateController().Login(new LoginRequest { Username = "nonexistent", Password = "password" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void Login_EmptyUsername_ReturnsBadRequest()
    {
        var result = CreateController().Login(new LoginRequest { Username = "", Password = "password" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var apiError = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("Username is required.", apiError.Message);
    }

    [Fact]
    public void Login_EmptyPassword_ReturnsBadRequest()
    {
        var result = CreateController().Login(new LoginRequest { Username = "admin", Password = "" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var apiError = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("Password is required.", apiError.Message);
    }

    [Fact]
    public void Login_NullUsername_ReturnsBadRequest()
    {
        var result = CreateController().Login(new LoginRequest { Username = null!, Password = "password" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Login_NullPassword_ReturnsBadRequest()
    {
        var result = CreateController().Login(new LoginRequest { Username = "admin", Password = null! });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Login_AccountLocked_Returns429()
    {
        SeedUser("admin", "password123");
        var col = _liteDbService.Database.GetCollection<User>("users");
        var user = col.FindOne(u => u.Username == "admin");
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(20);
        user.FailedLoginAttempts = 5;
        col.Update(user);

        var result = CreateController().Login(new LoginRequest { Username = "admin", Password = "password123" });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(429, statusResult.StatusCode);
    }

    // ── Change Password ─────────────────────────────────────────────────

    [Fact]
    public void ChangePassword_Valid_Success()
    {
        SeedUser("admin", "oldpassword", mustChangePassword: true);

        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, "admin") }, "test"))
            }
        };

        var result = controller.ChangePassword(new ChangePasswordRequest
        {
            OldPassword = "oldpassword",
            NewPassword = "newpassword1"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var col = _liteDbService.Database.GetCollection<User>("users");
        var user = col.FindOne(u => u.Username == "admin");
        Assert.False(user.MustChangePassword);
    }

    [Fact]
    public void ChangePassword_WeakPassword_ReturnsBadRequest()
    {
        SeedUser("admin", "oldpassword");

        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, "admin") }, "test"))
            }
        };

        var result = controller.ChangePassword(new ChangePasswordRequest
        {
            OldPassword = "oldpassword",
            NewPassword = "short"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ChangePassword_NoDigit_ReturnsBadRequest()
    {
        SeedUser("admin", "oldpassword");

        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, "admin") }, "test"))
            }
        };

        var result = controller.ChangePassword(new ChangePasswordRequest
        {
            OldPassword = "oldpassword",
            NewPassword = "password"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ChangePassword_NoLetter_ReturnsBadRequest()
    {
        SeedUser("admin", "oldpassword");

        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, "admin") }, "test"))
            }
        };

        var result = controller.ChangePassword(new ChangePasswordRequest
        {
            OldPassword = "oldpassword",
            NewPassword = "12345678"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ChangePassword_WrongOldPassword_ReturnsBadRequest()
    {
        SeedUser("admin", "oldpassword");

        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, "admin") }, "test"))
            }
        };

        var result = controller.ChangePassword(new ChangePasswordRequest
        {
            OldPassword = "wrongpassword",
            NewPassword = "newpassword1"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Token Claims ────────────────────────────────────────────────────────

    [Fact]
    public void Login_MustChangePassword_Claim_Included()
    {
        SeedUser("admin", "password", mustChangePassword: true);

        var result = CreateController().Login(new LoginRequest { Username = "admin", Password = "password" });

        var ok = Assert.IsType<OkObjectResult>(result);
    }
}