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
                Username = user.Username
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
                PhoneNumber = request.PhoneNumber,
                Email = request.Email
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponse
            {
                Success = true,
                Username = user.Username,
                Token = GenerateJwtToken(user)
            });
        }

        [HttpPost("reset-password/request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] ResetPasswordRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new AuthResponse { Success = false, Error = "Kullanıcı adı ve e-posta adresi zorunludur." });

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());

            if (user == null)
            {
                return BadRequest(new AuthResponse { Success = false, Error = "Kullanıcı adı veya e-posta adresi hatalı." });
            }

            if (string.IsNullOrEmpty(user.Email) || user.Email.Trim().ToLower() != request.Email.Trim().ToLower())
            {
                return BadRequest(new AuthResponse { Success = false, Error = "Kullanıcı adı veya e-posta adresi hatalı." });
            }

            // Generate 6-digit random code
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            // Set expiry (5 minutes)
            user.SmsOtpCode = otpCode;
            user.SmsOtpExpiry = DateTime.UtcNow.AddMinutes(5);

            await _context.SaveChangesAsync();

            // Send actual email using System.Net.Mail SMTP
            try
            {
                await SendResetEmailAsync(user.Email, user.Username, otpCode);
                Console.WriteLine($"\n[EMAIL SENDER] Reset code '{otpCode}' sent to '{user.Email}'\n");

                // Log to system logs
                var logEntry = new SystemLog
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Username = user.Username,
                    ActionType = "EMAIL_OTP",
                    Status = "SUCCESS",
                    Details = $"E-posta Şifre Sıfırlama Kodu gönderildi. Kod: {otpCode} (E-posta: {user.Email})"
                };
                _context.SystemLogs.Add(logEntry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[EMAIL SENDER ERROR] Failed to send email to '{user.Email}': {ex.Message}\n");
                return StatusCode(500, new AuthResponse { Success = false, Error = $"E-posta gönderilemedi: {ex.Message}" });
            }

            return Ok(new
            {
                Success = true,
                Message = "Doğrulama kodu e-posta adresinize gönderildi.",
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

            // Log to system logs
            try
            {
                var logEntry = new SystemLog
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Username = user.Username,
                    ActionType = "Şifre_Sıfırlama",
                    Status = "SUCCESS",
                    Details = "Kullanıcı şifresi E-posta doğrulaması ile başarıyla yenilendi."
                };
                _context.SystemLogs.Add(logEntry);
                await _context.SaveChangesAsync();
            }
            catch { }

            return Ok(new AuthResponse
            {
                Success = true,
                Username = user.Username,
                Token = GenerateJwtToken(user)
            });
        }

        private async Task SendResetEmailAsync(string email, string username, string code)
        {
            var fromAddress = new System.Net.Mail.MailAddress("sifreyenileme3@gmail.com", "Fiş/Fatura OCR Şifre Sıfırlama");
            var toAddress = new System.Net.Mail.MailAddress(email);
            const string fromPassword = "sifreyenilemesistemi3";
            const string subject = "Şifre Sıfırlama Doğrulama Kodu";
            string body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; color: #333;'>
                <h2>Merhaba {username},</h2>
                <p>Hesabınızın şifresini yenilemek için talepte bulundunuz.</p>
                <p>Şifre sıfırlama doğrulama kodunuz:</p>
                <div style='background-color: #f1f5f9; padding: 15px; font-size: 24px; font-weight: bold; letter-spacing: 5px; text-align: center; border-radius: 8px; border: 1px solid #cbd5e1; margin: 20px 0; max-width: 200px;'>
                    {code}
                </div>
                <p>Bu kod 5 dakika geçerlidir. Eğer bu talebi siz yapmadıysanız bu e-postayı görmezden gelebilirsiniz.</p>
                <br/>
                <p>Saygılarımızla,<br/>Fiş/Fatura OCR Destek Ekibi</p>
            </body>
            </html>";

            using var smtp = new System.Net.Mail.SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential(fromAddress.Address, fromPassword)
            };

            using var message = new System.Net.Mail.MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await smtp.SendMailAsync(message);
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

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
    }

    public class ResetPasswordRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordVerifyDto
    {
        public string Username { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
