using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Proxy.Host.Services;

namespace Proxy.Host.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly LogService _logService;

    public LogsController(LogService logService)
    {
        _logService = logService;
    }

    [HttpGet]
    public IActionResult GetLogs(
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        [FromQuery] string? clusterId = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] string? clientIp = null,
        [FromQuery] string? method = null)
    {
        try
        {
            var logs = _logService.GetLogs(limit, offset, clusterId, statusCode, clientIp, method).ToList();
            var total = _logService.GetTotalCount(clusterId, statusCode, clientIp, method);

            return Ok(new { Data = logs, Total = total });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message, Details = ex.ToString() });
        }
    }


    [HttpDelete("clear")]
    public IActionResult ClearLogs()
    {
        var count = _logService.ClearLogs();
        return Ok(new { Message = $"Cleared {count} logs." });
    }
}
