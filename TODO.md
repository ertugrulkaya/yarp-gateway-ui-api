# YARP Proxy Manager — Fix & Improvement Backlog

> Kaynak: 3 uzman panel analizi (🔴 .NET Lead · 🟦 Angular Lead · 🟨 DB & Architecture Expert)
> Oluşturma: 2026-04-18

---

## 🚨 TİER 1 — KRİTİK (Production öncesi zorunlu)

### T1-1 · JWT Secret Key — Environment Variable'a Taşı
- **Uzman:** 🔴 🟨
- **Dosyalar:** `Proxy.Host/Program.cs`, `Proxy.Host/appsettings.json`
- **Sorun:** JWT signing key source code'da ve appsettings.json'da açık metin. Git history'de görünür. Tüm token'lar taklit edilebilir.
- **Yapılacak:**
  - `appsettings.json`'dan `Jwt:Key` satırını kaldır
  - `dotnet user-secrets set "Jwt:Key" "..."` ile local dev'de set et
  - Production için environment variable (`JWT__KEY`) kullan
  - `Program.cs`'de hardcoded fallback string'i kaldır, key yoksa startup'ta exception fırlat
- **Tahmini süre:** 30 dakika
- [x] Tamamlandı

---

### T1-2 · Default Admin Şifresi — First-Run'da Zorla Değiştir
- **Uzman:** 🔴
- **Dosyalar:** `Proxy.Host/Services/LiteDbService.cs`, `Proxy.Host/Controllers/AuthController.cs`
- **Sorun:** `"Rexadmin1234."` source code'da hardcoded. Default credential herkesçe bilinir.
- **Yapılacak:**
  - `User` modeline `MustChangePassword` (bool) alanı ekle
  - `EnsureDefaultAdminExists()` kullanıcıyı `MustChangePassword = true` ile oluştur
  - `AuthController.Login` response'unda `mustChangePassword` flag'i döndür
  - Angular login sayfası bu flag'i görünce zorunlu şifre değiştirme ekranına yönlendirsin
  - Şifre değiştirilmeden API çağrıları 403 döndürsün (middleware veya action filter ile)
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T1-3 · `UpdateRawConfig` Atomicity — Rollback Mekanizması
- **Uzman:** 🔴 🟨
- **Dosyalar:** `Proxy.Host/Controllers/ProxyConfigController.cs`
- **Sorun:** `DeleteAll()` sonrası `InsertBulk()` öncesinde exception/network hatası olursa DB tamamen boş kalır. YARP hiç request proxy'lemez.
- **Yapılacak:**
  - Mevcut config'i işlem öncesinde belleğe al (backup)
  - `DeleteAll()` + `InsertBulk()` try içinde çalıştır
  - Exception durumunda backup'tan geri yükle
  - `_provider.UpdateConfig()` sadece başarıda çağır
  - Alternatif: yeni collection'a yaz, başarıysa swap et
- **Örnek yaklaşım:**
  ```csharp
  var routeBackup = routesCol.FindAll().ToList();
  var clusterBackup = clustersCol.FindAll().ToList();
  try {
      routesCol.DeleteAll(); clustersCol.DeleteAll();
      // InsertBulk...
      _provider.UpdateConfig();
  } catch {
      routesCol.DeleteAll(); clustersCol.DeleteAll();
      routesCol.InsertBulk(routeBackup);
      clustersCol.InsertBulk(clusterBackup);
      throw;
  }
  ```
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T1-4 · `CancellationTokenSource` Memory Leak + Broken Change Token
- **Uzman:** 🔴 🟨
- **Dosyalar:** `Proxy.Host/Providers/LiteDbProxyConfigProvider.cs`
- **Sorun:**
  - `LiteDbProxyConfig` içindeki `_cts` `readonly` — her `SignalChange()` sonrası yeni CTS oluşturulmuyor
  - İkinci config güncellemesinde change token sinyal vermez → YARP stale config kullanır
  - Eski `LiteDbProxyConfig` nesneleri dispose edilmeden heap'te birikir
- **Yapılacak:**
  - `UpdateConfig()` içinde yeni `LiteDbProxyConfig` oluştururken CTS'yi doğru yönet
  - `LiteDbProxyConfig`'i `IDisposable` yap, `_cts.Dispose()` çağır
  - Her `UpdateConfig()` çağrısında eski config dispose edilsin
  - `GetConfig()` thread-safe olacak şekilde `volatile` veya lock kullan
- **Tahmini süre:** 2 saat
- [x] Tamamlandı

---

