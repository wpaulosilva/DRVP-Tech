using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Staff;
using DanceSchoolApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;

namespace DanceSchoolApp.Server.Controllers
{
    [ApiController]
    [Route("api/staff")]
    [Authorize(Roles = "staff,admin")]
    [EnableRateLimiting("api")]
    public class StaffDashboardController : ControllerBase
    {
        private readonly StaffDashboardService _staffService;
        private readonly BillingService        _billingService;
        private readonly AppSettingService     _appSettingService;

        public StaffDashboardController(
            StaffDashboardService staffService,
            BillingService        billingService,
            AppSettingService     appSettingService)
        {
            _staffService      = staffService;
            _billingService    = billingService;
            _appSettingService = appSettingService;
        }

        //  GET /api/staff/dashboard 
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var result = await _staffService.GetDashboardAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/staff/agenda 
        // Query: from, to (DateOnly, required), studioId?, status?
        // Range capped at 60 days.
        [HttpGet("agenda")]
        public async Task<IActionResult> GetAgenda(
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            [FromQuery] int?  studioId = null,
            [FromQuery] byte? status   = null)
        {
            if (to < from)
                return BadRequest("'to' must be on or after 'from'.");

            if ((to.DayNumber - from.DayNumber) > 60)
                return BadRequest("Date range must not exceed 60 days.");

            try
            {
                var result = await _staffService.GetAgendaAsync(from, to, studioId, status);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/staff/validate-classes 
        // Query: tab ("requested"|"finished"|"pending", default "requested"),
        //        page (default 1), pageSize (default 15, max 50)
        [HttpGet("validate-classes")]
        public async Task<IActionResult> GetValidateClasses(
            [FromQuery] string tab      = "requested",
            [FromQuery] int    page     = 1,
            [FromQuery] int    pageSize = 15)
        {
            if (page < 1)     page     = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 50) pageSize = 50;

            try
            {
                var result = await _staffService.GetValidateClassesAsync(tab, page, pageSize);

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/staff/validate-students 
        // Query: status ("pending"|"approved"|"all", default "pending"),
        //        page (default 1), pageSize (default 10, max 50)
        [HttpGet("validate-students")]
        public async Task<IActionResult> GetValidateStudents(
            [FromQuery] string status   = "pending",
            [FromQuery] int    page     = 1,
            [FromQuery] int    pageSize = 10)
        {
            if (page < 1)     page     = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 50) pageSize = 50;

            try
            {
                var result = await _staffService.GetValidateStudentsAsync(status, page, pageSize);

                if (result.TotalCount == 0)
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/staff/billing/students 
        // Query: month (YYYY-MM, required), search?, page, pageSize (default 25)
        [HttpGet("billing/students")]
        public async Task<IActionResult> GetBillingStudents(
            [FromQuery] string  month,
            [FromQuery] string? search   = null,
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 25)
        {
            if (!TryParseYearMonth(month, out int year, out int monthInt))
                return BadRequest("Invalid month format. Expected YYYY-MM.");

            if (page < 1)      page     = 1;
            if (pageSize < 1)  pageSize = 1;
            if (pageSize > 100) pageSize = 100;

            try
            {
                var result = await _billingService.GetStudentBillingAsync(
                    year, monthInt, search, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        //  GET /api/staff/billing/coaches 
        // Query: month (YYYY-MM, required), search?, page, pageSize (default 25)
        [HttpGet("billing/coaches")]
        public async Task<IActionResult> GetBillingCoaches(
            [FromQuery] string  month,
            [FromQuery] string? search   = null,
            [FromQuery] int     page     = 1,
            [FromQuery] int     pageSize = 25)
        {
            if (!TryParseYearMonth(month, out int year, out int monthInt))
                return BadRequest("Invalid month format. Expected YYYY-MM.");

            if (page < 1)      page     = 1;
            if (pageSize < 1)  pageSize = 1;
            if (pageSize > 100) pageSize = 100;

            try
            {
                var result = await _billingService.GetCoachBillingAsync(
                    year, monthInt, search, page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("billing/students/export")]
        public async Task<IActionResult> ExportBillingStudents(
            [FromQuery] string month,
            [FromQuery] string? search = null)
        {
            if (!TryParseYearMonth(month, out int year, out int monthInt))
                return BadRequest("Invalid month format. Expected YYYY-MM.");

            var fileBytes = await _billingService.ExportStudentBillingAsync(year, monthInt, search);

            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"billing_students_{year}-{monthInt:D2}.xlsx"
            );
        }

        [HttpGet("billing/coaches/export")]
        public async Task<IActionResult> ExportBillingCoaches(
            [FromQuery] string month,
            [FromQuery] string? search = null)
        {
            if (!TryParseYearMonth(month, out int year, out int monthInt))
                return BadRequest("Invalid month format. Expected YYYY-MM.");

            var fileBytes = await _billingService.ExportCoachBillingAsync(year, monthInt, search);

            return File(
                fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"billing_coaches_{year}-{monthInt:D2}.xlsx"
            );
        }


        //  Private helpers 

        private static bool TryParseYearMonth(string? input, out int year, out int month)
        {
            year = month = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            if (!DateTime.TryParseExact(
                    input + "-01", "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
                return false;

            year  = dt.Year;
            month = dt.Month;
            return true;
        }
    }
}
