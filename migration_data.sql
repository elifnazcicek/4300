USE ReceiptOcrDb_New;
GO

PRINT 'Migrating Expenses...';
SET IDENTITY_INSERT dbo.Expenses ON;
INSERT INTO dbo.Expenses (Id, Tarih, FirmaAdi, FisNo, KdvOrani, ToplamTutar, KdvTutari, KaydedenKullanici, CreatedDate, ItemsJson, Category, PaymentMethod, ImagePath) VALUES (1, '2026-06-24', N'Gemini Test Market', N'0042', 20, 150.5, 30.1, N'test_user', '2026-06-24 11:02:37', N'[]', N'Diğer', N'Nakit', NULL);
INSERT INTO dbo.Expenses (Id, Tarih, FirmaAdi, FisNo, KdvOrani, ToplamTutar, KdvTutari, KaydedenKullanici, CreatedDate, ItemsJson, Category, PaymentMethod, ImagePath) VALUES (2, '2023-11-10', N'UĞUR GIDA AĞAÇ AKS. İNŞ. SAN. TİC. LTD. ŞTİ.', N'0018', 20, 61.9, 4.53, N'admin', '2026-06-24 11:03:20', N'[]', N'Diğer', N'Nakit', N'data/processed_1782288185.jpg');
INSERT INTO dbo.Expenses (Id, Tarih, FirmaAdi, FisNo, KdvOrani, ToplamTutar, KdvTutari, KaydedenKullanici, CreatedDate, ItemsJson, Category, PaymentMethod, ImagePath) VALUES (3, '2024-02-01', N'ŞOK MARKETLER TİC. A.Ş', N'0259', 20, 427.42, 46.0, N'admin', '2026-06-24 11:07:03', N'[]', N'Diğer', N'Nakit', N'data/processed_1782288413.jpg');
SET IDENTITY_INSERT dbo.Expenses OFF;
GO