### T1-5 · `deleteRoute` / `deleteCluster` — `markForCheck()` Eksik
- **Uzman:** 🟦
- **Dosyalar:** `Proxy.UI/src/app/pages/dashboard/dashboard.ts`
- **Sorun:** `provideZonelessChangeDetection()` kullanılıyor. `delete` işlemlerinde `this.routes.filter(...)` sonrası `markForCheck()` çağrılmıyor → UI güncellenmeyebilir.
- **Yapılacak:**
  ```typescript
  // deleteRoute next:
  this.routes = this.routes.filter(r => r.routeId !== route.routeId);
  this.cdr.markForCheck(); // ← EKLE

  // deleteCluster next:
  this.clusters = this.clusters.filter(c => c.clusterId !== cluster.clusterId);
  this.cdr.markForCheck(); // ← EKLE
  ```
- **Tahmini süre:** 15 dakika
- [x] Tamamlandı

---

### T1-6 · `EnableMultipleHttp1Connections` Mapping Eksik
- **Uzman:** 🟨
- **Dosyalar:** `Proxy.Host/Providers/LiteDbProxyConfigProvider.cs`
- **Sorun:** `ConfigDtos.cs`'de alan var ama YARP v2.3 `HttpClientConfig` bu özelliği desteklemiyor. Alan DTO ve UI'dan kaldırıldı.
- **Tahmini süre:** 15 dakika
- [x] Tamamlandı (alan YARP'ta mevcut değil — DTO + UI'dan kaldırıldı)

---

## 🟠 TİER 2 — ÖNEMLİ (2 hafta içinde)

### T2-1 · Logging: Fire-and-Forget → Channel-Based Queue
- **Uzman:** 🔴 🟨
- **Dosyalar:** `Proxy.Host/Middleware/YarpLoggingMiddleware.cs`, `Proxy.Host/Services/LogService.cs`
- **Sorun:** `Task.Run(() => logService.LogRequest(...))` pattern'i:
  - Thread pool'u tüketir (yüksek yükte)
  - Shutdown sırasında loglar kaybolur
  - Exception'lar sessizce yutulur
  - LiteDB write lock tüm istekleri bloklar
- **Yapılacak:**
  - `LogService`'e `Channel<LogEntry>` ekle (unbounded veya bounded)
  - `IHostedService` implement eden `LogWriterService` oluştur — channel'dan okur, DB'ye yazar
  - `YarpLoggingMiddleware`'de sadece `channel.Writer.TryWrite(entry)` çağır (non-blocking)
  - Uygulama kapanırken channel drain edilsin (`StopAsync` içinde)
- **Tahmini süre:** 2 gün
- [x] Tamamlandı

---

### T2-2 · Config Provider Thread Safety — `ReaderWriterLockSlim`
- **Uzman:** 🟨
- **Dosyalar:** `Proxy.Host/Providers/LiteDbProxyConfigProvider.cs`
- **Sorun:** `_config` field'ı lock olmadan okunup yazılıyor. Multi-thread ortamda race condition olabilir.
- **Yapılacak:**
  ```csharp
  private readonly ReaderWriterLockSlim _lock = new();

  public IProxyConfig GetConfig() {
      _lock.EnterReadLock();
      try { return _config; }
      finally { _lock.ExitReadLock(); }
  }

  public void UpdateConfig() {
      var newConfig = LoadFromDb();
      _lock.EnterWriteLock();
      try {
          var old = _config;
          _config = newConfig;
          old.SignalChange();
      }
      finally { _lock.ExitWriteLock(); }
  }
  ```
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T2-3 · Login Brute Force Koruması — Rate Limiting
- **Uzman:** 🔴
- **Dosyalar:** `Proxy.Host/Controllers/AuthController.cs`, `Proxy.Host/Program.cs`
- **Sorun:** `/api/auth/login` endpoint'ine sınırsız istek gönderilebilir.
- **Yapılacak:**
  - `AspNetCoreRateLimit` paketi veya .NET 7+ built-in `RateLimiter` kullan
  - IP başına dakikada max 10 login denemesi
  - 5 başarısız denemede 15 dakika geçici kilit
  - Başarısız deneme sayısını `User` modeline veya in-memory cache'e kaydet
  - Locked durumda 429 Too Many Requests döndür
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T2-4 · FormArray Güvensiz Cast'leri — Null-Safe Yap
- **Uzman:** 🟦
- **Dosyalar:** `Proxy.UI/src/app/pages/dashboard/dialogs/route-dialog.ts`, `cluster-dialog.ts`
- **Sorun:** `this.form.get('headers') as FormArray` runtime'da null dönebilir, crash.
- **Yapılacak:**
  ```typescript
  // Getter'ları güvenli yap:
  get headers(): FormArray {
    const fa = this.form.get('headers');
    if (!(fa instanceof FormArray)) throw new Error('headers FormArray not found');
    return fa;
  }

  // Ya da optional chain ile:
  getTransformEntries(i: number): FormArray | null {
    const group = this.transforms.at(i);
    const fa = group?.get('entries');
    return fa instanceof FormArray ? fa : null;
  }
  ```
  - Template'de null check ekle: `@if (getTransformEntries(ti); as entries)`
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T2-5 · 401 Interceptor Race Condition — Snackbar + Redirect Çakışması
- **Uzman:** 🟦
- **Dosyalar:** `Proxy.UI/src/app/core/auth-interceptor.ts`
- **Sorun:** 401 alınca hem `router.navigate(['/login'])` başlatılıyor hem `throwError()` ile component'in snackbar'ı tetikleniyor. Kullanıcı hem hata görür hem redirect olur.
- **Yapılacak:**
  ```typescript
  if (err.status === 401) {
    localStorage.removeItem('access_token');
    router.navigate(['/login']);
    return EMPTY; // throwError yerine EMPTY — component'e hata yayılmaz
  }
  return throwError(() => err);
  ```
