using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Scheduling;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DanceSchoolApp.Server.Services.Scheduling
{
    public class PortugueseHolidayService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PortugueseHolidayService> _logger;

        private const string NAGER_DATE_API = "https://date.nager.at/api/v3/publicholidays";
        private const int PORTUGAL_COUNTRY_CODE = 5; // ISO 3166-1 numeric code for PT

        public PortugueseHolidayService(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<PortugueseHolidayService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Fetches Portuguese public holidays for a given year from Nager.Date API
        /// and creates BlockedPeriod records for them with Holiday scope.
        /// </summary>
        public async Task SyncHolidaysForYearAsync(int year)
        {
            try
            {
                var holidays = await FetchPortugueseHolidaysAsync(year);

                if (!holidays.Any())
                {
                    _logger.LogWarning($"No holidays found for Portugal in {year}");
                    return;
                }

                foreach (var holiday in holidays)
                {
                    await CreateOrUpdateHolidayBlockAsync(holiday);
                }

                _logger.LogInformation($"Successfully synced {holidays.Count} Portuguese holidays for {year}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error syncing Portuguese holidays for {year}");
                throw;
            }
        }

        /// <summary>
        /// Syncs holidays for the current year and next year.
        /// Call this once per year (e.g., on January 1st) to keep holidays up-to-date.
        /// </summary>
        public async Task SyncCurrentAndNextYearAsync()
        {
            var currentYear = DateTime.UtcNow.Year;

            await SyncHolidaysForYearAsync(currentYear);
            await SyncHolidaysForYearAsync(currentYear + 1);
        }

        /// <summary>
        /// Removes holidays from years before the current year.
        /// Ensures we only keep 2 years of holidays (current + next).
        /// </summary>
        public async Task CleanupOldHolidaysAsync()
        {
            try
            {
                var currentYear = DateTime.UtcNow.Year;

                var oldHolidays = await _context.BlockedPeriods
                    .Where(b => 
                        b.Scope == (byte)BlockedPeriodScope.Holiday &&
                        b.StartDatetime.Year < currentYear)
                    .ToListAsync();

                if (oldHolidays.Any())
                {
                    _context.BlockedPeriods.RemoveRange(oldHolidays);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Cleaned up {oldHolidays.Count} old holiday records from years before {currentYear}");
                }
                else
                {
                    _logger.LogInformation("No old holidays to cleanup");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old holidays");
                throw;
            }
        }

        /// <summary>
        /// Fetches Portuguese public holidays from the Nager.Date API.
        /// Returns a list of (date, name) tuples.
        /// </summary>
        private async Task<List<(DateTime Date, string Name)>> FetchPortugueseHolidaysAsync(int year)
        {
            var holidays = new List<(DateTime Date, string Name)>();

            try
            {
                using var client = _httpClientFactory.CreateClient();
                var url = $"{NAGER_DATE_API}/{year}/PT";

                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var dateStr = element.GetProperty("date").GetString();
                    var name = element.GetProperty("name").GetString();

                    if (DateTime.TryParse(dateStr, out var date))
                    {
                        holidays.Add((date, name ?? "Portuguese Public Holiday"));
                    }
                }

                _logger.LogInformation($"Fetched {holidays.Count} holidays for Portugal in {year} from Nager.Date API");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch holidays from Nager.Date API");
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Nager.Date API response");
                throw;
            }

            return holidays;
        }

        /// <summary>
        /// Creates or updates a BlockedPeriod for a holiday.
        /// Uses the holiday date as both start and end (full day block).
        /// Checks if a holiday block already exists to avoid duplicates.
        /// </summary>
        private async Task CreateOrUpdateHolidayBlockAsync((DateTime Date, string Name) holiday)
        {
            // Set times to cover the full day (00:00 to 23:59:59)
            var startDatetime = holiday.Date.Date;
            var endDatetime = holiday.Date.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

            // Check if a holiday block already exists for this date with the same name
            var existingBlock = await _context.BlockedPeriods
                .FirstOrDefaultAsync(b =>
                    b.Scope == (byte)BlockedPeriodScope.Holiday &&
                    b.StartDatetime.Date == startDatetime.Date &&
                    b.Reason == holiday.Name);

            if (existingBlock != null)
            {
                // Update existing block
                existingBlock.EndDatetime = endDatetime;
                _context.BlockedPeriods.Update(existingBlock);
                _logger.LogInformation($"Updated holiday block for {holiday.Name} on {holiday.Date:yyyy-MM-dd}");
            }
            else
            {
                // Create new block
                var blockedPeriod = new BlockedPeriod
                {
                    Scope = (byte)BlockedPeriodScope.Holiday,
                    StartDatetime = startDatetime,
                    EndDatetime = endDatetime,
                    Reason = holiday.Name,
                    IdCoach = null,
                    IdStudio = null
                };

                _context.BlockedPeriods.Add(blockedPeriod);
                _logger.LogInformation($"Created holiday block for {holiday.Name} on {holiday.Date:yyyy-MM-dd}");
            }

            await _context.SaveChangesAsync();
        }
    }
}
