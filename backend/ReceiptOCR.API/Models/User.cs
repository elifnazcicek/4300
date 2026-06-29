namespace ReceiptOCR.API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? SmsOtpCode { get; set; }
        public DateTime? SmsOtpExpiry { get; set; }
    }
}
