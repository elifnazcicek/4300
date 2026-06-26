using Microsoft.EntityFrameworkCore;
using ReceiptOCR.API.Models;

namespace ReceiptOCR.API.Data
{
    public class ReceiptDbContext : DbContext
    {
        public ReceiptDbContext(DbContextOptions<ReceiptDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<SystemLog> SystemLogs { get; set; }
        public DbSet<Setting> Settings { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.ToTable("Expenses");
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.Tarih).HasColumnName("Tarih")
                    .HasConversion(
                        v => string.IsNullOrEmpty(v) ? DateTime.Today : DateTime.Parse(v),
                        v => v.ToString("yyyy-MM-dd")
                    );
                entity.Property(e => e.FirmaAdi).HasColumnName("FirmaAdi");
                entity.Property(e => e.FisNo).HasColumnName("FisNo");
                entity.Property(e => e.VknTckn).HasColumnName("VknTckn");
                entity.Property(e => e.KdvOrani).HasColumnName("KdvOrani");
                entity.Property(e => e.ToplamTutar).HasColumnName("ToplamTutar");
                entity.Property(e => e.KdvTutari).HasColumnName("KdvTutari");
                entity.Property(e => e.KaydedenKullanici).HasColumnName("KaydedenKullanici");
                entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate")
                    .HasConversion(
                        v => string.IsNullOrEmpty(v) ? DateTime.Now : DateTime.Parse(v),
                        v => v.ToString("yyyy-MM-dd HH:mm:ss")
                    );
                entity.Property(e => e.ItemsJson).HasColumnName("ItemsJson");
                entity.Property(e => e.ImagePath).HasColumnName("ImagePath");
            });

            modelBuilder.Entity<SystemLog>(entity =>
            {
                entity.ToTable("SystemLogs");
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.Timestamp).HasColumnName("Timestamp")
                    .HasConversion(
                        v => string.IsNullOrEmpty(v) ? DateTime.Now : DateTime.Parse(v),
                        v => v.ToString("yyyy-MM-dd HH:mm:ss")
                    );
                entity.Property(e => e.Username).HasColumnName("Username");
                entity.Property(e => e.ActionType).HasColumnName("ActionType");
                entity.Property(e => e.Status).HasColumnName("Status");
                entity.Property(e => e.Details).HasColumnName("Details");
            });

            modelBuilder.Entity<Setting>(entity =>
            {
                entity.ToTable("Settings");
                entity.Property(e => e.Key).HasColumnName("Key");
                entity.Property(e => e.Value).HasColumnName("Value");
                entity.Property(e => e.Description).HasColumnName("Description");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.Username).HasColumnName("Username");
                entity.Property(e => e.PasswordHash).HasColumnName("PasswordHash");
                entity.Property(e => e.FullName).HasColumnName("FullName");
                entity.Property(e => e.Role).HasColumnName("Role");
                entity.Property(e => e.IsActive).HasColumnName("IsActive");
                entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
            });
        }
    }
}
