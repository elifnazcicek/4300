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

        public AuthController(IConfiguration configuration, ReceiptDbContext context)
        {
            _configuration = configuration;
            _context = context;
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
                CreatedDate = DateTime.UtcNow,
                PhoneNumber = request.PhoneNumber
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

        [HttpPost("reset-password/request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] ResetPasswordRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.PhoneNumber))
                return BadRequest(new AuthResponse { Success = false, Error = "Kullanıcı adı ve telefon numarası zorunludur." });

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());

            if (user == null)
            {
                return BadRequest(new AuthResponse { Success = false, Error = "Kullanıcı adı veya telefon numarası hatalı." });
            }

            // Normalise phone numbers
            var dbPhone = new string(user.PhoneNumber?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());
            var reqPhone = new string(request.PhoneNumber.Where(char.IsDigit).ToArray());

            if (string.IsNullOrEmpty(dbPhone) || dbPhone != reqPhone)
            {
                return BadRequest(new AuthResponse { Success = false, Error = "Kullanıcı adı veya telefon numarası hatalı." });
            }

            // Generate 6-digit random code
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            // Set expiry (5 minutes)
            user.SmsOtpCode = otpCode;
            user.SmsOtpExpiry = DateTime.UtcNow.AddMinutes(5);

            await _context.SaveChangesAsync();

            // Simulating SMS gateway send
            Console.WriteLine($"\n[SMS GATEWAY] OTP for user '{user.Username}' sent to '{user.PhoneNumber}': {otpCode}\n");

            return Ok(new
            {
                Success = true,
                Message = "Doğrulama kodu telefonunuza gönderildi.",
                OtpCode = otpCode 
            });
        }

        [HttpPost("reset-password/verify")]
        public async Task<IActionResult> VerifyPasswordReset([FromBody] ResetPasswordVerifyDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.OtpCode) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new AuthResponse { Success = false, Error = "Tüm alanlar zorunludur." });

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());

            if (user == null || user.SmsOtpCode != request.OtpCode)
            {
                return BadRequest(new AuthResponse { Success = false, Error = "Doğrulama kodu hatalı." });
            }

            if (user.SmsOtpExpiry == null || user.SmsOtpExpiry < DateTime.UtcNow)
            {
                return BadRequest(new AuthResponse { Success = false, Error = "Doğrulama kodunun süresi dolmuş." });
            }

            // Reset password
            user.PasswordHash = HashPassword(request.NewPassword);
            user.SmsOtpCode = null;
            user.SmsOtpExpiry = null;

            await _context.SaveChangesAsync();

            return Ok(new AuthResponse
            {
                Success = true,
                Username = user.Username,
                Token = GenerateJwtToken(user),
                Role = user.Role
            });
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
        public string? PhoneNumber { get; set; }
    }

    public class ResetPasswordRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class ResetPasswordVerifyDto
    {
        public string Username { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
