@echo off
:: SQL Server Veritabanı Yedekleme Çalıştırıcısı (Ayarlar Tablosundaki Klasör Yolunu Kullanır)
sqlcmd -S localhost -E -d ReceiptOcrDb_New -Q "EXEC sp_BackupDatabase;"
echo Yedekleme islemi tetiklendi.
timeout /t 5
