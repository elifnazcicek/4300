using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReceiptOCR.API.Models;

namespace ReceiptOCR.API.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiService> _logger;
        private readonly string _apiKey;
        private readonly string _modelName;

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            // Read API key from environment variable first, then from appsettings configuration
            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
                      ?? configuration["Gemini:ApiKey"] 
                      ?? configuration["GeminiApiKey"] 
                      ?? "YOUR_API_KEY_HERE"; // Fallback placeholder
            
            _modelName = configuration["Gemini:ModelName"] 
                         ?? configuration["ModelName"] 
                         ?? "gemini-2.5-flash";
        }

        public async Task<GeminiReceiptResponse> ScanReceiptAsync(byte[] imageBytes, string contentType = "image/jpeg")
        {
            _logger.LogInformation("Gemini API'sine fiş okuma isteği gönderiliyor...");

            // Safe check if API key is not set or placeholder
            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "YOUR_API_KEY_HERE" || string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("Gemini API anahtarı ayarlanmamış!");
                throw new InvalidOperationException("Gemini API anahtarı ayarlanmamış veya eksik.");
            }

            string base64Image = Convert.ToBase64String(imageBytes);

            // Construct JSON request body for Gemini 2.0 Flash
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = "Extract details and KDV rates breakdown from this receipt image. You MUST return a JSON object with this exact structure: { \"merchantName\": \"Store Name\", \"vknTckn\": \"12345678901\", \"receiptDate\": \"YYYY-MM-DD\", \"receiptNo\": \"Fis No\", \"totalAmount\": 123.45, \"taxAmount\": 12.34, \"matrah1\": 10.00, \"kdv1\": 0.10, \"matrah10\": 50.00, \"kdv10\": 5.00, \"matrah20\": 50.00, \"kdv20\": 10.00 }. Do not include markdown code block formatting (no ```json). Fill as much detail as possible. vknTckn is the 10-digit or 11-digit Tax ID (VKN) or T.C. Kimlik Number (TCKN) found on the receipt. receiptNo is the receipt/invoice number (usually labelled as FİŞ NO, FATURA NO, EFATURA NO, etc.). For each KDV rate (%1, %10, %20) present in the receipt, group the products/items under that rate and compute its total matrah (taxable net amount) and kdv (tax amount). If a rate is not present, set its matrah and kdv to 0.00." },
                            new { inlineData = new { mimeType = contentType, data = base64Image } }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            string jsonRequest = JsonSerializer.Serialize(requestBody);
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";

            try
            {
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API hata döndürdü: {StatusCode} - {Error}", response.StatusCode, errorResponse);
                    throw new HttpRequestException($"Gemini API isteği başarısız oldu: {response.StatusCode}");
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                
                // Extract response text from Gemini structure: candidates[0].content.parts[0].text
                var root = doc.RootElement;
                string textResult = root
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                _logger.LogInformation("Gemini API'den gelen yanıt: {Text}", textResult);

                // Clean markdown artifacts from response if any
                string cleanText = textResult ?? string.Empty;
                cleanText = cleanText.Trim();
                if (cleanText.StartsWith("```json")) cleanText = cleanText.Substring(7);
                else if (cleanText.StartsWith("```")) cleanText = cleanText.Substring(3);
                if (cleanText.EndsWith("```")) cleanText = cleanText.Substring(0, cleanText.Length - 3);
                else if (cleanText.EndsWith("``")) cleanText = cleanText.Substring(0, cleanText.Length - 2);
                else if (cleanText.EndsWith("`")) cleanText = cleanText.Substring(0, cleanText.Length - 1);
                cleanText = cleanText.Trim();

                // Parse the response text as our GeminiReceiptResponse model
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsedData = JsonSerializer.Deserialize<GeminiReceiptResponse>(cleanText, options);

                if (parsedData == null)
                {
                    throw new JsonException("Gemini yanıtı çözümlenemedi.");
                }

                return parsedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini OCR okuma işleminde hata oluştu.");
                throw;
            }
        }

        private Expense GenerateMockOcrData()
        {
            var receiptTemplates = new[] {
                new {
                    MerchantName = "MİGROS TİC. A.Ş.",
                    Items = new[] {
                        new { ItemName = "SÜT 1L TAMS YAĞLI", Quantity = 2.0, UnitPrice = 32.50m, TotalPrice = 65.00m, TaxRate = 10 },
                        new { ItemName = "MAKARNA 500G", Quantity = 3.0, UnitPrice = 15.00m, TotalPrice = 45.00m, TaxRate = 10 },
                        new { ItemName = "SÜTAŞ KAŞAR 400G", Quantity = 1.0, UnitPrice = 140.00m, TotalPrice = 140.00m, TaxRate = 10 }
                    }
                },
                new {
                    MerchantName = "BİM BİRLEŞİK MAĞAZALAR",
                    Items = new[] {
                        new { ItemName = "DOST YOGURT 3KG", Quantity = 1.0, UnitPrice = 95.00m, TotalPrice = 95.00m, TaxRate = 10 },
                        new { ItemName = "EFSANE PİRİNÇ 2KG", Quantity = 1.0, UnitPrice = 78.00m, TotalPrice = 78.00m, TaxRate = 10 },
                        new { ItemName = "KOLA 2.5L", Quantity = 2.0, UnitPrice = 45.00m, TotalPrice = 90.00m, TaxRate = 20 }
                    }
                },
                new {
                    MerchantName = "SHELL PETROL A.Ş.",
                    Items = new[] {
                        new { ItemName = "FUEL SAVE KURŞUNSUZ 95", Quantity = 25.4, UnitPrice = 42.50m, TotalPrice = 1079.50m, TaxRate = 20 }
                    }
                },
                new {
                    MerchantName = "STARBUCKS COFFEE",
                    Items = new[] {
                        new { ItemName = "CAPPACCINO GRANDE", Quantity = 1.0, UnitPrice = 95.00m, TotalPrice = 95.00m, TaxRate = 10 },
                        new { ItemName = "HAVUÇLU KEK", Quantity = 1.0, UnitPrice = 75.00m, TotalPrice = 75.00m, TaxRate = 10 }
                    }
                }
            };

            var random = new Random();
            var template = receiptTemplates[random.Next(receiptTemplates.Length)];

            var total = template.Items.Sum(i => i.TotalPrice);
            var maxTaxRate = template.Items.Max(i => i.TaxRate);

            var categories = new[] { "Gıda", "Giyim", "Akaryakıt", "Ulaşım", "Ofis Malzemesi", "Diğer" };
            var paymentMethods = new[] { "Nakit", "Kredi Kartı" };

            return new Expense
            {
                FirmaAdi = template.MerchantName,
                Tarih = DateTime.Now.ToString("yyyy-MM-dd"),
                ToplamTutar = Math.Round(total, 2),
                KdvOrani = maxTaxRate,
                CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ItemsJson = JsonSerializer.Serialize(template.Items)
            };
        }
    }

    // Helper classes for deserialization
    public class GeminiReceiptResponse
    {
        public string MerchantName { get; set; }
        public string VknTckn { get; set; }
        public string ReceiptDate { get; set; }
        public string ReceiptNo { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Matrah1 { get; set; }
        public decimal Kdv1 { get; set; }
        public decimal Matrah10 { get; set; }
        public decimal Kdv10 { get; set; }
        public decimal Matrah20 { get; set; }
        public decimal Kdv20 { get; set; }
    }

    public class GeminiReceiptItemResponse
    {
        public string ItemName { get; set; }
        public double Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int TaxRate { get; set; }
    }
}
