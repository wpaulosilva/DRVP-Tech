using ClosedXML.Excel;
using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Billing;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services
{
    public class BillingService
    {
        private readonly AppDbContext _context;
        private readonly AppSettingService _appSettings;

        public BillingService(AppDbContext context, AppSettingService appSettings)
        {
            _context  = context;
            _appSettings = appSettings;
        }


        public async Task<PagedBillingStudentResponse> GetStudentBillingAsync(
            int year, int month, string? search, int page, int pageSize)
        {
            decimal weekdayRate = await _appSettings.GetDecimalAsync(
                "class_price_weekday", 36.00m);
            decimal weekendRate = await _appSettings.GetDecimalAsync(
                "class_price_weekend", 43.50m);

          
            var classes = await _context.CoachClasses
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c =>
                    c.Status == (byte)CoachClassStatus.Validated &&
                    c.StartDatetime.Year  == year &&
                    c.StartDatetime.Month == month)
                .ToListAsync();

    
            var studentTotals = new Dictionary<int, (string Name, decimal HoursWeekday, decimal HoursWeekend, decimal Amount)>();

            foreach (var cls in classes)
            {
                decimal durationHours = DurationHours(cls);
                bool isWeekend = cls.StartDatetime.DayOfWeek is
                    DayOfWeek.Saturday or DayOfWeek.Sunday;
                decimal rate   = isWeekend ? weekendRate : weekdayRate;
                decimal amount = durationHours * rate;

                foreach (var p in cls.Participants)
                {
                    var student = p.IdStudentNavigation;
                    int sid = student.StudentId;
                    string name = ResolveStudentName(student);
                    string? nif = student.PersonInfo?.Nif ?? student.PersonInfo?.Nif;

                    if (studentTotals.TryGetValue(sid, out var existing))
                    {
                        if (isWeekend)
                            studentTotals[sid] = (name, existing.HoursWeekday, existing.HoursWeekend + durationHours, existing.Amount + amount);
                        else
                            studentTotals[sid] = (name, existing.HoursWeekday + durationHours, existing.HoursWeekend, existing.Amount + amount);
                    }
                    else
                    {
                        if (isWeekend)
                            studentTotals[sid] = (name, 0m, durationHours, amount);
                        else
                            studentTotals[sid] = (name, durationHours, 0m, amount);
                    }
                }
            }

            // Build the full row list (for summary totals + search + paging).
            // Fetch NIFs for all students in a single query to avoid per-row async calls
            var studentIds = studentTotals.Keys.ToList();
            var nifMap = await _context.Students
                .Where(s => studentIds.Contains(s.StudentId))
                .Select(s => new { s.StudentId, Nif = s.PersonInfo != null ? s.PersonInfo.Nif : null })
                .ToDictionaryAsync(x => x.StudentId, x => x.Nif!);

            var allRows = studentTotals
                .Select(kv => new BillingStudentRow
                {
                    StudentId      = kv.Key,
                    StudentName    = kv.Value.Name,
                    HoursWeekday   = Math.Round(kv.Value.HoursWeekday, 2),
                    HoursWeekend   = Math.Round(kv.Value.HoursWeekend, 2),
                    TotalAmount    = Math.Round(kv.Value.Amount, 2),
                    Nif            = nifMap.TryGetValue(kv.Key, out var nif) ? nif : null,
                    PaymentStatus  = null,
                    LastPaymentDate = null
                })
                .OrderBy(r => r.StudentName)
                .ToList();

            // Summary covers the entire month, not just the search slice.
            var summary = new BillingStudentSummary
            {
                TotalStudents = allRows.Count,
                TotalRevenue  = allRows.Sum(r => r.TotalAmount),
                TotalHours    = allRows.Sum(r => r.HoursCompleted),
                PendingCount  = 0
            };

            // Apply optional name search.
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                allRows = allRows
                    .Where(r => r.StudentName.ToLowerInvariant().Contains(term))
                    .ToList();
            }

            var pagedItems = allRows
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedBillingStudentResponse
            {
                Summary    = summary,
                Items      = pagedItems,
                TotalCount = allRows.Count,
                Page       = page,
                PageSize   = pageSize
            };
        }

        //  Coach billing 
        // Sums hours taught and school expense per coach across all Validated
        // classes in the requested month.

        public async Task<PagedBillingCoachResponse> GetCoachBillingAsync(
            int year, int month, string? search, int page, int pageSize)
        {
            decimal weekdayRate = await _appSettings.GetDecimalAsync(
                "class_price_weekday", 36.00m);
            decimal weekendRate = await _appSettings.GetDecimalAsync(
                "class_price_weekend", 43.50m);

            // One query: validated classes in the month, with coach → user info
            // and coach modalities.
            var classes = await _context.CoachClasses
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.IdModalities)
                .Where(c =>
                    c.Status == (byte)CoachClassStatus.Validated &&
                    c.StartDatetime.Year  == year &&
                    c.StartDatetime.Month == month)
                .ToListAsync();

            // Group by coach and accumulate hours + amount using weekday/weekend
            // rates (same pricing as student billing).
            var coachTotals = new Dictionary<int, (Coach Coach, decimal HoursWeekday, decimal HoursWeekend, decimal Amount)>();

            foreach (var cls in classes)
            {
                decimal durationHours = DurationHours(cls);
                bool isWeekend = cls.StartDatetime.DayOfWeek is
                    DayOfWeek.Saturday or DayOfWeek.Sunday;
                decimal rate   = isWeekend ? weekendRate : weekdayRate;
                decimal amount = durationHours * rate;

                int cid = cls.IdCoach;
                if (coachTotals.TryGetValue(cid, out var existing))
                {
                    if (isWeekend)
                        coachTotals[cid] = (existing.Coach, existing.HoursWeekday, existing.HoursWeekend + durationHours, existing.Amount + amount);
                    else
                        coachTotals[cid] = (existing.Coach, existing.HoursWeekday + durationHours, existing.HoursWeekend, existing.Amount + amount);
                }
                else
                {
                    if (isWeekend)
                        coachTotals[cid] = (cls.IdCoachNavigation, 0m, durationHours, amount);
                    else
                        coachTotals[cid] = (cls.IdCoachNavigation, durationHours, 0m, amount);
                }
            }

            var allRows = coachTotals
                .Select(kv =>
                {
                    var coach      = kv.Value.Coach;
                    var modalities = coach.IdModalities
                        .Select(m => m.Name)
                        .OrderBy(n => n)
                        .ToList();

                return new BillingCoachRow
                {
                    CoachId         = kv.Key,
                    CoachName       = ResolveCoachName(coach),
                    Modalities      = modalities,
                    HoursWeekday    = Math.Round(kv.Value.HoursWeekday, 2),
                    HoursWeekend    = Math.Round(kv.Value.HoursWeekend, 2),
                    TotalAmount     = Math.Round(kv.Value.Amount, 2),
                    Nif             = coach.CoachNavigation?.PersonInfo?.Nif,
                    PaymentStatus   = null,
                    LastPaymentDate = null
                };
                })
                .OrderBy(r => r.CoachName)
                .ToList();

            // Summary covers the entire month.
            var summary = new BillingCoachSummary
            {
                TotalCoaches = allRows.Count,
                TotalExpense = allRows.Sum(r => r.TotalAmount),
                TotalHours   = allRows.Sum(r => r.HoursTaught),
                PendingCount = 0
            };

            // Apply optional name search.
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                allRows = allRows
                    .Where(r => r.CoachName.ToLowerInvariant().Contains(term))
                    .ToList();
            }

            var pagedItems = allRows
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedBillingCoachResponse
            {
                Summary    = summary,
                Items      = pagedItems,
                TotalCount = allRows.Count,
                Page       = page,
                PageSize   = pageSize
            };
        }

        public async Task<byte[]> ExportStudentBillingAsync(
           int year, int month, string? search)
        {
            // Reuse existing logic — fetch all rows (no paging).
            var result = await GetStudentBillingAsync(
                year, month, search, page: 1, pageSize: int.MaxValue);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Faturação Alunos");

            // Title row
            ws.Cell(1, 1).Value = $"Faturação de Alunos — {year}-{month:D2}";
            ws.Range(1, 1, 1, 6).Merge();
            ws.Cell(1, 1).Style
                .Font.SetBold(true)
                .Font.SetFontSize(13)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Summary row
            ws.Cell(2, 1).Value = $"Total alunos: {result.Summary.TotalStudents}";
            ws.Cell(2, 3).Value = $"Total horas: {result.Summary.TotalHours:F2}";
            ws.Cell(2, 5).Value = "Total receita:";
            ws.Cell(2, 6).Value = result.Summary.TotalRevenue;
            ws.Cell(2, 6).Style.NumberFormat.Format = "#,##0.00 €";
            ws.Range(2, 1, 2, 6).Style.Font.SetItalic(true);

            // Header row
            ws.Cell(4, 1).Value = "Nome";
            ws.Cell(4, 2).Value = "NIF";
            ws.Cell(4, 3).Value = "Horas Semana";
            ws.Cell(4, 4).Value = "Horas Fim de Semana";
            ws.Cell(4, 5).Value = "Total Horas";
            ws.Cell(4, 6).Value = "Total (€)";

            ws.Range(4, 1, 4, 6).Style
                .Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#2D3748"))
                .Font.SetFontColor(XLColor.White)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Data rows
            int row = 5;
            foreach (var item in result.Items)
            {
                ws.Cell(row, 1).Value = item.StudentName;
                ws.Cell(row, 2).Value = item.Nif ?? "";
                ws.Cell(row, 3).Value = item.HoursWeekday;
                ws.Cell(row, 4).Value = item.HoursWeekend;
                ws.Cell(row, 5).Value = item.HoursCompleted;
                ws.Cell(row, 6).Value = item.TotalAmount;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 €";

                // Alternate row shading for readability.
                if (row % 2 == 0)
                    ws.Range(row, 1, row, 6).Style
                        .Fill.SetBackgroundColor(XLColor.FromHtml("#F7FAFC"));

                row++;
            }

            // Totals row
            ws.Cell(row, 1).Value = "TOTAL";
            ws.Cell(row, 5).FormulaA1 = $"=SUM(E5:E{row - 1})";
            ws.Cell(row, 6).FormulaA1 = $"=SUM(F5:F{row - 1})";
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 €";
            ws.Range(row, 1, row, 6).Style
                .Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#EDF2F7"));

            ws.Columns().AdjustToContents();
            ws.Column(4).Width = Math.Max(ws.Column(4).Width, 18);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        //  Coach billing export 
        // Returns a .xlsx byte array with all validated coach billing rows for
        // the requested month. Search filter is applied before building the sheet.

        public async Task<byte[]> ExportCoachBillingAsync(
            int year, int month, string? search)
        {
            // Reuse existing logic — fetch all rows (no paging).
            var result = await GetCoachBillingAsync(
                year, month, search, page: 1, pageSize: int.MaxValue);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Faturação Coaches");

            // Title row
            ws.Cell(1, 1).Value = $"Faturação de Coaches — {year}-{month:D2}";
            ws.Range(1, 1, 1, 7).Merge();
            ws.Cell(1, 1).Style
                .Font.SetBold(true)
                .Font.SetFontSize(13)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Summary row
            ws.Cell(2, 1).Value = $"Total coaches: {result.Summary.TotalCoaches}";
            ws.Cell(2, 3).Value = $"Total horas: {result.Summary.TotalHours:F2}";
            ws.Cell(2, 6).Value = "Total despesa:";
            ws.Cell(2, 7).Value = result.Summary.TotalExpense;
            ws.Cell(2, 7).Style.NumberFormat.Format = "#,##0.00 €";
            ws.Range(2, 1, 2, 7).Style.Font.SetItalic(true);

            // Header row
            ws.Cell(4, 1).Value = "Nome";
            ws.Cell(4, 2).Value = "NIF";
            ws.Cell(4, 3).Value = "Modalidades";
            ws.Cell(4, 4).Value = "Horas Semana";
            ws.Cell(4, 5).Value = "Horas Fim de Semana";
            ws.Cell(4, 6).Value = "Total Horas";
            ws.Cell(4, 7).Value = "Total (€)";

            ws.Range(4, 1, 4, 7).Style
                .Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#2D3748"))
                .Font.SetFontColor(XLColor.White)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Data rows
            int row = 5;
            foreach (var item in result.Items)
            {
                ws.Cell(row, 1).Value = item.CoachName;
                ws.Cell(row, 2).Value = item.Nif ?? "";
                ws.Cell(row, 3).Value = string.Join(", ", item.Modalities);
                ws.Cell(row, 4).Value = item.HoursWeekday;
                ws.Cell(row, 5).Value = item.HoursWeekend;
                ws.Cell(row, 6).Value = item.HoursTaught;
                ws.Cell(row, 7).Value = item.TotalAmount;
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00 €";

                // Alternate row shading for readability.
                if (row % 2 == 0)
                    ws.Range(row, 1, row, 7).Style
                        .Fill.SetBackgroundColor(XLColor.FromHtml("#F7FAFC"));

                row++;
            }

            // Totals row
            ws.Cell(row, 1).Value = "TOTAL";
            ws.Cell(row, 6).FormulaA1 = $"=SUM(F5:F{row - 1})";
            ws.Cell(row, 7).FormulaA1 = $"=SUM(G5:G{row - 1})";
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00 €";
            ws.Range(row, 1, row, 7).Style
                .Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#EDF2F7"));

            ws.Columns().AdjustToContents();
            ws.Column(3).Width = Math.Max(ws.Column(3).Width, 20);
            ws.Column(5).Width = Math.Max(ws.Column(5).Width, 18);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return stream.ToArray();
        }

        //  Private helpers 

        private static decimal DurationHours(CoachClass cls) =>
            (decimal)(cls.EndDatetime - cls.StartDatetime).TotalMinutes / 60.0m;

        private static string ResolveStudentName(Student student)
        {
            var p = student.PersonInfo;
            return p is not null
                ? $"{p.FirstName} {p.LastName}".Trim()
                : $"Student {student.StudentId}";
        }

        private static string ResolveCoachName(Coach coach)
        {
            var p = coach.CoachNavigation?.PersonInfo;
            return p is not null
                ? $"{p.FirstName} {p.LastName}".Trim()
                : coach.CoachNavigation?.Username ?? $"Coach {coach.CoachId}";
        }
    }
}
