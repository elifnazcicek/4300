@echo off
:: SQL Server Log Temizleme Çalıştırıcısı (localhost varsayılan sunucusu ile)
sqlcmd -S localhost -E -d ReceiptOcrDb_New -Q "EXEC sp_CleanOldLogs;"
echo Log temizleme islemi tetiklendi.
pause