- **Tahmini süre:** 30 dakika
- [x] Tamamlandı

---

### T2-6 · Query String Loglardan Çıkar
- **Uzman:** 🔴
- **Dosyalar:** `Proxy.Host/Middleware/YarpLoggingMiddleware.cs`
- **Sorun:** Query string API token, şifre, kişisel veri içerebilir. Loglanmamalı.
- **Yapılacak:**
  - `LogEntry`'den `QueryString` alanını kaldır veya isteğe bağlı yap
  - Loglamak gerekiyorsa sensitive param'ları filtrele (`token`, `key`, `secret`, `password` içeren key'leri `***` ile maskele)
  - `YarpLoggingMiddleware`'de sadece path'i logla, query string'i değil
- **Tahmini süre:** 30 dakika
- [x] Tamamlandı

---

### T2-7 · Dialog Concurrency — Loading State & Duplicate Submit Koruması
- **Uzman:** 🟦
- **Dosyalar:** `Proxy.UI/src/app/pages/dashboard/dashboard.ts`, `dashboard.html`
- **Sorun:** Hızlı tıklamada birden fazla overlapping HTTP call oluşabilir. Save button'ı dialog kapandıktan sonra bile birden fazla kez tetiklenebilir.
- **Yapılacak:**
  - Dashboard'da `isLoading = false` signal/property ekle
  - Save başlarken `isLoading = true`, tamamlanınca `false`
  - "Add Route", "Add Cluster" butonlarını `[disabled]="isLoading"` yap
  - Route/cluster dialog `onSave()` içinde save butonu submit sonrası disable et
  - `loadConfig()` çağrılarını debounce et (switchMap veya flag ile)
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

## 🟡 TİER 3 — GELİŞTİRMELER (Backlog)

### T3-1 · Config Audit Trail — Kim Ne Zaman Değiştirdi
- **Uzman:** 🟨
- **Dosyalar:** `Proxy.Host/Models/` (yeni), `Proxy.Host/Controllers/ProxyConfigController.cs`
- **Sorun:** Config değişikliklerinin geçmişi yok. Production'da sorun çıkınca root cause analiz edilemiyor.
- **Yapılacak:**
  - `ConfigHistory` LiteDB collection'ı oluştur:
    ```csharp
    public class ConfigHistory {
        [BsonId] public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EntityType { get; set; } // "route" | "cluster"
        public string EntityId { get; set; }
        public string Action { get; set; } // "create" | "update" | "delete"
        public string ChangedBy { get; set; } // JWT'den alınan username
        public DateTime ChangedAt { get; set; }
        public string OldValueJson { get; set; }
        public string NewValueJson { get; set; }
    }
    ```
  - Her CRUD işleminde history kaydı ekle
  - `GET /api/proxyconfig/history` endpoint'i
  - Angular'da basit history sayfası (tarih, kim, ne değişti)
- **Tahmini süre:** 2-3 gün
- [x] Tamamlandı

---

