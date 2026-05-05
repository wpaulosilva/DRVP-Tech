namespace DanceSchoolApp.Server.Services.Scheduling
{
    /// <summary>
    /// Background service that syncs Portuguese public holidays annually.
    /// Runs once per year on January 1st at 02:00 AM UTC.
    /// 
    /// Design rationale:
    /// - Annual sync is sufficient: holidays don't change frequently
    /// - Maintains 2 years of holidays (current + next)
    /// - Automatically cleans up old holidays (before current year)
    /// - Minimal resource usage: only 1 execution per year
    /// </summary>
    public class HolidaySyncWorker : BackgroundService
    {
        private readonly ILogger<HolidaySyncWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Sync at 2:00 AM UTC on January 1st
        private readonly TimeSpan _syncTime = new TimeSpan(2, 0, 0);
        private readonly int _syncMonth = 1;    // January
        private readonly int _syncDay = 1;      // 1st

        public HolidaySyncWorker(
            ILogger<HolidaySyncWorker> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("HolidaySyncWorker started - Annual sync on January 1st at 02:00 AM UTC");

            // Initial check: if we're already past Jan 1st this year, wait until next year
            var now = DateTime.UtcNow;
            var nextSync = CalculateNextSyncDate(now);
            _logger.LogInformation($"Next holiday sync scheduled for {nextSync:yyyy-MM-dd HH:mm:ss} UTC");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    now = DateTime.UtcNow;
                    nextSync = CalculateNextSyncDate(now);

                    var timeUntilNextSync = nextSync - now;
                    _logger.LogInformation($"Waiting {timeUntilNextSync.TotalDays:F0} days until next sync on {nextSync:yyyy-MM-dd HH:mm:ss} UTC");

                    // Wait until the next sync time
                    await Task.Delay(timeUntilNextSync, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await SyncHolidaysAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("HolidaySyncWorker cancellation requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in HolidaySyncWorker");
                    // If sync fails, try again in 24 hours instead of 5 minutes
                    // (since we only sync once per year, we want to be sure it succeeds)
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
            }

            _logger.LogInformation("HolidaySyncWorker stopped");
        }

        /// <summary>
        /// Calculates the next sync date (January 1st at 2:00 AM UTC).
        /// If we're already past that date this year, returns next year's date.
        /// </summary>
        private DateTime CalculateNextSyncDate(DateTime now)
        {
            var syncDate = new DateTime(now.Year, _syncMonth, _syncDay, _syncTime.Hours, _syncTime.Minutes, 0, DateTimeKind.Utc);

            if (now > syncDate)
            {
                // We've already passed Jan 1st this year, sync next year
                syncDate = syncDate.AddYears(1);
            }

            return syncDate;
        }

        private async Task SyncHolidaysAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var holidayService = scope.ServiceProvider.GetRequiredService<PortugueseHolidayService>();

                _logger.LogInformation("Starting annual Portuguese holidays synchronization (Jan 1st)...");

                // Sync current and next year
                await holidayService.SyncCurrentAndNextYearAsync();

                // Clean up holidays from years before the current year
                await holidayService.CleanupOldHolidaysAsync();

                _logger.LogInformation("Annual Portuguese holidays synchronization completed successfully");
                _logger.LogInformation($"Next sync scheduled for: {CalculateNextSyncDate(DateTime.UtcNow):yyyy-MM-dd HH:mm:ss} UTC");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync Portuguese holidays");
            }
        }
    }
}
