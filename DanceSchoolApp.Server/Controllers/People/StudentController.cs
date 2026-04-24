using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Services.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers.People
{
    [Route("api/students")]
    [ApiController]
    public class StudentController : ControllerBase
    {

        private readonly StudentService _studentService;
        public StudentController(StudentService studentService) 
        {
            _studentService = studentService;
        }

        // ─── GET /api/students ─────────────────────────────────────────────────
        [Authorize(Roles = "staff")]
        [HttpGet]
        public async Task<IActionResult> GetStudents()
        {
            try
            {
                var result = await _studentService.GetStudentsAsync();

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // ─── GET /api/students/{id} ────────────────────────────────────────────
        [Authorize(Roles = "staff,parent")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetStudent(int id)
        {
            try
            {
                var result = await _studentService.GetStudentAsync(id);

                if (!IsStaff() && result.ParentUserId != GetUserId())
                    return Forbid();

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // ─── GET /api/students/parent/{parentId} ───────────────────────────────
        [Authorize(Roles = "staff,parent")]
        [HttpGet("parent/{parentId}")]
        public async Task<IActionResult> GetStudentsByParent(int parentId)
        {
            if (!IsStaff() && parentId != GetUserId())
                return Forbid();

            try
            {
                var result = await _studentService.GetStudentsByParentAsync(parentId);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private bool IsStaff() => User.IsInRole("staff");

        // ─── POST /api/students ────────────────────────────────────────────────
        [Authorize(Roles = "staff,parent")]
        [HttpPost]
        public async Task<IActionResult> CreateStudent([FromBody] StudentCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!IsStaff())
                request.ParentId = GetUserId();

            try
            {
                var newId = await _studentService.CreateStudentAsync(request);
                return CreatedAtAction(nameof(GetStudent), new { id = newId }, new { studentId = newId });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // ─── PUT /api/students/{id} ───────────────────────────────────────────
        // Parent use — updates PersonInfo fields only (does NOT reset AcceptanceStatus).
        [Authorize(Roles = "staff")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStudentPersonInfo(
            int id, [FromBody] StudentUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var student = await _studentService.GetStudentAsync(id);
                if (student.ParentUserId != GetUserId())
                    return Forbid();

                await _studentService.UpdatePersonInfoAsync(id, request);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // ─── PATCH /api/students/{id} ──────────────────────────────────────────
        [Authorize(Roles = "staff,parent")]
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                if (!IsStaff())
                {
                    var student = await _studentService.GetStudentAsync(id);
                    if (student.ParentUserId != GetUserId())
                        return Forbid();
                }

                await _studentService.UpdateStudentAsync(id, request);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // ─── PATCH /api/students/{id}/activate ────────────────────────────────
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/activate")]
        public async Task<IActionResult> ActivateStudent(int id)
        {
            try
            {
                await _studentService.SetStudentStateAsync(id, isActive: true);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // ─── PATCH /api/students/{id}/deactivate ──────────────────────────────
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateStudent(int id)
        {
            try
            {
                await _studentService.SetStudentStateAsync(id, isActive: false);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // ─── PATCH /api/students/{id}/accept ──────────────────────────────────
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/accept")]
        public async Task<IActionResult> AcceptStudent(int id)
        {
            try
            {
                await _studentService.AcceptStudentAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // ─── PATCH /api/students/{id}/reject ──────────────────────────────────
        [Authorize(Roles = "staff")]
        [HttpPatch("{id}/reject")]
        public async Task<IActionResult> RejectStudent(int id,
            [FromBody] StudentAcceptanceRejectRequest? request)
        {
            try
            {
                await _studentService.RejectStudentAsync(id, request?.Reason);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // ─── GET /api/students/{id}/classes ───────────────────────────────────
        // Returns full class history for a student.
        // Staff: any student. Parent: only their own students.
        [Authorize(Roles = "staff,parent")]
        [HttpGet("{id}/classes")]
        public async Task<IActionResult> GetStudentClasses(int id)
        {
            try
            {
                if (!IsStaff())
                {
                    var student = await _studentService.GetStudentAsync(id);
                    if (student.ParentUserId != GetUserId())
                        return Forbid();
                }

                var result = await _studentService.GetStudentClassesAsync(id);

                if (!result.Any())
                    return NoContent();

                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

    }
}
