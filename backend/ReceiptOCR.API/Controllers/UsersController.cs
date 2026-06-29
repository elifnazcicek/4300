using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptOCR.API.Data;
using ReceiptOCR.API.Models;

namespace ReceiptOCR.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly ReceiptDbContext _context;

        public UsersController(ReceiptDbContext context)
        {
            _context = context;
        }

        // GET: api/users
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .OrderBy(u => u.Username)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.FullName,
                    u.Role,
                    u.IsActive,
                    u.PhoneNumber
                })
                .ToListAsync();

            return Ok(users);
        }

        // PUT: api/users/{id}/role
        [HttpPut("{id}/role")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDto request)
        {
            var currentCaller = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentCaller))
            {
                return Unauthorized();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest(new { Message = "Rol alanı zorunludur." });
            }

            if (request.Role != "Admin" && request.Role != "User")
            {
                return BadRequest(new { Message = "Geçersiz rol değeri. Rol sadece 'Admin' veya 'User' olabilir." });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "Kullanıcı bulunamadı." });
            }

            // Prevent self-demotion
            if (user.Username.Equals(currentCaller, System.StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = "Kendi yetkinizi değiştiremezsiniz." });
            }

            // Prevent the main stajyer admin from removing their own admin privilege
            if (user.Username.ToLower() == "stajyer" && request.Role != "Admin")
            {
                return BadRequest(new { Message = "Ana stajyer yöneticisinin yetkisi geri alınamaz." });
            }

            user.Role = request.Role;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Kullanıcı yetkisi başarıyla güncellendi.", Role = user.Role });
        }

        // PUT: api/users/{id}/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateStatusDto request)
        {
            var currentCaller = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentCaller))
            {
                return Unauthorized();
            }

            if (request == null)
            {
                return BadRequest(new { Message = "İstek gövdesi boş olamaz." });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { Message = "Kullanıcı bulunamadı." });
            }

            // Prevent self-status toggling
            if (user.Username.Equals(currentCaller, System.StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Message = "Kendi aktif/pasif durumunuzu değiştiremezsiniz." });
            }

            // Prevent disabling stajyer
            if (user.Username.ToLower() == "stajyer" && !request.IsActive)
            {
                return BadRequest(new { Message = "Ana stajyer yöneticisi pasif hale getirilemez." });
            }

            user.IsActive = request.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Kullanıcı durumu başarıyla güncellendi.", IsActive = user.IsActive });
        }
    }

    public class UpdateRoleDto
    {
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateStatusDto
    {
        public bool IsActive { get; set; }
    }
}
