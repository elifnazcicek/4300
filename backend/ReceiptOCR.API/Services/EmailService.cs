using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ReceiptOCR.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendResetCodeEmailAsync(string targetEmail, string code)
        {
            try
            {
                var smtpServer = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
                var smtpPortStr = _configuration["Smtp:Port"] ?? "587";
                var smtpUser = _configuration["Smtp:Username"] ?? "sifreyenileme3@gmail.com";
                var smtpPass = _configuration["Smtp:Password"] ?? "sifreyenilemesistemi3";

                int smtpPort = int.Parse(smtpPortStr);

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(smtpUser, "Fiş OCR Şifre Yenileme"),
                        Subject = "Şifre Sıfırlama Doğrulama Kodu",
                        Body = $@"
                        <h3>Şifre Sıfırlama Talebi</h3>
                        <p>Hesabınız için şifre sıfırlama talebinde bulunuldu. Şifrenizi yenilemek için kullanacağınız 6 haneli doğrulama kodu aşağıdadır:</p>
                        <h2 style='color: #4f46e5; letter-spacing: 2px;'>{code}</h2>
                        <p>Bu kod 10 dakika süreyle geçerlidir. Eğer bu talebi siz yapmadıysanız bu e-postayı dikkate almayınız.</p>
                        ",
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(targetEmail);

                    _logger.LogInformation("SMTP üzerinden e-posta gönderiliyor: {TargetEmail}", targetEmail);
                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation("Şifre yenileme kodu başarıyla e-posta olarak gönderildi: {TargetEmail}", targetEmail);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "E-posta gönderimi sırasında hata oluştu. Alıcı: {TargetEmail}", targetEmail);
                throw new Exception($"E-posta gönderimi başarısız oldu: {ex.Message}", ex);
            }
        }
    }
}
