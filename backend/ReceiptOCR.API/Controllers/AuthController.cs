using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReceiptOCR.API.Data;
using ReceiptOCR.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ReceiptOCR.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ReceiptDbContext _context;
        private readonly Services.EmailService _emailService;

        public AuthController(IConfiguration configuration, ReceiptDbContext context, Services.EmailService emailService)
        {
            _configuration = configuration;
            _context = context;
            _emailService = emailService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new AuthResponse { Success = false, Error = "Kullanıcı adı ve şifre zorunludur." });

            var passwordHash = HashPassword(request.Password);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower() && u.PasswordHash == passwordHash);

            if (user == null || !user.IsActive)
            {
                return Unauthorized(new AuthResponse { Success = false, Error = "Geçersiz kullanıcı adı veya şifre" });
            }

            var token = GenerateJwtToken(user);

            return Ok(new AuthResponse
            {
                Success = true,
                Token = token,
                Username = user.Username,
                Role = user.Role
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new AuthResponse { Success = false, Error = "Kullanıcı adı ve şifre zorunludur." });

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
                return BadRequest(new AuthResponse { Success = false, Error = "Bu kullanıcı adı zaten alınmış." });

            var user = new User
            {
                Username = request.Username,
                PasswordHash = HashPassword(request.Password),
                FullName = request.Username, // Varsayılan olarak username atanıyor
                Role = "User",
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponse
            {
                Success = true,
                Username = user.Username,
                Token = GenerateJwtToken(user),
                Role = user.Role
            });
        }


        // POST: api/auth/forgot-password
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.EmailOrUsername))
            {
                return BadRequest(new { Message = "E-posta veya kullanıcı adı gereklidir." });
            }

            var query = request.EmailOrUsername.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.Username.ToLower() == query || 
                (u.Email != null && u.Email.ToLower() == query)
            );

            if (user == null)
            {
                return NotFound(new { Message = "Sistemde bu kullanıcı adı veya e-posta adresi ile eşleşen bir kayıt bulunamadı." });
            }

            if (string.IsNullOrEmpty(user.Email))
            {
                return BadRequest(new { Message = "Bu kullanıcının sistemde tanımlı bir e-posta adresi bulunmamaktadır. Lütfen yöneticinizle irtibata geçin." });
            }

            // Generate 6 digit random numeric code
            var random = new Random();
            var code = random.Next(100000, 999999).ToString();

            user.EmailResetCode = code;
            user.EmailResetExpiry = DateTime.UtcNow.AddMinutes(10); // 10 minutes expiry

            await _context.SaveChangesAsync();

            // Log code to server console for testing/debugging
            Console.WriteLine($"\n\n[TEST/DEMO UYARISI] Şifre Sıfırlama Kodu (Kullanıcı: {user.Username}): {code}\n\n");

            try
            {
                await _emailService.SendResetCodeEmailAsync(user.Email, code);
                return Ok(new { Message = "Şifre yenileme kodunuz başarıyla e-posta adresinize gönderildi.", Email = user.Email });
            }
            catch (Exception ex)
            {
                // Fallback for demo environments: return Ok but warn that email failed and code is in terminal
                return Ok(new { 
                    Message = $"E-posta gönderilemedi ({ex.InnerException?.Message ?? ex.Message}). Ancak test/demo ortamı kolaylığı için şifre yenileme kodunuz sunucu konsoluna yazdırılmıştır. Lütfen konsoldan kodu alıp devam ediniz.", 
                    Email = user.Email,
                    IsDemo = true
                });
            }
        }

        // POST: api/auth/verify-reset-code
        [HttpPost("verify-reset-code")]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.EmailOrUsername) || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { Message = "E-posta/kullanıcı adı ve doğrulama kodu zorunludur." });
            }

            var query = request.EmailOrUsername.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.Username.ToLower() == query || 
                (u.Email != null && u.Email.ToLower() == query)
            );

            if (user == null)
            {
                return NotFound(new { Message = "Kullanıcı bulunamadı." });
            }

            if (user.EmailResetCode == null || user.EmailResetCode != request.Code.Trim() || user.EmailResetExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { Message = "Geçersiz veya süresi dolmuş doğrulama kodu." });
            }

            return Ok(new { Message = "Doğrulama kodu onaylandı. Yeni şifrenizi belirleyebilirsiniz." });
        }

        // POST: api/auth/reset-password
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (request == null || 
                string.IsNullOrWhiteSpace(request.EmailOrUsername) || 
                string.IsNullOrWhiteSpace(request.Code) || 
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { Message = "Tüm alanlar zorunludur." });
            }

            var query = request.EmailOrUsername.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.Username.ToLower() == query || 
                (u.Email != null && u.Email.ToLower() == query)
            );

            if (user == null)
            {
                return NotFound(new { Message = "Kullanıcı bulunamadı." });
            }

            if (user.EmailResetCode == null || user.EmailResetCode != request.Code.Trim() || user.EmailResetExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { Message = "Geçersiz veya süresi dolmuş doğrulama kodu." });
            }

            // Update password
            user.PasswordHash = HashPassword(request.NewPassword);
            
            // Clear verification code
            user.EmailResetCode = null;
            user.EmailResetExpiry = null;

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz." });
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var builder = new StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = _configuration["Jwt:Key"] ?? "super_secret_key_for_receipt_ocr_app_1234567890123456";
            var key = Encoding.ASCII.GetBytes(jwtKey);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"] ?? "ReceiptOCR",
                Audience = _configuration["Jwt:Audience"] ?? "ReceiptOCR"
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        public string EmailOrUsername { get; set; } = string.Empty;
    }

    public class VerifyResetCodeRequest
    {
        public string EmailOrUsername { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string EmailOrUsername { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