### T3-2 · Health Check Endpoint
- **Uzman:** 🟨
- **Dosyalar:** `Proxy.Host/Program.cs`
- **Sorun:** Load balancer, Docker, Kubernetes instance sağlığını bilemiyor.
- **Yapılacak:**
  ```csharp
  builder.Services.AddHealthChecks()
      .AddCheck("litedb", () => {
          try {
              _db.GetCollection<RouteConfigWrapper>("routes").Count();
              return HealthCheckResult.Healthy();
          } catch (Exception ex) {
              return HealthCheckResult.Unhealthy(ex.Message);
          }
      });
  app.MapHealthChecks("/health");
  ```
  - DB erişimi, YARP provider durumu, log service durumu kontrol et
  - `/health` endpoint'i auth gerektirmesin
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T3-3 · API Error Response Standardizasyonu
- **Uzman:** 🟨
- **Dosyalar:** `Proxy.Host/Controllers/*.cs`
- **Sorun:** Farklı controller'larda `new { Error = ... }`, `new { Message = ... }` karışık formatlar. Frontend parsing zorlaşıyor.
- **Yapılacak:**
  - Standart `ApiErrorResponse` DTO oluştur:
    ```csharp
    public record ApiErrorResponse(string Code, string Message, Dictionary<string, string[]>? Errors = null);
    ```
  - Global exception handler middleware ekle (`UseExceptionHandler`)
  - Tüm BadRequest/NotFound/Conflict dönüşlerini standart formata taşı
  - Stack trace'i production'da response'a ekleme
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T3-4 · Config Backup / Restore Endpoint
- **Uzman:** 🟨
- **Dosyalar:** `Proxy.Host/Controllers/ProxyConfigController.cs`
- **Sorun:** DB corrupt olursa kurtarma yok.
- **Yapılacak:**
  - `GET /api/proxyconfig/backup` → tüm config'i JSON olarak indir (dosya download)
  - `POST /api/proxyconfig/restore` → JSON yükle, atomic import et
  - Dosya adına timestamp ekle: `proxy-config-2026-04-18.json`
  - Angular raw-editor'e "Export" ve "Import from file" butonları ekle
- **Tahmini süre:** 2 gün
- [x] Tamamlandı

---

### T3-5 · Signals ile State Management
- **Uzman:** 🟦
- **Dosyalar:** `Proxy.UI/src/app/pages/dashboard/dashboard.ts`, ilgili componentler
- **Sorun:** Zoneless mode'da plain properties + manual `markForCheck()` fragile. Signals + computed = granular otomatik güncelleme.
- **Yapılacak:**
  ```typescript
  // Plain property yerine:
  routes: RouteConfig[] = [];

  // Signal kullan:
  routes = signal<RouteConfig[]>([]);
  clusters = signal<ClusterConfig[]>([]);

  // Template'de:
  [dataSource]="routes()"
  ```
  - `markForCheck()` çağrıları kademeli olarak kaldır
  - `computed()` ile türetilen state'ler (örn. clusterIds listesi)
  - `effect()` ile side effect'ler (örn. snackbar)
- **Tahmini süre:** 3 gün
- [x] Tamamlandı

---

### T3-6 · `OnPush` Change Detection Strategy
- **Uzman:** 🟦
- **Dosyalar:** Tüm Angular component'ler
- **Sorun:** Default strategy zoneless ile birlikte verimsiz.
- **Yapılacak:**
  - Tüm component'lere `changeDetection: ChangeDetectionStrategy.OnPush` ekle
  - Signals kullanıldıktan sonra `markForCheck()` çağrıları zaten gereksiz olacak
  - İlk adım olarak dashboard, raw-editor, login component'leri
- **Tahmini süre:** 1 gün (T3-5 ile birlikte)
- [x] Tamamlandı

---

### T3-7 · LiteDB Index Stratejisi
- **Uzman:** 🟨
- **Dosyalar:** `Proxy.Host/Services/LiteDbService.cs`, `LogService.cs`
- **Sorun:** Routes/clusters collection'larında index yok. Logs'ta sadece Timestamp index'i var.
- **Yapılacak:**
  ```csharp
  // LiteDbService EnsureDefaultConfigExists() veya constructor'da:
  var routes = _db.GetCollection<RouteConfigWrapper>("routes");
  routes.EnsureIndex(x => x.ClusterId); // cluster'a göre route arama

  // LogService:
  logs.EnsureIndex(x => x.ClusterId);
  logs.EnsureIndex(x => x.StatusCode);
  logs.EnsureIndex(x => x.ClientIp);
  // Composite: timestamp + cluster
  ```
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T3-8 · Route/Cluster Validation — ClusterId Cross-Reference
- **Uzman:** 🔴
- **Dosyalar:** `Proxy.Host/Controllers/ProxyConfigController.cs`
- **Sorun:** Route kaydedilirken referans verilen `ClusterId`'nin gerçekten var olup olmadığı kontrol edilmiyor. YARP bunu startup'ta yakalyaabilir ama API bu hatayı önceden vermeli.
- **Yapılacak:**
  - `AddRoute` / `UpdateRoute` sırasında ClusterId'nin clusters collection'da var olduğunu kontrol et
  - Yoksa 422 Unprocessable Entity dön: `"Cluster 'xyz' not found"`
  - `POST /raw` için de aynı validasyon
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T3-9 · Native `confirm()` → Material Dialog ile Değiştir
- **Uzman:** 🟦
- **Dosyalar:** `Proxy.UI/src/app/pages/dashboard/dashboard.ts`
- **Sorun:** Browser native `confirm()` ugly, Material tasarımla uyumsuz, test edilemiyor.
- **Yapılacak:**
  - Basit `ConfirmDialogComponent` oluştur (title, message, confirm/cancel butonları)
  - `deleteRoute` ve `deleteCluster`'da `confirm()` yerine bu dialog'u kullan
  - Observable/Promise pattern ile sonucu al
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

