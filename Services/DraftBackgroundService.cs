namespace VacationTracker.Services;

public class DraftBackgroundService : BackgroundService
{
    private readonly IDraftService _draftService;
    private readonly ILogger<DraftBackgroundService> _logger;

    public DraftBackgroundService(IDraftService draftService, ILogger<DraftBackgroundService> logger)
    {
        _draftService = draftService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _draftService.ProcessScheduledDraftsAsync();
                await _draftService.ProcessTurnTimeoutAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing draft timeout");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
