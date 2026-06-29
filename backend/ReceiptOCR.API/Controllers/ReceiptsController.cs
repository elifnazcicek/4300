using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using ReceiptOCR.API.Data;
using ReceiptOCR.API.Models;
using ReceiptOCR.API.Services;

namespace ReceiptOCR.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReceiptsController : ControllerBase
    {
        private readonly ReceiptDbContext _context;
        private readonly ILogger<ReceiptsController> _logger;
        private readonly ReceiptOCR.API.Services.GeminiService _geminiService;
        private readonly ReceiptOCR.API.Services.ImagePreprocessingService _preprocessingService;
        private static readonly string BaseDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
        private static readonly string LogFile = Path.Combine(BaseDir, "app.log");
        private static readonly string BackupDir = Path.Combine(BaseDir, "backup");

        public ReceiptsController(
            ReceiptDbContext context, 
            ILogger<ReceiptsController> logger,
            ReceiptOCR.API.Services.GeminiService geminiService,
            ReceiptOCR.API.Services.ImagePreprocessingService preprocessingService)
        {
            _context = context;
            _logger = logger;
            _geminiService = geminiService;
            _preprocessingService = preprocessingService;
            
            // Ensure directories exist
            Directory.CreateDirectory(BaseDir);
            Directory.CreateDirectory(BackupDir);
            
            // Set EPPlus LicenseContext
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // Write custom logs to database / log file
        private void WriteLog(string username, string actionType, string status, string details)
        {
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{status}] User: {username}, Action: {actionType}, Details: {details}";
            try
            {
                System.IO.File.AppendAllText(LogFile, logMessage + Environment.NewLine, Encoding.UTF8);

                // Save log entry to SQLite SystemLogs table
                var logEntry = new SystemLog
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Username = username,
                    ActionType = actionType,
                    Status = status,
                    Details = details
                };
                _context.SystemLogs.Add(logEntry);
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing log file / DB: {ex.Message}");
            }
            Console.WriteLine(logMessage);
        }

        // Backup Logs logic
        private bool ExecuteLogBackup()
        {
            try
            {
                if (!System.IO.File.Exists(LogFile))
                    return false;

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(BackupDir, $"app_{timestamp}.log");
                System.IO.File.Copy(LogFile, backupPath, true);

                // Limit backup files to last 20
                var files = Directory.GetFiles(BackupDir, "app_*.log")
                    .Select(f => new System.IO.FileInfo(f))
                    .OrderBy(f => f.LastWriteTime)
                    .ToList();

                while (files.Count > 20)
                {
                    files[0].Delete();
                    files.RemoveAt(0);
                }

                WriteLog("System", "Db_Backup", "SUCCESS", $"Sistem logları başarıyla backup klasörüne yedeklendi. Yedek: {Path.GetFileName(backupPath)}");
                return true;
            }
            catch (Exception ex)
            {
                WriteLog("System", "Db_Backup", "ERROR", $"Log yedekleme hatası: {ex.Message}");
                return false;
            }
        }

        // Regenerate/Update Excel File
        private bool ExecuteExcelUpdate(string username = "default")
        {
            try
            {
                var queryData = _context.Expenses
                    .Where(r => r.KaydedenKullanici == username)
                    .OrderByDescending(r => r.Id)
                    .ToList();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Masraf Listesi");

                    // Set headers
                    string[] headers = {
                        "Fiş_Tarihi", "Açıklama", "VKN_TCKN", "Fiş_No", "KDV_Oranı", "Matrah", "KDV", "Toplam_Fiyat"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cells[1, i + 1].Value = headers[i];
                        worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                    }

                    int rowIdx = 2;
                    foreach (var expense in queryData)
                    {
                        // Check if ItemsJson has a VAT breakdown list
                        List<VatBreakdownItem>? vatItems = null;
                        if (!string.IsNullOrEmpty(expense.ItemsJson))
                        {
                            try
                            {
                                vatItems = System.Text.Json.JsonSerializer.Deserialize<List<VatBreakdownItem>>(expense.ItemsJson);
                            }
                            catch { }
                        }

                        if (vatItems != null && vatItems.Count > 0)
                        {
                            // Write a row for each VAT breakdown item
                            foreach (var vatItem in vatItems)
                            {
                                worksheet.Cells[rowIdx, 1].Value = expense.Tarih;
                                worksheet.Cells[rowIdx, 2].Value = expense.FirmaAdi;
                                worksheet.Cells[rowIdx, 3].Value = expense.VknTckn ?? "";
                                worksheet.Cells[rowIdx, 4].Value = expense.FisNo ?? "";
                                worksheet.Cells[rowIdx, 5].Value = vatItem.KdvOrani;
                                worksheet.Cells[rowIdx, 6].Value = vatItem.Matrah;
                                worksheet.Cells[rowIdx, 7].Value = vatItem.Kdv;
                                worksheet.Cells[rowIdx, 8].Value = vatItem.Matrah + vatItem.Kdv;
                                rowIdx++;
                            }
                        }
                        else
                        {
                            // Write standard single row
                            decimal matrah = expense.ToplamTutar - expense.KdvTutari;
                            worksheet.Cells[rowIdx, 1].Value = expense.Tarih;
                            worksheet.Cells[rowIdx, 2].Value = expense.FirmaAdi;
                            worksheet.Cells[rowIdx, 3].Value = expense.VknTckn ?? "";
                            worksheet.Cells[rowIdx, 4].Value = expense.FisNo ?? "";
                            worksheet.Cells[rowIdx, 5].Value = expense.KdvOrani;
                            worksheet.Cells[rowIdx, 6].Value = matrah;
                            worksheet.Cells[rowIdx, 7].Value = expense.KdvTutari;
                            worksheet.Cells[rowIdx, 8].Value = expense.ToplamTutar;
                            rowIdx++;
                        }
                    }
                    // Auto fit columns
                    if (worksheet.Dimension != null)
                    {
                        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                    }

                    string userExcelFile = Path.Combine(BaseDir, $"fis_raporu_{username}.xlsx");
                    var fileInfo = new System.IO.FileInfo(userExcelFile);
                    package.SaveAs(fileInfo);
                }

                WriteLog(username, "Excel_Kayıt", "SUCCESS", $"Excel dosyası başarıyla güncellendi: fis_raporu_{username}.xlsx");
                return true;
            }
            catch (Exception ex)
            {
                WriteLog(username, "Excel_Kayıt", "ERROR", $"Excel güncellenirken hata oluştu: {ex.Message}");
                return false;
            }
        }

        // 1. API: Image/PDF upload and OCR processing
        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                WriteLog("Guest", "OCR_Okuma", "ERROR", "Boş resim/PDF yükleme denemesi.");
                return BadRequest("Resim seçilmedi.");
            }

            WriteLog("Guest", "OCR_Okuma", "INFO", $"Yeni bir fiş dosyası yükleniyor: {file.FileName} ({file.ContentType})");

            try
            {
                var extension = Path.GetExtension(file.FileName).ToLower();
                if (extension == ".pdf" || file.ContentType == "application/pdf")
                {
                    // Read PDF into byte array so we can open fresh streams for each operation (avoiding Closed Stream errors)
                    byte[] pdfBytes;
                    using (var tempMs = new MemoryStream())
                    {
                        await file.CopyToAsync(tempMs);
                        pdfBytes = tempMs.ToArray();
                    }

                    int pageCount = 0;
                    try
                    {
                        using var msForCount = new MemoryStream(pdfBytes);
                        pageCount = PDFtoImage.Conversion.GetPageCount(msForCount);
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Guest", "PDF_Açma", "ERROR", $"PDF sayfa sayısı okunamadı: {ex.Message}");
                        return BadRequest($"PDF dosyası açılamadı: {ex.Message}");
                    }

                    if (pageCount == 0)
                    {
                        return BadRequest("PDF dosyasında sayfa bulunamadı.");
                    }

                    var pdfPages = new List<string>();

                    // Convert all pages to JPEGs
                    for (int i = 0; i < pageCount; i++)
                    {
                        var pageFileName = $"processed_{Guid.NewGuid()}_page_{i}.jpeg";
                        var pagePath = Path.Combine(BaseDir, pageFileName);

                        using var msForSave = new MemoryStream(pdfBytes);
                        PDFtoImage.Conversion.SaveJpeg(pagePath, msForSave, i, options: new(Dpi: 150));

                        pdfPages.Add($"data/{pageFileName}");
                    }

                    // Run OCR on the first page
                    var firstPagePath = Path.Combine(BaseDir, Path.GetFileName(pdfPages[0]));
                    byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(firstPagePath);
                    var parsedData = await _geminiService.ScanReceiptAsync(imageBytes, "image/jpeg");

                    WriteLog("Guest", "OCR_Okuma", "SUCCESS", $"Gemini OCR başarıyla tamamlandı (PDF Sayfa 1). Algılanan Mağaza: {parsedData.MerchantName}");

                    return Ok(new
                    {
                        merchant_name = parsedData.MerchantName,
                        vkn_tckn = parsedData.VknTckn ?? "",
                        receipt_date = parsedData.ReceiptDate,
                        receipt_no = parsedData.ReceiptNo ?? "",
                        total_amount = parsedData.TotalAmount,
                        tax_amount = parsedData.TaxAmount,
                        matrah1 = parsedData.Matrah1,
                        kdv1 = parsedData.Kdv1,
                        matrah10 = parsedData.Matrah10,
                        kdv10 = parsedData.Kdv10,
                        matrah20 = parsedData.Matrah20,
                        kdv20 = parsedData.Kdv20,
                        image_path = pdfPages[0],
                        pdf_pages = pdfPages,
                        pdf_page_count = pageCount,
                        current_page = 0,
                        items = new string[] {}
                    });
                }
                else
                {
                    // Run image preprocessing (resizing + saving)
                    using var stream = file.OpenReadStream();
                    var preprocessResult = await _preprocessingService.ProcessAsync(stream, file.FileName);

                    string relativePath = $"data/{preprocessResult.ProcessedFileName}";
                    string absolutePath = Path.Combine(BaseDir, preprocessResult.ProcessedFileName);

                    // Read bytes of optimized image
                    byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(absolutePath);

                    // Call Gemini for real OCR extraction
                    var parsedData = await _geminiService.ScanReceiptAsync(imageBytes, file.ContentType);

                    WriteLog("Guest", "OCR_Okuma", "SUCCESS", $"Gemini OCR başarıyla tamamlandı. Algılanan Mağaza: {parsedData.MerchantName}");

                    // Returns properties matching frontend Receipt models
                    return Ok(new
                    {
                        merchant_name = parsedData.MerchantName,
                        vkn_tckn = parsedData.VknTckn ?? "",
                        receipt_date = parsedData.ReceiptDate,
                        receipt_no = parsedData.ReceiptNo ?? "",
                        total_amount = parsedData.TotalAmount,
                        tax_amount = parsedData.TaxAmount,
                        matrah1 = parsedData.Matrah1,
                        kdv1 = parsedData.Kdv1,
                        matrah10 = parsedData.Matrah10,
                        kdv10 = parsedData.Kdv10,
                        matrah20 = parsedData.Matrah20,
                        kdv20 = parsedData.Kdv20,
                        image_path = relativePath,
                        items = new string[] {}
                    });
                }
            }
            catch (Exception ex)
            {
                WriteLog("Guest", "OCR_Okuma", "ERROR", $"OCR Okuma hatası: {ex.Message}");
                return StatusCode(500, $"Yapay zeka okuma hatası: {ex.Message}");
            }
        }

        [HttpPost("ocr-image")]
        public async Task<IActionResult> RunOcrOnImage([FromBody] OcrImageRequest request)
        {
            if (string.IsNullOrEmpty(request.ImagePath))
            {
                return BadRequest("Image path is required.");
            }

            try
            {
                var filename = Path.GetFileName(request.ImagePath);
                var absolutePath = Path.Combine(BaseDir, filename);

                if (!System.IO.File.Exists(absolutePath))
                {
                    return NotFound("Image file not found.");
                }

                byte[] imageBytes = await System.IO.File.ReadAllBytesAsync(absolutePath);
                var parsedData = await _geminiService.ScanReceiptAsync(imageBytes, "image/jpeg");

                return Ok(new
                {
                    merchant_name = parsedData.MerchantName,
                    vkn_tckn = parsedData.VknTckn ?? "",
                    receipt_date = parsedData.ReceiptDate,
                    receipt_no = parsedData.ReceiptNo ?? "",
                    total_amount = parsedData.TotalAmount,
                    tax_amount = parsedData.TaxAmount,
                    matrah1 = parsedData.Matrah1,
                    kdv1 = parsedData.Kdv1,
                    matrah10 = parsedData.Matrah10,
                    kdv10 = parsedData.Kdv10,
                    matrah20 = parsedData.Matrah20,
                    kdv20 = parsedData.Kdv20,
                    image_path = request.ImagePath
                });
            }
            catch (Exception ex)
            {
                WriteLog("Guest", "OCR_Okuma", "ERROR", $"Görsel OCR okuma hatası: {ex.Message}");
                return StatusCode(500, $"Yapay zeka okuma hatası: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllReceipts([FromQuery] string? username)
        {
            string userFilter = username ?? "Tümü";
            WriteLog(userFilter, "Veri_Listeleme", "INFO", $"Kayıtlı fiş listesi istendi. Kullanıcı: {userFilter}");
            
            var query = _context.Expenses.AsQueryable();
            if (!string.IsNullOrEmpty(username))
            {
                query = query.Where(r => r.KaydedenKullanici == username);
            }
            var expenses = await query.OrderByDescending(r => r.Id).ToListAsync();

            // Map database schema values to frontend model specifications
            var responseList = expenses.Select(e => {
                return new {
                    id = e.Id,
                    merchantName = e.FirmaAdi,
                    vknTckn = e.VknTckn ?? "",
                    receiptDate = e.Tarih,
                    receiptNo = e.FisNo ?? "",
                    totalAmount = e.ToplamTutar,
                    taxAmount = e.KdvTutari,
                    kdvOrani = e.KdvOrani,
                    imagePath = e.ImagePath,
                    createdAt = e.CreatedDate,
                    createdBy = e.KaydedenKullanici
                };
            });

            return Ok(responseList);
        }

        // 3. API: Get single receipt details
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReceiptDetails(int id)
        {
            WriteLog("Guest", "Detay_Görüntüleme", "INFO", $"Fiş detayı istendi. ID: {id}");
            var expense = await _context.Expenses.FirstOrDefaultAsync(r => r.Id == id);

            if (expense == null)
            {
                WriteLog("Guest", "Detay_Görüntüleme", "WARNING", $"İstenen fiş bulunamadı. ID: {id}");
                return NotFound("Fiş bulunamadı.");
            }

            var itemsList = new List<GeminiReceiptItemResponse>();
            if (!string.IsNullOrEmpty(expense.ItemsJson))
            {
                itemsList = JsonSerializer.Deserialize<List<GeminiReceiptItemResponse>>(expense.ItemsJson) ?? new();
            }

            // Respond as a structured Receipt object with items array
            return Ok(new
            {
                id = expense.Id,
                merchantName = expense.FirmaAdi,
                vknTckn = expense.VknTckn ?? "",
                receiptDate = expense.Tarih,
                receiptNo = expense.FisNo ?? "",
                totalAmount = expense.ToplamTutar,
                taxAmount = expense.KdvTutari,
                kdvOrani = expense.KdvOrani,
                imagePath = expense.ImagePath,
                createdAt = expense.CreatedDate,
                createdBy = expense.KaydedenKullanici,
                items = new string[] {}
            });
        }

        private Expense CreateExpenseFromDto(ReceiptSaveDto dto)
        {
            var vatItems = new List<VatBreakdownItem>();
            if (dto.Matrah1 > 0 || dto.Kdv1 > 0)
            {
                vatItems.Add(new VatBreakdownItem { KdvOrani = 1, Matrah = dto.Matrah1, Kdv = dto.Kdv1 });
            }
            if (dto.Matrah10 > 0 || dto.Kdv10 > 0)
            {
                vatItems.Add(new VatBreakdownItem { KdvOrani = 10, Matrah = dto.Matrah10, Kdv = dto.Kdv10 });
            }
            if (dto.Matrah20 > 0 || dto.Kdv20 > 0)
            {
                vatItems.Add(new VatBreakdownItem { KdvOrani = 20, Matrah = dto.Matrah20, Kdv = dto.Kdv20 });
            }

            var itemsJson = System.Text.Json.JsonSerializer.Serialize(vatItems);

            return new Expense
            {
                FirmaAdi = dto.MerchantName,
                Tarih = dto.ReceiptDate,
                ToplamTutar = dto.TotalAmount,
                KdvTutari = dto.TaxAmount,
                FisNo = dto.FisNo,
                VknTckn = dto.VknTckn,
                KdvOrani = dto.KdvOrani,
                KaydedenKullanici = string.IsNullOrEmpty(dto.CreatedBy) ? "default" : dto.CreatedBy,
                CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ImagePath = dto.ImagePath,
                ItemsJson = itemsJson
            };
        }

        [HttpPost]
        public async Task<IActionResult> SaveReceipt([FromBody] ReceiptSaveDto dto)
        {
            WriteLog(dto.CreatedBy, "Veri_Kayıt", "INFO", $"Yeni bir fiş kaydetme isteği alındı. Mağaza: {dto.MerchantName}, Tutar: {dto.TotalAmount} TL, Kullanıcı: {dto.CreatedBy}");
            try
            {
                var exp = CreateExpenseFromDto(dto);
                _context.Expenses.Add(exp);
                await _context.SaveChangesAsync();

                int primaryReceiptId = exp.Id;

                WriteLog(dto.CreatedBy, "Veri_Kayıt", "SUCCESS", $"Fiş başarıyla kaydedildi. Veritabanı ID: {primaryReceiptId}");

                // Update Excel file
                ExecuteExcelUpdate(string.IsNullOrEmpty(dto.CreatedBy) ? "default" : dto.CreatedBy);

                // Backup logs
                ExecuteLogBackup();

                return Ok(new
                {
                    status = "success",
                    receipt_id = primaryReceiptId,
                    message = "Fiş veritabanına kaydedildi."
                });
            }
            catch (Exception ex)
            {
                WriteLog(dto.CreatedBy, "Veri_Kayıt", "ERROR", $"Fiş kaydedilirken hata: {ex.Message}");
                return StatusCode(500, $"Veritabanı kayıt hatası: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReceipt(int id, [FromBody] ReceiptSaveDto dto)
        {
            string creator = dto.CreatedBy ?? "default";
            WriteLog(creator, "Veri_Güncelleme", "INFO", $"Fiş güncelleme isteği alındı. ID: {id}, Mağaza: {dto.MerchantName}");
            try
            {
                var dbExpense = await _context.Expenses.FirstOrDefaultAsync(r => r.Id == id);

                if (dbExpense == null)
                {
                    WriteLog(creator, "Veri_Güncelleme", "WARNING", $"Güncellenecek fiş bulunamadı. ID: {id}");
                    return NotFound("Fiş bulunamadı.");
                }

                // Update fields
                dbExpense.FirmaAdi = dto.MerchantName;
                dbExpense.Tarih = dto.ReceiptDate;
                dbExpense.FisNo = dto.FisNo;
                dbExpense.VknTckn = dto.VknTckn;
                dbExpense.ToplamTutar = dto.TotalAmount;
                dbExpense.KdvTutari = dto.TaxAmount;
                dbExpense.KdvOrani = dto.KdvOrani;
                
                if (!string.IsNullOrEmpty(dto.ImagePath))
                {
                    dbExpense.ImagePath = dto.ImagePath;
                }
                if (!string.IsNullOrEmpty(dto.CreatedBy))
                {
                    dbExpense.KaydedenKullanici = dto.CreatedBy;
                }

                // Serialize KDV breakdown to ItemsJson
                var vatItems = new List<VatBreakdownItem>();
                if (dto.Matrah1 > 0 || dto.Kdv1 > 0)
                {
                    vatItems.Add(new VatBreakdownItem { KdvOrani = 1, Matrah = dto.Matrah1, Kdv = dto.Kdv1 });
                }
                if (dto.Matrah10 > 0 || dto.Kdv10 > 0)
                {
                    vatItems.Add(new VatBreakdownItem { KdvOrani = 10, Matrah = dto.Matrah10, Kdv = dto.Kdv10 });
                }
                if (dto.Matrah20 > 0 || dto.Kdv20 > 0)
                {
                    vatItems.Add(new VatBreakdownItem { KdvOrani = 20, Matrah = dto.Matrah20, Kdv = dto.Kdv20 });
                }
                dbExpense.ItemsJson = System.Text.Json.JsonSerializer.Serialize(vatItems);

                await _context.SaveChangesAsync();

                WriteLog(creator, "Veri_Güncelleme", "SUCCESS", $"Fiş başarıyla güncellendi. ID: {id}");

                // Update Excel file
                ExecuteExcelUpdate(dbExpense.KaydedenKullanici);

                // Backup logs
                ExecuteLogBackup();

                return Ok(new { status = "success", message = "Fiş güncellendi." });
            }
            catch (Exception ex)
            {
                WriteLog(creator, "Veri_Güncelleme", "ERROR", $"Fiş güncellenirken hata: {ex.Message}");
                return StatusCode(500, $"Veritabanı güncelleme hatası: {ex.Message}");
            }
        }

        [HttpGet("export")]
        public IActionResult ExportExcel([FromQuery] string? username)
        {
            string targetUser = username ?? "default";
            WriteLog(targetUser, "Excel_İndirme", "INFO", "Excel raporu indirme isteği alındı.");
            string userExcelFile = Path.Combine(BaseDir, $"fis_raporu_{targetUser}.xlsx");

            if (!System.IO.File.Exists(userExcelFile))
            {
                ExecuteExcelUpdate(targetUser);
            }

            if (!System.IO.File.Exists(userExcelFile))
            {
                return NotFound("Rapor dosyası oluşturulamadı.");
            }

            var bytes = System.IO.File.ReadAllBytes(userExcelFile);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"fis_raporu_{targetUser}.xlsx");
        }

        // DELETE: api/receipts/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReceipt(int id)
        {
            try
            {
                var expense = await _context.Expenses.FindAsync(id);
                if (expense == null)
                {
                    return NotFound("Kayıt bulunamadı.");
                }

                string creator = expense.KaydedenKullanici;
                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();

                WriteLog(creator, "Veri_Silme", "SUCCESS", $"Fiş başarıyla silindi. ID: {id}");

                // Update Excel file
                ExecuteExcelUpdate(creator);

                return Ok(new { status = "success", message = "Masraf kaydı silindi." });
            }
            catch (Exception ex)
            {
                WriteLog("System", "Veri_Silme", "ERROR", $"Fiş silinirken hata: {ex.Message}");
                return StatusCode(500, $"Veritabanından silme hatası: {ex.Message}");
            }
        }
    }

    public class OcrImageRequest
    {
        public string ImagePath { get; set; } = string.Empty;
    }

    // DTO for saving/updating receipts sent from the frontend
    public class ReceiptSaveDto
    {
        public int Id { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public string ReceiptDate { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public int KdvOrani { get; set; } = 20;
        public decimal Matrah1 { get; set; }
        public decimal Kdv1 { get; set; }
        public decimal Matrah10 { get; set; }
        public decimal Kdv10 { get; set; }
        public decimal Matrah20 { get; set; }
        public decimal Kdv20 { get; set; }
        public string? Category { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ImagePath { get; set; }
        public string CreatedBy { get; set; } = "default";
        public string? VknTckn { get; set; }
        public string? FisNo { get; set; }
        public List<GeminiReceiptItemResponse> Items { get; set; } = new();
    }

    public class VatBreakdownItem
    {
        public int KdvOrani { get; set; }
        public decimal Matrah { get; set; }
        public decimal Kdv { get; set; }
    }
}
