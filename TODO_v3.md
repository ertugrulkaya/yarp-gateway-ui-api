# TODO_v3.md — Gelişim ve İyileştirme Analizi

> Product Owner: Selin Yıldız  
> Tarih: 2026-04-20  
> Kapsam: Backend, Test Coverage, UI/UX

---

## ✅ TAMAMLANDI — Kritik

### [DONE] Login/ChangePassword Input Validation
- **Dosya**: `Proxy.Host/Controllers/AuthController.cs`, `Proxy.Host/Models/AuthRequests.cs`
- **Çözüm**: DTO'lara `[Required]` + `[MinLength(8)]` eklendi, password karmaşıklık kontrolü (harf + rakam) eklendi

### [DONE] Cluster Silmede Route Referans Kontrolü
- **Dosya**: `Proxy.Host/Controllers/ProxyConfigController.cs:DeleteCluster`
- **Çözüm**: Cluster silinmeden önce referans eden route'lar kontrol ediliyor, varsa 422 hatası döndürülüyor

### [DONE] AuthController Test
- **Dosya**: `Proxy.Tests/Controllers/AuthControllerTests.cs`
- **Çözüm**: 13 test yazıldı (login validation, empty/null username/password, lockout, change password complexity)

---

## 🟡 ORTA — Bu Sprintte Yapılacak

### [MEMORY] LiteDbProxyConfigProvider Race Condition
- **Dosya**: `Proxy.Host/Providers/LiteDbProxyConfigProvider.cs:134-144`
- **Durum**: Mevcut kod zaten ReaderWriterLockSlim kullanıyor, race condition düşük risk
- **Öneri**: İleri aşamada iyileştirme olarak bakılabilir

### [MEMORY] LogWriterService Hata Sonrası Entry Kaybı
- **Dosya**: `Proxy.Host/Services/LogWriterService.cs:23-28`
- **Sorun**: `WriteToDb` hata olursa log entry kayboluyor, retry yok.
- **Öneri**: Failed entry'leri dead-letter queue'ya al veya retry mekanizması ekle.

### [BUG] Log Endpoint Exception Detail Sızıntısı ✅
- **Dosya**: `Proxy.Host/Controllers/LogsController.cs:37-40`
- **Çözüm**: `IsDevelopment()` kontrolü eklendi, sadece dev'de mesaj gösteriliyor

### [BUG] UpdateCluster Destination Address Validation Yok
- **Dosya**: `Proxy.Host/Controllers/ProxyConfigController.cs:258-265`
- **Sorun**: Destination adresleri için URL format validation yapılmıyor.
- **Öneri**: `Uri.TryCreate()` ile validation ekle.

### [UIUX] Offset/Limit Negative Kontrolü ✅
- **Dosya**: `LogsController.cs`, `ProxyConfigController.cs:GetHistory`
- **Çözüm**: limit clamp 1-1000, offset minimum 0

### [UIUX] Rate Limiter Retry-After Header Eksik
- **Dosya**: `Proxy.Host/Program.cs:95`
- **Durum**: Custom middleware gerekli, bu sprintte değmez
- **Öneri**: Sonraki aşamada

---

## 🟡 ORTA — Test Coverage

| Eksik Test | Öncelik |
|-----------|---------|
| Login validation (empty username/password) | 🔴 Kritik |
| Lockout süresi dolduktan sonra login | 🔴 Kritik |
| Rate limit aşımı | 🔴 Kritik |
| Change password validation | 🟡 Orta |
| Cluster silme + orphan route | 🔴 Kritik |
| Raw config validation hataları | 🟡 Orta |
| Restore transaction failure | 🟡 Orta |
| `LogService` disposal testi | 🟡 Orta |
| `LiteDbProxyConfigProvider` concurrent access | 🟡 Orta |

---

## 🟡 ORTA — Code Quality

### [BUG] History Transaction Gereksiz Kullanımı
- **Dosya**: `Proxy.Host/Controllers/ProxyConfigController.cs:337-349`
- **Sorun**: `BeginTrans()` açılıyor ama commitlenmiyor. LiteDB query'leri transaction gerektirmez.
- **Öneri**: Transaction'ı kaldır, basit query kullan.

### [BUG] Backup Büyük Dosya Memory Riskı
- **Dosya**: `Proxy.Host/Controllers/ProxyConfigController.cs:284-293`
- **Sorun**: Çok fazla veri varsa tüm JSON memory'e yüklenir.
- **Öneri**: Streaming response kullan veya boyut limiti koy.

---

## ✅ Tamamlanmış (Mevcut Durum)

- JWT auth + lockout + rate limiting çalışıyor
- YARP hot-reload provider çalışıyor
- LiteDB atomic writes (BeginTrans/Commit/Rollback) çalışıyor
- Channel-based async log queue çalışıyor
- Health check endpoint çalışıyor

---

## Öncelik Sıralaması

| # | Öncelik | Issue |
|---|--------|-------|
| 1 | 🔴 Kritik | Auth input validation ekle |
| 2 | 🔴 Kritik | Cluster silmede referans kontrolü ekle |
| 3 | 🔴 Kritik | AuthController test yaz |
| 4 | 🟡 Orta | Log exception detail sızıntısını düzelt |
| 5 | 🟡 Orta | Memory race condition düzelt |
| 6 | 🟡 Orta | Limit/offset validation ekle |
| 7 | 🟡 Orta | Rate limiter Retry-After ekle |

---

## Notlar

- Frontend UI/UX analizi ayrı yapılacak (şu an backend odaklı)
- Test coverage'ı artırmak için Mock kullanınız (Moq)
- `set_test_config.ps1` backend çalışırken çalıştırılmalı