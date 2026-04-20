using System.Text.Json;

namespace Proxy.Host.Services;

public class LogWriterService : BackgroundService
{
    private readonly LogService _logService;
    private readonly ILogger<LogWriterService> _logger;
    private readonly string _failedLogPath;

    public LogWriterService(LogService logService, ILogger<LogWriterService> logger)
    {
        _logService = logService;
        _logger = logger;
        _failedLogPath = Path.Combine(AppContext.BaseDirectory, "data", "failed-logs.jsonl");
        var dir = Path.GetDirectoryName(_failedLogPath);
        if (dir != null) Directory.CreateDirectory(dir);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LogWriterService started.");
        try
        {
            await foreach (var entry in _logService.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    _logService.WriteToDb(entry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WriteToDb failed for entry {Path} {Method}", entry.Path, entry.Method);
                    await WriteFailedEntryToFile(entry);
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "LogWriterService crashed — no more logs will be written!");
        }
        _logger.LogInformation("LogWriterService stopped.");
    }

    private async Task WriteFailedEntryToFile(Models.LogEntry entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry);
            await File.AppendAllTextAsync(_failedLogPath, json + Environment.NewLine);
        }
        catch
        {
            _logger.LogCritical("Failed to write failed entry to backup file. Entry lost.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logService.CompleteChannel();

        using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, drainCts.Token);
        try { await _logService.Reader.Completion.WaitAsync(linked.Token); }
        catch (OperationCanceledException) { /* timeout — accept partial drain */ }

        await base.StopAsync(cancellationToken);
    }
}
