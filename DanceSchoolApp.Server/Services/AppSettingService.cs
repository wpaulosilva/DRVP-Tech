using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services
{
    public class AppSettingService
    {
        private readonly AppDbContext _context;

        // Keys seeded on first access if absent.
        private static readonly Dictionary<string, string> _defaults = new()
        {
            ["validation_window_hours"] = "48",
            ["class_price_weekday"]     = "36.00",
            ["class_price_weekend"]     = "43.20",
            ["max_participants"]        = "8"
        };

        public AppSettingService(AppDbContext context)
        {
            _context = context;
        }

        //  Queries 

        public async Task<string?> GetValueAsync(string key)
        {
            var setting = await GetOrCreateAsync(key);
            return setting?.SettingValue;
        }

        public async Task<int> GetIntAsync(string key, int defaultValue)
        {
            var raw = await GetValueAsync(key);
            return int.TryParse(raw, out var parsed) ? parsed : defaultValue;
        }

        public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue)
        {
            var raw = await GetValueAsync(key);
            return decimal.TryParse(raw,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) ? parsed : defaultValue;
        }

        public async Task<List<AppSettingResponse>> GetAllAsync()
        {
            // Ensure all default keys exist before returning the full list.
            foreach (var key in _defaults.Keys)
                await GetOrCreateAsync(key);

            return await _context.AppSettings
                .Select(s => new AppSettingResponse
                {
                    SettingId = s.SettingId,
                    Key       = s.SettingKey,
                    Value     = s.SettingValue,
                    UpdatedAt = s.UpdatedAt
                })
                .ToListAsync();
        }

        //  Commands 

        public async Task UpdateAsync(string key, string value)
        {
            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (setting is null)
                throw new KeyNotFoundException(
                    $"Setting with key '{key}' was not found.");

            setting.SettingValue = value;
            setting.UpdatedAt    = DateOnly.FromDateTime(DateTime.Now);
            await _context.SaveChangesAsync();
        }

        //  Private helpers 

        // Returns the existing row or inserts the default and returns the new row.
        private async Task<AppSetting?> GetOrCreateAsync(string key)
        {
            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (setting is not null)
                return setting;

            if (!_defaults.TryGetValue(key, out var defaultValue))
                return null;

            setting = new AppSetting
            {
                SettingKey   = key,
                SettingValue = defaultValue,
                UpdatedAt    = DateOnly.FromDateTime(DateTime.Now)
            };

            _context.AppSettings.Add(setting);
            await _context.SaveChangesAsync();

            return setting;
        }
    }
}
