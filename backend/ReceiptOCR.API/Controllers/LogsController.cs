using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using ReceiptOCR.API.Data;

namespace ReceiptOCR.API.Controllers
{
    [ApiController]
    [Route("api/logs")]
    [Route("api/receipts/logs")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class LogsController : ControllerBase
    {
        private static readonly string BaseDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
        private static readonly string LogFile = Path.Combine(BaseDir, "app.log");
        private static readonly string BackupDir = Path.Combine(BaseDir, "backup");

        public LogsController()
        {
            Directory.CreateDirectory(BaseDir);
            Directory.CreateDirectory(BackupDir);
        }

        // GET api/logs OR api/receipts/logs
        [HttpGet]
        public IActionResult GetLiveLogs()
        {
            try
            {
                if (!System.IO.File.Exists(LogFile))
                {
                    return Ok("Henüz log kaydı bulunmuyor.");
                }

                // Read last 2000 lines of app.log to prevent sending huge files
                var lines = System.IO.File.ReadLines(LogFile, Encoding.UTF8).TakeLast(2000);
                string content = string.Join(Environment.NewLine, lines);
                return Content(content, "text/plain", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Log dosyası okunurken hata oluştu: {ex.Message}");
            }
        }

        // POST api/logs/backup OR api/receipts/logs/backup
        [HttpPost("backup")]
        public IActionResult TriggerBackup()
        {
            try
            {
                if (!System.IO.File.Exists(LogFile))
                {
                    return BadRequest("Yedeklenecek log dosyası bulunamadı.");
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(BackupDir, $"app_{timestamp}.log");
                System.IO.File.Copy(LogFile, backupPath, true);

                // Limit backup files to last 20
                var files = Directory.GetFiles(BackupDir, "app_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.LastWriteTime)
                    .ToList();

                while (files.Count > 20)
                {
                    files[0].Delete();
                    files.RemoveAt(0);
                }

                // Append a success log to app.log as well
                var successMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [SUCCESS] User: System, Action: Db_Backup, Details: Sistem logları başarıyla backup klasörüne yedeklendi. Yedek: {Path.GetFileName(backupPath)}";
                System.IO.File.AppendAllText(LogFile, successMsg + Environment.NewLine, Encoding.UTF8);

                return Ok(new { status = "success", message = "Yedek oluşturuldu." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Yedekleme sırasında hata oluştu: {ex.Message}");
            }
        }

        // GET api/logs/backups OR api/receipts/logs/backups
        [HttpGet("backups")]
        public IActionResult GetBackups()
        {
            try
            {
                if (!Directory.Exists(BackupDir))
                {
                    return Ok(Array.Empty<object>());
                }

                var files = Directory.GetFiles(BackupDir, "app_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Select(f => new
                    {
                        filename = f.Name,
                        lastModified = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        size = f.Length
                    })
                    .ToList();

                return Ok(files);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Yedek listesi alınırken hata oluştu: {ex.Message}");
            }
        }

        // GET api/logs/backups/{filename} OR api/receipts/logs/backups/{filename}
        [HttpGet("backups/{filename}")]
        public IActionResult GetBackupContent(string filename)
        {
            try
            {
                // Basic path traversal prevention
                var cleanFilename = Path.GetFileName(filename);
                var filePath = Path.Combine(BackupDir, cleanFilename);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("Yedek dosyası bulunamadı.");
                }

                string content = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
                return Content(content, "text/plain", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Yedek dosyası okunurken hata oluştu: {ex.Message}");
            }
        }

        // DELETE api/logs OR api/receipts/logs
        [HttpDelete]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public IActionResult ClearLogs([FromServices] ReceiptDbContext dbContext)
        {
            try
            {
                if (System.IO.File.Exists(LogFile))
                {
                    System.IO.File.WriteAllText(LogFile, string.Empty);
                }

                dbContext.SystemLogs.RemoveRange(dbContext.SystemLogs);
                dbContext.SaveChanges();

                return Ok(new { status = "success", message = "Tüm sistem logları başarıyla temizlendi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Loglar temizlenirken hata oluştu: {ex.Message}");
            }
        }
    }
}
