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
        private readonly Microsoft.Extensions.Logging.ILogger<BillingService> _logger;

        public BillingService(AppDbContext context, AppSettingService appSettings, Microsoft.Extensions.Logging.ILogger<BillingService> logger)
        {
            _context = context;
            _appSettings = appSettings;
            _logger = logger;
        }

        public async Task<PagedBillingStudentResponse> GetStudentBillingAsync(
            int year,
            int month,
            string? search,
            int page,
            int pageSize)
        {
            decimal weekdayRate = await _appSettings.GetDecimalAsync(
                "class_price_weekday",
                36.00m);

            decimal weekendRate = await _appSettings.GetDecimalAsync(
                "class_price_weekend",
                43.50m);

            var classes = await _context.CoachClasses
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c =>
                    c.Status == (byte)CoachClassStatus.Validated &&
                    c.StartDatetime.Year == year &&
                    c.StartDatetime.Month == month)
                .ToListAsync();

            var studentTotals = new Dictionary<int, (string Name, decimal HoursWeekday, decimal HoursWeekend, decimal Amount)>();

            foreach (var cls in classes)
            {
                decimal durationHours = DurationHours(cls);
                bool isSundayOrHoliday = IsSundayOrHoliday(cls.StartDatetime);

                foreach (var p in cls.Participants)
                {
                    // Per-participant price takes precedence; null = fall back to app-settings rates.
                    // This preserves correct totals for legacy rows that predate the price field.
                    decimal appliedRate = p.PerParticipantPrice ?? (isSundayOrHoliday ? weekendRate : weekdayRate);
                    decimal amount = durationHours * appliedRate;

                    var student = p.IdStudentNavigation;
                    int studentId = student.StudentId;
                    string name = ResolveStudentName(student);

                    if (studentTotals.TryGetValue(studentId, out var existing))
                    {
                        if (isSundayOrHoliday)
                        {
                            studentTotals[studentId] = (
                                name,
                                existing.HoursWeekday,
                                existing.HoursWeekend + durationHours,
                                existing.Amount + amount);
                        }
                        else
                        {
                            studentTotals[studentId] = (
                                name,
                                existing.HoursWeekday + durationHours,
                                existing.HoursWeekend,
                                existing.Amount + amount);
                        }
                    }
                    else
                    {
                        if (isSundayOrHoliday)
                        {
                            studentTotals[studentId] = (name, 0m, durationHours, amount);
                        }
                        else
                        {
                            studentTotals[studentId] = (name, durationHours, 0m, amount);
                        }
                    }
                }
            }

            var studentIds = studentTotals.Keys.ToList();

            // build parent/guardian info by loading students with their ParentUser and PersonInfo
            var studentsWithParents = await _context.Students
                .Where(s => studentIds.Contains(s.StudentId))
                .Include(s => s.ParentUser)
                    .ThenInclude(u => u.PersonInfo)
                .Include(s => s.PersonInfo)
                .ToListAsync();

            var parentMap = new List<object>();
            var nifMap = new Dictionary<int, string?>();
            var responsibleNameMap = new Dictionary<int, string?>();
            var responsibleNifMap = new Dictionary<int, string?>();

            foreach (var s in studentsWithParents)
            {
                var studentNif = s.PersonInfo != null ? s.PersonInfo.Nif : null;
                string? parentUsername = s.ParentUser?.Username;
                string? responsibleName = null;
                string? responsibleNif = null;

                if (s.ParentUser?.PersonInfo != null)
                {
                    responsibleName = (s.ParentUser.PersonInfo.FirstName + " " + s.ParentUser.PersonInfo.LastName).Trim();
                    responsibleNif = s.ParentUser.PersonInfo.Nif;
                }

                // Fallback to username if no person info
                if (string.IsNullOrWhiteSpace(responsibleName) && !string.IsNullOrWhiteSpace(parentUsername))
                    responsibleName = parentUsername;

                nifMap[s.StudentId] = studentNif;
                responsibleNameMap[s.StudentId] = responsibleName;
                responsibleNifMap[s.StudentId] = responsibleNif;

                parentMap.Add(new { s.StudentId, StudentNif = studentNif, ParentUsername = parentUsername, ResponsibleName = responsibleName, ResponsibleNif = responsibleNif });
            }

            var allRows = studentTotals
                .Select(kv => new BillingStudentRow
                {
                    StudentId = kv.Key,
                    StudentName = kv.Value.Name,
                    HoursWeekday = Math.Round(kv.Value.HoursWeekday, 2),
                    HoursWeekend = Math.Round(kv.Value.HoursWeekend, 2),
                    TotalAmount = Math.Round(kv.Value.Amount, 2),
                    Nif = nifMap.TryGetValue(kv.Key, out var nif) ? nif : null,
                    ResponsibleName = responsibleNameMap.TryGetValue(kv.Key, out var rn) ? rn : null,
                    ResponsibleNif = responsibleNifMap.TryGetValue(kv.Key, out var rnf) ? rnf : null,
                    PaymentStatus = null,
                    LastPaymentDate = null
                })
                .OrderBy(r => r.StudentName)
                .ToList();

            // Debug logging: show what parentMap and allRows contain to aid diagnosis
            try
            {
                _logger?.LogDebug("BillingService parentMap: {ParentMap}", parentMap);
                _logger?.LogDebug("BillingService allRows: {AllRows}", allRows);
            }
            catch { }

            var summary = new BillingStudentSummary
            {
                TotalStudents = allRows.Count,
                TotalRevenue = allRows.Sum(r => r.TotalAmount),
                TotalHours = allRows.Sum(r => r.HoursCompleted),
                PendingCount = 0
            };

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
                Summary = summary,
                Items = pagedItems,
                TotalCount = allRows.Count,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedBillingCoachResponse> GetCoachBillingAsync(
            int year,
            int month,
            string? search,
            int page,
            int pageSize)
        {
            decimal weekdayRate = await _appSettings.GetDecimalAsync(
                "class_price_weekday",
                36.00m);

            decimal weekendRate = await _appSettings.GetDecimalAsync(
                "class_price_weekend",
                43.50m);

            var classes = await _context.CoachClasses
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.IdModalities)
                .Where(c =>
                    c.Status == (byte)CoachClassStatus.Validated &&
                    c.StartDatetime.Year == year &&
                    c.StartDatetime.Month == month)
                .ToListAsync();

            var coachTotals = new Dictionary<int, (Coach Coach, decimal HoursWeekday, decimal HoursWeekend, decimal Amount)>();

            foreach (var cls in classes)
            {
                decimal durationHours = DurationHours(cls);
                bool isSundayOrHoliday = IsSundayOrHoliday(cls.StartDatetime);

                decimal rate = isSundayOrHoliday
                    ? weekendRate
                    : weekdayRate;

                decimal amount = durationHours * rate;

                int coachId = cls.IdCoach;

                if (coachTotals.TryGetValue(coachId, out var existing))
                {
                    if (isSundayOrHoliday)
                    {
                        coachTotals[coachId] = (
                            existing.Coach,
                            existing.HoursWeekday,
                            existing.HoursWeekend + durationHours,
                            existing.Amount + amount);
                    }
                    else
                    {
                        coachTotals[coachId] = (
                            existing.Coach,
                            existing.HoursWeekday + durationHours,
                            existing.HoursWeekend,
                            existing.Amount + amount);
                    }
                }
                else
                {
                    if (isSundayOrHoliday)
                    {
                        coachTotals[coachId] = (
                            cls.IdCoachNavigation,
                            0m,
                            durationHours,
                            amount);
                    }
                    else
                    {
                        coachTotals[coachId] = (
                            cls.IdCoachNavigation,
                            durationHours,
                            0m,
                            amount);
                    }
                }
            }

            var allRows = coachTotals
                .Select(kv =>
                {
                    var coach = kv.Value.Coach;

                    var modalities = coach.IdModalities
                        .Select(m => m.Name)
                        .OrderBy(n => n)
                        .ToList();

                    return new BillingCoachRow
                    {
                        CoachId = kv.Key,
                        CoachName = ResolveCoachName(coach),
                        Modalities = modalities,
                        HoursWeekday = Math.Round(kv.Value.HoursWeekday, 2),
                        HoursWeekend = Math.Round(kv.Value.HoursWeekend, 2),
                        TotalAmount = Math.Round(kv.Value.Amount, 2),
                        Nif = coach.CoachNavigation?.PersonInfo?.Nif,
                        PaymentStatus = null,
                        LastPaymentDate = null
                    };
                })
                .OrderBy(r => r.CoachName)
                .ToList();

            var summary = new BillingCoachSummary
            {
                TotalCoaches = allRows.Count,
                TotalExpense = allRows.Sum(r => r.TotalAmount),
                TotalHours = allRows.Sum(r => r.HoursTaught),
                PendingCount = 0
            };

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
                Summary = summary,
                Items = pagedItems,
                TotalCount = allRows.Count,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<byte[]> ExportStudentBillingAsync(
            int year,
            int month,
            string? search)
        {
            var result = await GetStudentBillingAsync(
                year,
                month,
                search,
                page: 1,
                pageSize: int.MaxValue);
            // Load authoritative parent/student info from DB to ensure parity with UI
            var studentIds = result.Items.Select(i => i.StudentId).ToList();

            var studentsWithParents = await _context.Students
                .Where(s => studentIds.Contains(s.StudentId))
                .Include(s => s.ParentUser)
                    .ThenInclude(u => u.PersonInfo)
                .Include(s => s.PersonInfo)
                .ToListAsync();

            var studentNifMap = studentsWithParents.ToDictionary(s => s.StudentId, s => s.PersonInfo?.Nif);
            var responsibleNameMap = new Dictionary<int, string?>();
            var responsibleNifMap = new Dictionary<int, string?>();
            var nameKeyMap = new Dictionary<string, (string? respName, string? respNif, string? studentNif)>();

            foreach (var s in studentsWithParents)
            {
                string? respName = null;
                string? respNif = null;

                if (s.ParentUser != null && s.ParentUser.PersonInfo != null)
                {
                    respName = (s.ParentUser.PersonInfo.FirstName + " " + s.ParentUser.PersonInfo.LastName).Trim();
                    respNif = s.ParentUser.PersonInfo.Nif;
                }
                else if (s.ParentUser != null)
                {
                    respName = s.ParentUser.Username;
                }

                responsibleNameMap[s.StudentId] = respName;
                responsibleNifMap[s.StudentId] = respNif;

                // also index by resolved student name as fallback
                var resolved = ResolveStudentName(s);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    nameKeyMap[resolved] = (respName, respNif, s.PersonInfo?.Nif);
                }
            }


            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Faturação Alunos");

            ws.Cell(1, 1).Value = $"Faturação de Alunos — {year}-{month:D2}";
            ws.Range(1, 1, 1, 8).Merge();

            ws.Cell(1, 1).Style
                .Font.SetBold(true)
                .Font.SetFontSize(13)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = $"Total alunos: {result.Summary.TotalStudents}";
            ws.Cell(2, 3).Value = $"Total horas: {result.Summary.TotalHours:F2}";
            ws.Cell(2, 5).Value = "Total receita:";
            ws.Cell(2, 6).Value = result.Summary.TotalRevenue;
            ws.Cell(2, 6).Style.NumberFormat.Format = "#,##0.00 €";
            ws.Range(2, 1, 2, 6).Style.Font.SetItalic(true);

            ws.Cell(4, 1).Value = "Nome";
            ws.Cell(4, 2).Value = "NIF";
            ws.Cell(4, 3).Value = "Nome do Responsável";
            ws.Cell(4, 4).Value = "NIF do Responsável";
            ws.Cell(4, 5).Value = "Horas segunda a sábado";
            ws.Cell(4, 6).Value = "Horas domingo ou feriados";
            ws.Cell(4, 7).Value = "Total Horas";
            ws.Cell(4, 8).Value = "Total (€)";

            ws.Range(4, 1, 4, 8).Style
                .Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#2D3748"))
                .Font.SetFontColor(XLColor.White)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            int row = 5;

            foreach (var item in result.Items)
            {
                ws.Cell(row, 1).Value = item.StudentName;

                // Student NIF: prefer the value from the billing result (UI), fallback to DB map
                string studentNif = item.Nif ?? string.Empty;
                if (string.IsNullOrWhiteSpace(studentNif) && studentNifMap != null && item.StudentId != 0 && studentNifMap.TryGetValue(item.StudentId, out var sn))
                    studentNif = sn ?? string.Empty;
                if (string.IsNullOrWhiteSpace(studentNif))
                {
                    var key = item.StudentName ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(key) && nameKeyMap.TryGetValue(key, out var kv)) studentNif = kv.studentNif ?? string.Empty;
                }
                ws.Cell(row, 2).SetValue(studentNif);
                ws.Cell(row, 2).Style.NumberFormat.Format = "@";

                // Responsible (parent) name / nif: prefer the values from the billing result (UI), fallback to DB-loaded maps
                string respName = item.ResponsibleName ?? string.Empty;
                string respNif = item.ResponsibleNif ?? string.Empty;

                if (string.IsNullOrWhiteSpace(respName) && item.StudentId != 0 && responsibleNameMap != null && responsibleNameMap.TryGetValue(item.StudentId, out var rn))
                    respName = rn ?? string.Empty;

                if (string.IsNullOrWhiteSpace(respNif) && item.StudentId != 0 && responsibleNifMap != null && responsibleNifMap.TryGetValue(item.StudentId, out var rnf))
                    respNif = rnf ?? string.Empty;

                // fallback by student name if still missing
                if ((string.IsNullOrWhiteSpace(respName) || string.IsNullOrWhiteSpace(respNif)))
                {
                    var key = item.StudentName ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(key) && nameKeyMap.TryGetValue(key, out var kv))
                    {
                        if (string.IsNullOrWhiteSpace(respName)) respName = kv.respName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(respNif)) respNif = kv.respNif ?? string.Empty;
                    }
                }

                ws.Cell(row, 3).Value = respName;
                ws.Cell(row, 4).SetValue(respNif);
                ws.Cell(row, 4).Style.NumberFormat.Format = "@";

                ws.Cell(row, 5).Value = item.HoursWeekday;
                ws.Cell(row, 6).Value = item.HoursWeekend;
                ws.Cell(row, 7).Value = item.HoursCompleted;
                ws.Cell(row, 8).Value = item.TotalAmount;
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00 €";

                if (row % 2 == 0)
                {
                    ws.Range(row, 1, row, 8).Style
                        .Fill.SetBackgroundColor(XLColor.FromHtml("#F7FAFC"));
                }

                row++;
            }

            ws.Cell(row, 1).Value = "TOTAL";
            // Sum the Total (€) column (column H)
            ws.Cell(row, 8).FormulaA1 = $"=SUM(H5:H{row - 1})";
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00 €";

            ws.Range(row, 1, row, 8).Style
                .Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#EDF2F7"));

            // Log rows for diagnostics
            try
            {
                foreach (var item in result.Items)
                {
                    _logger?.LogDebug("ExportStudentBilling writing item: {Item}", item);
                }
            }
            catch { }

            ws.Columns().AdjustToContents();
            ws.Column(3).Width = Math.Max(ws.Column(3).Width, 24);
            ws.Column(4).Width = Math.Max(ws.Column(4).Width, 22);
            ws.Column(5).Width = Math.Max(ws.Column(5).Width, 20);
            ws.Column(6).Width = Math.Max(ws.Column(6).Width, 20);
            ws.Column(7).Width = Math.Max(ws.Column(7).Width, 18);
            ws.Column(8).Width = Math.Max(ws.Column(8).Width, 18);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);

            return stream.ToArray();
        }

        public async Task<byte[]> ExportCoachBillingAsync(
            int year,
            int month,
            string? search)
        {
            var result = await GetCoachBillingAsync(
                year,
                month,
                search,
                page: 1,
                pageSize: int.MaxValue);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Faturação Coaches");

            ws.Cell(1, 1).Value = $"Faturação de Coaches — {year}-{month:D2}";
            ws.Range(1, 1, 1, 7).Merge();

            ws.Cell(1, 1).Style
                .Font.SetBold(true)
                .Font.SetFontSize(13)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = $"Total coaches: {result.Summary.TotalCoaches}";
            ws.Cell(2, 3).Value = $"Total horas: {result.Summary.TotalHours:F2}";
            ws.Cell(2, 6).Value = "Total despesa:";
            ws.Cell(2, 7).Value = result.Summary.TotalExpense;
            ws.Cell(2, 7).Style.NumberFormat.Format = "#,##0.00 €";
            ws.Range(2, 1, 2, 7).Style.Font.SetItalic(true);

            ws.Cell(4, 1).Value = "Nome";
            ws.Cell(4, 2).Value = "NIF";
            ws.Cell(4, 3).Value = "Modalidades";
            ws.Cell(4, 4).Value = "Horas segunda a sábado";
            ws.Cell(4, 5).Value = "Horas domingo ou feriados";
            ws.Cell(4, 6).Value = "Total Horas";
            ws.Cell(4, 7).Value = "Total (€)";

            ws.Range(4, 1, 4, 7).Style
                .Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#2D3748"))
                .Font.SetFontColor(XLColor.White)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

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

                if (row % 2 == 0)
                {
                    ws.Range(row, 1, row, 7).Style
                        .Fill.SetBackgroundColor(XLColor.FromHtml("#F7FAFC"));
                }

                row++;
            }

            ws.Cell(row, 1).Value = "TOTAL";
            ws.Cell(row, 6).FormulaA1 = $"=SUM(F5:F{row - 1})";
            ws.Cell(row, 7).FormulaA1 = $"=SUM(G5:G{row - 1})";
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00 €";

            ws.Range(row, 1, row, 7).Style
                .Font.SetBold(true)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#EDF2F7"));

            ws.Columns().AdjustToContents();
            ws.Column(3).Width = Math.Max(ws.Column(3).Width, 20);
            ws.Column(4).Width = Math.Max(ws.Column(4).Width, 24);
            ws.Column(5).Width = Math.Max(ws.Column(5).Width, 22);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);

            return stream.ToArray();
        }

        public async Task<BillingAnnualResponse> GetAnnualBillingAsync(int year)
        {
            decimal weekdayRate = await _appSettings.GetDecimalAsync("class_price_weekday", 36.00m);
            decimal weekendRate = await _appSettings.GetDecimalAsync("class_price_weekend", 43.50m);

            var classes = await _context.CoachClasses
                .Include(c => c.Participants)
                .Where(c =>
                    c.Status == (byte)CoachClassStatus.Validated &&
                    c.StartDatetime.Year == year)
                .ToListAsync();

            var monthNames = new[] { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };
            var monthPoints = new List<BillingAnnualMonthPoint>();

            for (int m = 1; m <= 12; m++)
            {
                var monthClasses = classes.Where(c => c.StartDatetime.Month == m).ToList();
                decimal revenue = 0m;
                decimal hours = 0m;

                foreach (var cls in monthClasses)
                {
                    decimal durationHours = DurationHours(cls);
                    bool isSundayOrHoliday = IsSundayOrHoliday(cls.StartDatetime);

                    hours += durationHours;

                    foreach (var p in cls.Participants)
                    {
                        decimal appliedRate = p.PerParticipantPrice ?? (isSundayOrHoliday ? weekendRate : weekdayRate);
                        revenue += durationHours * appliedRate;
                    }
                }

                monthPoints.Add(new BillingAnnualMonthPoint
                {
                    Month = m,
                    MonthLabel = monthNames[m - 1],
                    TotalRevenue = Math.Round(revenue, 2),
                    TotalHours = Math.Round(hours, 2),
                    TotalSessions = monthClasses.Count
                });
            }

            return new BillingAnnualResponse
            {
                Year = year,
                Months = monthPoints,
                YearTotalRevenue = Math.Round(monthPoints.Sum(p => p.TotalRevenue), 2),
                YearTotalHours = Math.Round(monthPoints.Sum(p => p.TotalHours), 2),
                YearTotalSessions = monthPoints.Sum(p => p.TotalSessions)
            };
        }

        private static bool IsSundayOrHoliday(DateTime date)
        {
            // Regra atual:
            // Segunda a sábado = 36€/h
            // Domingo/feriados = 43,50€/h
            //
            // TODO: adicionar lógica real de feriados aqui.
            return date.DayOfWeek == DayOfWeek.Sunday;
        }

        private static decimal DurationHours(CoachClass cls)
        {
            return (decimal)(cls.EndDatetime - cls.StartDatetime).TotalMinutes / 60.0m;
        }

        private static string ResolveStudentName(Student student)
        {
            var person = student.PersonInfo;

            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : $"Student {student.StudentId}";
        }

        private static string ResolveCoachName(Coach coach)
        {
            var person = coach.CoachNavigation?.PersonInfo;

            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : coach.CoachNavigation?.Username ?? $"Coach {coach.CoachId}";
        }
    }
}