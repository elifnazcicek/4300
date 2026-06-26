using System;

namespace ReceiptOCR.API.Models
{
    public class Expense
    {
        public int Id { get; set; }
        public string Tarih { get; set; } = string.Empty;
        public string FirmaAdi { get; set; } = string.Empty;
        public string? FisNo { get; set; }
        public string? VknTckn { get; set; }
        public int KdvOrani { get; set; } = 20;
        public decimal ToplamTutar { get; set; }
        public decimal KdvTutari { get; set; }
        public string KaydedenKullanici { get; set; } = "default";
        public string CreatedDate { get; set; } = string.Empty;
        public string? ItemsJson { get; set; }
        public string? ImagePath { get; set; }
    }
}