PRINT 'Migrating SystemLogs...';
SET IDENTITY_INSERT dbo.SystemLogs ON;
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (1, '2026-06-24 11:02:37', N'test_user', N'Veri_Kayıt', N'INFO', N'Yeni bir fiş kaydetme isteği alındı. Mağaza: Gemini Test Market, Tutar: 150,5 TL, Kullanıcı: test_user');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (2, '2026-06-24 11:02:37', N'test_user', N'Veri_Kayıt', N'SUCCESS', N'Fiş başarıyla kaydedildi. Veritabanı ID: 1');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (3, '2026-06-24 11:02:38', N'test_user', N'Excel_Kayıt', N'SUCCESS', N'Excel dosyası başarıyla güncellendi: fis_raporu_test_user.xlsx');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (4, '2026-06-24 11:02:38', N'System', N'Db_Backup', N'SUCCESS', N'Sistem logları başarıyla backup klasörüne yedeklendi. Yedek: app_20260624_110238.log');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (5, '2026-06-24 11:02:38', N'test_user', N'Veri_Listeleme', N'INFO', N'Kayıtlı fiş listesi istendi. Kullanıcı: test_user');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (6, '2026-06-24 11:02:56', N'admin', N'Veri_Listeleme', N'INFO', N'Kayıtlı fiş listesi istendi. Kullanıcı: admin');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (7, '2026-06-24 11:03:05', N'Guest', N'OCR_Okuma', N'INFO', N'Yeni bir fiş resmi yükleniyor: m1.jpg');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (8, '2026-06-24 11:03:09', N'Guest', N'OCR_Okuma', N'SUCCESS', N'Gemini OCR başarıyla tamamlandı. Algılanan Mağaza: UĞUR GIDA AĞAÇ AKS. İNŞ. SAN. TİC. LTD. ŞTİ.');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (9, '2026-06-24 11:03:20', N'admin', N'Veri_Kayıt', N'INFO', N'Yeni bir fiş kaydetme isteği alındı. Mağaza: UĞUR GIDA AĞAÇ AKS. İNŞ. SAN. TİC. LTD. ŞTİ., Tutar: 61,9 TL, Kullanıcı: admin');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (10, '2026-06-24 11:03:20', N'admin', N'Veri_Kayıt', N'SUCCESS', N'Fiş başarıyla kaydedildi. Veritabanı ID: 2');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (11, '2026-06-24 11:03:20', N'admin', N'Excel_Kayıt', N'SUCCESS', N'Excel dosyası başarıyla güncellendi: fis_raporu_admin.xlsx');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (12, '2026-06-24 11:03:20', N'System', N'Db_Backup', N'SUCCESS', N'Sistem logları başarıyla backup klasörüne yedeklendi. Yedek: app_20260624_110320.log');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (13, '2026-06-24 11:03:20', N'admin', N'Veri_Listeleme', N'INFO', N'Kayıtlı fiş listesi istendi. Kullanıcı: admin');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (14, '2026-06-24 11:06:42', N'admin', N'Veri_Listeleme', N'INFO', N'Kayıtlı fiş listesi istendi. Kullanıcı: admin');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (15, '2026-06-24 11:06:53', N'Guest', N'OCR_Okuma', N'INFO', N'Yeni bir fiş resmi yükleniyor: sok-market-fis-etiket-uyumsuzlugu-1.jpg');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (16, '2026-06-24 11:06:57', N'Guest', N'OCR_Okuma', N'SUCCESS', N'Gemini OCR başarıyla tamamlandı. Algılanan Mağaza: ŞOK MARKETLER TİC. A.Ş');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (17, '2026-06-24 11:07:03', N'admin', N'Veri_Kayıt', N'INFO', N'Yeni bir fiş kaydetme isteği alındı. Mağaza: ŞOK MARKETLER TİC. A.Ş, Tutar: 427,42 TL, Kullanıcı: admin');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (18, '2026-06-24 11:07:03', N'admin', N'Veri_Kayıt', N'SUCCESS', N'Fiş başarıyla kaydedildi. Veritabanı ID: 3');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (19, '2026-06-24 11:07:03', N'admin', N'Excel_Kayıt', N'SUCCESS', N'Excel dosyası başarıyla güncellendi: fis_raporu_admin.xlsx');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (20, '2026-06-24 11:07:03', N'System', N'Db_Backup', N'SUCCESS', N'Sistem logları başarıyla backup klasörüne yedeklendi. Yedek: app_20260624_110703.log');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (21, '2026-06-24 11:07:03', N'admin', N'Veri_Listeleme', N'INFO', N'Kayıtlı fiş listesi istendi. Kullanıcı: admin');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (22, '2026-06-24 11:07:40', N'Guest', N'Detay_Görüntüleme', N'INFO', N'Fiş detayı istendi. ID: 3');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (23, '2026-06-24 11:07:45', N'admin', N'Veri_Listeleme', N'INFO', N'Kayıtlı fiş listesi istendi. Kullanıcı: admin');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (24, '2026-06-24 11:07:52', N'admin', N'Veri_Güncelleme', N'INFO', N'Fiş güncelleme isteği alındı. ID: 3, Mağaza: ŞOK MARKETLER TİC. A.Ş');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (25, '2026-06-24 11:07:52', N'admin', N'Veri_Güncelleme', N'SUCCESS', N'Fiş başarıyla güncellendi. ID: 3');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (26, '2026-06-24 11:07:52', N'admin', N'Excel_Kayıt', N'SUCCESS', N'Excel dosyası başarıyla güncellendi: fis_raporu_admin.xlsx');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (27, '2026-06-24 11:07:52', N'admin', N'Veri_Listeleme', N'INFO', N'Kayıtlı fiş listesi istendi. Kullanıcı: admin');
INSERT INTO dbo.SystemLogs (Id, Timestamp, Username, ActionType, Status, Details) VALUES (28, '2026-06-24 11:07:54', N'admin', N'Excel_İndirme', N'INFO', N'Excel raporu indirme isteği alındı.');
SET IDENTITY_INSERT dbo.SystemLogs OFF;
GO