### T3-10 · Swagger / OpenAPI Dokümantasyon
- **Uzman:** 🔴
- **Dosyalar:** `Proxy.Host/Program.cs`
- **Sorun:** API dokümantasyonu yok. Yeni geliştirici veya entegrasyon için endpoint'leri anlamak zor.
- **Yapılacak:**
  - `Swashbuckle.AspNetCore` paketi ekle
  - `app.UseSwagger()` + `app.UseSwaggerUI()` sadece development'ta
  - Controller action'larına XML comment + `[ProducesResponseType]` attribute'ları
  - JWT auth için Swagger'a Bearer token desteği ekle
- **Tahmini süre:** 1 gün
- [x] Tamamlandı

---

## 📊 ÖZET TABLO

| ID | Başlık | Tier | Uzman | Süre | Durum |
|----|--------|------|-------|------|-------|
| T1-1 | JWT Key → Environment Variable | 🚨 | 🔴🟨 | 30 dk | ⬜ |
| T1-2 | Default Şifre → First-Run Değiştirme | 🚨 | 🔴 | 1 gün | ⬜ |
| T1-3 | UpdateRawConfig Atomicity | 🚨 | 🔴🟨 | 1 gün | ⬜ |
| T1-4 | CancellationTokenSource Leak Fix | 🚨 | 🔴🟨 | 2 saat | ⬜ |
| T1-5 | deleteRoute/Cluster markForCheck() | 🚨 | 🟦 | 15 dk | ⬜ |
| T1-6 | EnableMultipleHttp1Connections Mapping | 🚨 | 🟨 | 15 dk | ⬜ |
| T2-1 | Logging Channel-Based Queue | 🟠 | 🔴🟨 | 2 gün | ⬜ |
| T2-2 | Config Provider Thread Safety | 🟠 | 🟨 | 1 gün | ⬜ |
| T2-3 | Login Rate Limiting | 🟠 | 🔴 | 1 gün | ⬜ |
| T2-4 | FormArray Null-Safe Cast | 🟠 | 🟦 | 1 gün | ⬜ |
| T2-5 | 401 Interceptor EMPTY Fix | 🟠 | 🟦 | 30 dk | ⬜ |
| T2-6 | Query String Log'dan Çıkar | 🟠 | 🔴 | 30 dk | ⬜ |
| T2-7 | Dialog Concurrency / Loading State | 🟠 | 🟦 | 1 gün | ⬜ |
| T3-1 | Audit Trail (ConfigHistory) | 🟡 | 🟨 | 3 gün | ⬜ |
| T3-2 | Health Check Endpoint | 🟡 | 🟨 | 1 gün | ⬜ |
| T3-3 | API Error Standardizasyonu | 🟡 | 🟨 | 1 gün | ⬜ |
| T3-4 | Config Backup / Restore | 🟡 | 🟨 | 2 gün | ⬜ |
| T3-5 | Signals ile State Management | 🟡 | 🟦 | 3 gün | ⬜ |
| T3-6 | OnPush Change Detection | 🟡 | 🟦 | 1 gün | ⬜ |
| T3-7 | LiteDB Index Stratejisi | 🟡 | 🟨 | 1 gün | ⬜ |
| T3-8 | Route→Cluster Cross-Reference Validasyon | 🟡 | 🔴 | 1 gün | ⬜ |
| T3-9 | Native confirm() → Material Dialog | 🟡 | 🟦 | 1 gün | ⬜ |
| T3-10 | Swagger / OpenAPI | 🟡 | 🔴 | 1 gün | ⬜ |

**Toplam Tier 1:** ~3.5 gün
**Toplam Tier 2:** ~7 gün
**Toplam Tier 3:** ~16 gün

---

*Her madde tamamlandığında `[ ]` → `[x]` işaretle.*
