using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Proxy.Host.Models;
using Proxy.Host.Services;

namespace Proxy.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly LogService _logService;
    private readonly IWebHostEnvironment _env;

    public LogsController(LogService logService, IWebHostEnvironment env)
    {
        _logService = logService;
        _env = env;
    }

    [HttpGet]
    public IActionResult GetLogs(
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        [FromQuery] string? clusterId = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] string? clientIp = null,
        [FromQuery] string? method = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null)
    {
        try
        {
            if (limit < 1) limit = 100;
            if (limit > 1000) limit = 1000;
            if (offset < 0) offset = 0;

            var logs = _logService.GetLogs(limit, offset, clusterId, statusCode, clientIp, method, sortBy, sortDir).ToList();
            var total = _logService.GetTotalCount(clusterId, statusCode, clientIp, method);

            return Ok(new { Data = logs, Total = total });
        }
        catch (Exception ex)
        {
            var message = _env.IsDevelopment() ? ex.Message : "An unexpected error occurred.";
            return StatusCode(500, new ApiError("INTERNAL_SERVER_ERROR", message));
        }
    }


    [HttpDelete("clear")]
    public IActionResult ClearLogs()
    {
        var count = _logService.ClearLogs();
        return Ok(new { Message = $"Cleared {count} logs." });
    }
}
