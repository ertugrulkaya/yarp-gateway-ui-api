# YARP Proxy Manager — Geliştirme Backlog v2

> Kaynak: 5 ajan teknik tasarım oturumu (🔵 PO · 🔴 .NET Lead · 🟢 Angular Lead · 🟡 DB/Arch Expert · 🎨 UI/UX Designer)
> Oluşturma: 2026-04-18 | Son güncelleme: 2026-04-18

---

## 🚨 BU RELEASE — KRİTİK & ÖNEMLİ

### T4-1 · `MustChangePassword` Enforcement — Backend Middleware + Angular Guard
- **Öncelik:** 🔴 Kritik
- **Uzman:** 🔴 🟢
- **Sorun:** `MustChangePassword = true` olan kullanıcı şifresini değiştirmeden tüm API endpoint'lerine erişebiliyor. JWT claim var ama backend middleware enforce etmiyor, Angular guard ise sadece UI'da yönlendiriyor — URL'yi direkt yazarak geçilebilir.
- **Backend Değişiklikler:**
  - `Program.cs` → `UseAuthorization()` sonrasına middleware ekle:
    ```csharp
    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.FindFirst("must_change_password")?.Value == "true"
            && !context.Request.Path.StartsWithSegments("/api/auth/change-password"))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(
                new ApiError("PASSWORD_CHANGE_REQUIRED",
                    "You must change your password before using the API."));
            return;
        }
        await next();
    });
    ```
  - `AuthController.cs` → `ChangePassword` endpoint'i bu middleware'den muaf tutulmalı (path zaten `/api/auth/change-password`)
- **Angular Değişiklikler:**
  - `change-password-guard.ts` → `CanActivateFn` implement et, token'dan `must_change_password` claim'i oku
  - `app.routes.ts` → tüm korumalı route'lara `changePasswordGuard` ekle (dashboard, raw-editor, history, logs)
  - `auth-interceptor.ts` → 403 + `PASSWORD_CHANGE_REQUIRED` kodu gelince `/change-password`'a redirect
- **Test Senaryosu:** Default admin ile giriş → şifre değiştirmeden `/api/proxyconfig/routes` call → 403 alınmalı
- **Tahmini süre:** 2 saat
- [x] Tamamlandı

---

### T4-2 · `LogWriterService` Graceful Shutdown — Channel Drain
- **Öncelik:** 🔴 Kritik
- **Uzman:** 🔴 🟡
- **Sorun:** `LogWriterService.StopAsync()` override edilmemiş. Uygulama kapanırken (deployment, restart, crash) `CancellationToken` iptal ediliyor ve `ReadAllAsync` döngüsü kırılıyor. Channel'da bekleyen loglar yazılmadan kayboluyor.
- **Dosya:** `Proxy.Host/Services/LogWriterService.cs`
- **Mevcut Kod:**
  ```csharp
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
      await foreach (var entry in _logService.Reader.ReadAllAsync(stoppingToken))
          try { _logService.WriteToDb(entry); } catch { }
  }
  ```
- **Yapılacak:**
  ```csharp
  public override async Task StopAsync(CancellationToken cancellationToken)
  {
      _logService.CompleteChannel(); // Writer'ı kapat, yeni entry kabul etme
      // Bekleyen tüm entry'leri drain et (max 5 saniye)
      using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(
          cancellationToken, drainCts.Token);
      try
      {
          await foreach (var entry in _logService.Reader.ReadAllAsync(linked.Token))
              try { _logService.WriteToDb(entry); } catch { }
      }
      catch (OperationCanceledException) { }
      await base.StopAsync(cancellationToken);
  }
  ```
  - `ExecuteAsync` içindeki `stoppingToken` ile `ReadAllAsync` zaten durdurulacak, `StopAsync` ek drain yapacak
- **Test Senaryosu:** Uygulama çalışırken 100 request at, hemen `Ctrl+C` yap → DB'de 100 log kaydı olmalı
- **Tahmini süre:** 30 dakika
- [x] Tamamlandı

---

### T4-3 · `LiteDbService` Constructor Sync I/O → `IHostedService.StartAsync()`
- **Öncelik:** 🔴 Kritik
- **Uzman:** 🔴 🟡
- **Sorun:** `LiteDbService` constructor'ında `EnsureDefaultAdminExists()` ve `EnsureDefaultConfigExists()` çağrılıyor. Constructor'da blocking disk I/O yapılması DI container initialization'ı yavaşlatır, hata yönetimi zorlaşır ve async pattern ile uyumsuz.
- **Dosya:** `Proxy.Host/Services/LiteDbService.cs`
- **Mevcut Kod:**
  ```csharp
  public LiteDbService(IConfiguration configuration)
  {
      var dbPath = configuration["LiteDb:Path"] ?? "proxy.db";
      _db = new LiteDatabase($"Filename={dbPath};Connection=shared");
      EnsureDefaultAdminExists();   // ← BURASI
      EnsureDefaultConfigExists();  // ← BURASI
  }
  ```
- **Yapılacak:**
  - `LiteDbService` → `IHostedService` implemente etsin (veya `BackgroundService`)
  - Constructor sadece `LiteDatabase` açsın
  - `StartAsync()` içinde seed metodlarını çağırsın
  - `Program.cs`'de `AddSingleton<LiteDbService>()` + `AddHostedService<LiteDbService>()` (aynı instance)
  - Alternatif (daha basit): `IStartupFilter` kullan
  ```csharp
  // Program.cs
  builder.Services.AddSingleton<LiteDbService>();
  builder.Services.AddHostedService(sp => sp.GetRequiredService<LiteDbService>()); // aynı instance
  ```
  ```csharp
  // LiteDbService.cs
  public class LiteDbService : IHostedService
  {
      public Task StartAsync(CancellationToken ct)
      {
          EnsureDefaultAdminExists();
          EnsureDefaultConfigExists();
          return Task.CompletedTask;
      }
      public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
  }
  ```
- **Tahmini süre:** 30 dakika
- [x] Tamamlandı

---

### T4-4 · Raw Editor `CanDeactivate` Guard — Kaydedilmemiş Değişiklik Uyarısı
- **Öncelik:** 🔴 Kritik
- **Uzman:** 🟢
- **Sorun:** Kullanıcı Raw Editor'da JSON konfigürasyonu düzenleyip kaydetmeden sayfadan ayrılırsa tüm değişiklikler sessizce kayboluyor. Enterprise bir proxy yönetim aracında bu kabul edilemez.
- **Dosya:** `Proxy.UI/src/app/pages/raw-editor/raw-editor.ts`, yeni `raw-editor-guard.ts`
- **Yapılacak:**
  - `raw-editor.ts` → `isDirty = signal(false)` ekle, `rawJson` değiştiğinde `true` yap, `saveConfig()` sonrası `false` yap
  - `raw-editor-guard.ts` oluştur:
    ```typescript
    export const rawEditorGuard: CanDeactivateFn<RawEditorComponent> = (component) => {
      if (!component.isDirty()) return true;
      return inject(MatDialog)
        .open(ConfirmDialogComponent, {
          width: '400px',
          data: {
            title: 'Unsaved Changes',
            message: 'You have unsaved changes. Leave without saving?',
            confirmLabel: 'Leave',
            cancelLabel: 'Stay',
          },
        })
        .afterClosed();
    };
    ```
  - `app.routes.ts` → raw-editor route'una `canDeactivate: [rawEditorGuard]` ekle
  - Template'de `(ngModelChange)="isDirty.set(true)"` bağlantısı
- **Tahmini süre:** 1 saat
- [x] Tamamlandı

---

### T4-5 · History Diff Dialog — `diff2html` Syntax Highlight
- **Öncelik:** 🟠 Önemli
- **Uzman:** 🟢
- **Sorun:** `HistoryDiffDialogComponent` şu an iki JSON string'i yan yana ham text olarak gösteriyor. Hangi alan değişti görülmüyor. Enterprise audit tool için kullanılamaz.
- **Dosya:** `Proxy.UI/src/app/pages/history/history-diff-dialog.ts`
- **Yapılacak:**
  - `npm install diff2html` (veya `npm install diff`)
  - Alternatif: `npm install ngx-json-diff` veya pure JS `jsdiff` + HTML render
  - `history-diff-dialog.ts` → old/new JSON karşılaştırması için unified diff üret
  - `history-diff-dialog.html` → diff output'u `innerHTML` ile render et
  - Stil: eklenen satırlar yeşil, silinen satırlar kırmızı arka plan
  - `diff2html` CSS'i `angular.json` styles array'ine ekle
  - Örnek:
    ```typescript
    import { createPatch } from 'diff';
    import { html } from 'diff2html';

    get diffHtml(): string {
      const patch = createPatch('config',
        JSON.stringify(JSON.parse(this.data.oldValue ?? '{}'), null, 2),
        JSON.stringify(JSON.parse(this.data.newValue ?? '{}'), null, 2));
      return html(patch, { drawFileList: false, matching: 'lines' });
    }
    ```
- **Tahmini süre:** 3-4 saat
- [x] Tamamlandı

---

### T4-6 · Log Tablosuna Server-Side Filtre
- **Öncelik:** 🟠 Önemli
- **Uzman:** 🔴 🟢 🟡
- **Sorun:** Log tablosu şu an sadece sayfalama destekliyor. 10.000+ kayıt arasında ClusterId, StatusCode veya IP'ye göre arama yapmak imkânsız. Frontend'e 10k kayıt çekilemez.
- **Backend Değişiklikler:**
  - `LogService.GetLogs()` → filtre parametreleri ekle:
    ```csharp
    public IEnumerable<LogEntry> GetLogs(
        int limit = 100, int offset = 0,
        string? clusterId = null,
        int? statusCode = null,
        string? clientIp = null,
        string? method = null)
    {
        var query = collection.Query();
        if (clusterId != null) query = query.Where(x => x.ClusterId == clusterId);
        if (statusCode != null) query = query.Where(x => x.StatusCode == statusCode);
        if (clientIp != null) query = query.Where(x => x.ClientIp == clientIp);
        if (method != null) query = query.Where(x => x.Method == method);
        return query.OrderByDescending(x => x.Timestamp).Offset(offset).Limit(limit).ToEnumerable();
    }
    ```
  - `GetTotalCount()` da aynı filtrelerle çalışmalı
  - `LogsController` (veya mevcut controller) → query parameter'lar: `?clusterId=&statusCode=&clientIp=&method=`
- **Angular Değişiklikler:**
  - `logs.ts` → filtre form ekle (`FormGroup` ile clusterId, statusCode, clientIp, method alanları)
  - `logs.html` → arama alanları (mat-form-field, mat-select) — collapsible filter panel
  - `logs.service.ts` → `getLogs()` metoduna filtre parametreleri ekle
  - Filter değiştiğinde `pageIndex = 0` resetle ve `loadLogs()` çağır
  - "Clear Filters" butonu
- **Tahmini süre:** Backend 2 saat + Angular 3 saat = 5 saat
- [x] Tamamlandı

---

### T4-7 · 404 Sayfası
- **Öncelik:** 🟠 Önemli
- **Uzman:** 🟢
- **Sorun:** Bilinmeyen URL'ye gidildiğinde Angular boş ekran gösteriyor. Kullanıcı kaybolduğunu anlayamıyor.
- **Dosya:** Yeni `Proxy.UI/src/app/pages/not-found/not-found.ts`, `app.routes.ts`
- **Yapılacak:**
  - `NotFoundComponent` oluştur — "404 — Page Not Found", dashboard'a dön butonu
  - `app.routes.ts` → wildcard route ekle: `{ path: '**', component: NotFoundComponent }`
  - Basit ama şık tasarım: büyük "404" yazısı, ikon, açıklama, "Go to Dashboard" butonu
- **Tahmini süre:** 30 dakika
- [x] Tamamlandı

---

### T4-8 · `GetHistory()` — İki Sorguyu Tek DTO'ya İndir
- **Öncelik:** 🟠 Önemli
- **Uzman:** 🔴 🟡
- **Sorun:** `GetHistory()` önce `col.Query()...ToList()` sonra ayrı `col.Count()` çağrısı yapıyor. İki sorgu arasında write gelirse count tutarsız olabilir. Ayrıca gereksiz iki DB round-trip.
- **Dosya:** `Proxy.Host/Controllers/ProxyConfigController.cs`
- **Mevcut Kod:**
  ```csharp
  var items = col.Query().OrderByDescending(x => x.ChangedAt).Offset(offset).Limit(limit).ToList();
  var total = col.Count();
  return Ok(new { data = items, total });
  ```
- **Yapılacak:**
  - LiteDB transaction içinde count + data al:
    ```csharp
    var db = _db.Database;
    int total;
    List<ConfigHistory> items;
    db.BeginTrans();
    try
    {
        var col = db.GetCollection<ConfigHistory>("config_history");
        total = col.Count();
        items = col.Query()
            .OrderByDescending(x => x.ChangedAt)
            .Offset(offset)
            .Limit(limit)
            .ToList();
        db.Commit();
    }
    catch { db.Rollback(); throw; }
    return Ok(new { data = items, total });
    ```
  - `config_history` collection'ına `ChangedAt` index ekle (`LiteDbService`'de)
- **Tahmini süre:** 1 saat
- [x] Tamamlandı

---

## 📋 SONRAKİ SPRINT

### ✅ T4-9 · `RecordHistory()` Request Path'inden Çıkar — Async Channel
- **Öncelik:** 🟠 Önemli
- **Uzman:** 🔴 🟡
- **Sorun:** `RecordHistory()` sync ve request path'inde. Her CRUD operasyonunda response dönmeden blocking DB write yapılıyor. Yüksek yükte latency artışına yol açar.
- **Dosya:** `Proxy.Host/Controllers/ProxyConfigController.cs`, `Proxy.Host/Services/`
- **Yapılacak:**
  - `HistoryWriterService` oluştur — `LogWriterService` pattern'ini taklit et
  - `Channel<HistoryEntry>` (unbounded veya bounded 1k)
  - `RecordHistory()` → channel'a `TryWrite()` yap, non-blocking
  - `HistoryWriterService.StopAsync()` → graceful drain (T4-2 ile aynı pattern)
  - `Program.cs` → `AddHostedService<HistoryWriterService>()` ekle
- **Tahmini süre:** 2 saat
- [x] Tamamlandı

---

### ✅ T4-10 · `LoadFromDb()` Cache + `RoutePatternFactory.Parse()` Cache
- **Öncelik:** 🟠 Önemli
- **Uzman:** 🔴 🟡
- **Sorun:**
  - `LiteDbProxyConfigProvider.LoadFromDb()` her `UpdateConfig()` çağrısında full `FindAll().ToList()` yapıyor
  - `RoutePatternFactory.Parse(path)` her load'da aynı path'i tekrar parse ediyor
  - 500+ route senaryosunda hem DB hem CPU yükü
- **Dosya:** `Proxy.Host/Providers/LiteDbProxyConfigProvider.cs`
- **Yapılacak:**
  - `_pathPatternCache = new ConcurrentDictionary<string, bool>()` — parse başarılı mı cache'le
  - `LoadFromDb()` içinde: `if (_pathPatternCache.ContainsKey(path)) skip parse`
  - Route silindiğinde cache'den temizle
  - Daha kapsamlı: `_routeCache` ve `_clusterCache` tutarak sadece değişen entity'leri reload et (dirty flag pattern)
  - Bunun için `UpdateConfig(string changedRouteId)` overload'u gerekebilir
- **Tahmini süre:** 3-4 saat
- [x] Tamamlandı

---

### T4-11 · Repository Pattern — `IRouteRepository`, `IClusterRepository`
- **Öncelik:** 🟠 Önemli
- **Uzman:** 🔴 🟡
- **Sorun:** `ProxyConfigController` doğrudan `_db.Database.GetCollection()` çağırıyor. Mock geçilemiyor, unit test yazılamıyor. Test coverage sıfır.
- **Dosyalar:** Yeni `Proxy.Host/Repositories/` klasörü
- **Yapılacak:**
  - `IRouteRepository` interface:
    ```csharp
    public interface IRouteRepository
    {
        IEnumerable<RouteDto> GetAll();
        RouteDto? FindById(string routeId);
        void Insert(RouteDto route);
        void Upsert(RouteDto route);
        bool Delete(string routeId);
    }
    ```
  - `IClusterRepository` — aynı pattern
  - `LiteDbRouteRepository : IRouteRepository` — LiteDB implementasyonu
  - `ProxyConfigController` → DI ile `IRouteRepository` ve `IClusterRepository` al
  - `Program.cs` → `AddScoped<IRouteRepository, LiteDbRouteRepository>()`
  - xUnit + Moq ile temel CRUD test'leri
- **Tahmini süre:** 1 gün (implementasyon) + 1 gün (testler)
- [x] Tamamlandı

---

### ✅ T4-12 · `MatSort` Kolon Sıralama — Route ve Log Tabloları
- **Öncelik:** 🟠 Önemli
- **Uzman:** 🟢
- **Sorun:** Dashboard route/cluster tabloları ve log tablosunda kolon başlıklarına tıklanarak sıralama yapılamıyor. Temel UX beklentisi karşılanmıyor.
- **Dosyalar:** `dashboard.html`, `dashboard.ts`, `logs.html`, `logs.ts`
- **Yapılacak:**
  - `MatSortModule` import et
  - `<table mat-table [dataSource]="..." matSort>` ekle
  - Her kolon header'ına `mat-sort-header` attribute ekle
  - Route tablosu: RouteId, ClusterId, Match Path kolonları sıralanabilir olsun
  - Log tablosu: Timestamp, StatusCode, Duration kolonları sıralanabilir olsun
  - Dashboard için client-side sort (signals ile): `sortedRoutes = computed(() => [...routes()].sort(...))`
  - Log tablosu için server-side sort parametresi ekle: `?sortBy=timestamp&sortDir=desc`
- **Tahmini süre:** 3 saat
- [x] Tamamlandı

---

## 🗓️ BACKLOG

### T4-13 · Dark Mode
- **Öncelik:** 🟡 Backlog
- **Uzman:** 🟢
- **Sorun:** İşletim sistemi dark mode'dayken uygulama yanıyor. 2026'da temel beklenti.
- **Yapılacak:**
  - Angular Material theming: `prefers-color-scheme` media query
  - `app.component.ts` → `@HostBinding('class')` ile tema class'ı yönet
  - CSS custom properties ile renk overrides
  - Toggle butonu navbar'a ekle (sistem tercihini geçersiz kılmak için)
  - Tüm custom CSS'lerde hardcoded renkleri (`#666`, `#1976d2` vb.) CSS variable'a taşı
- **Tahmini süre:** 1-2 gün
- [ ] Tamamlandı

---

### T4-14 · Mobile Responsive Breakpoint'ler
- **Öncelik:** 🟡 Backlog
- **Uzman:** 🟢
- **Sorun:** Tablolar küçük ekranda taşıyor. Sidebar collapse etmiyor. Form dialog'ları mobile'da kullanılamıyor.
- **Yapılacak:**
  - `BreakpointObserver` ile Handset/Tablet detection
  - Sidebar → `mat-sidenav` ile overlay mode
  - Tablolar → küçük ekranda card layout'a geç (`@if (isHandset())`)
  - Dialog width'leri → `calc(100vw - 32px)` clamp'i ekle
  - Font size ve spacing'i mobile için ayarla
- **Tahmini süre:** 2-3 gün
- [ ] Tamamlandı

---

### T4-15 · Raw Editor → Monaco Editor
- **Öncelik:** 🟡 Backlog
- **Uzman:** 🟢 🔴
- **Sorun:** `textarea` ile JSON edit: syntax highlight yok, hata göstergesi yok, format-on-save yok, otomatik tamamlama yok.
- **Yapılacak:**
  - `npm install ngx-monaco-editor-v2` (veya `@monaco-editor/angular`)
  - `app.config.ts` → Monaco loader konfigüre et
  - `raw-editor.html` → `<ngx-monaco-editor>` ile `<textarea>` değiştir
  - JSON schema validation: route/cluster yapısına göre schema tanımla
  - Format on save: `editor.getAction('editor.action.formatDocument').run()`
  - Theme: system dark/light ile senkronize et
- **Tahmini süre:** 1-2 gün
- [ ] Tamamlandı

---

### T4-16 · `LogService` / `LiteDbService` Dispose Zinciri
- **Öncelik:** 🟡 Backlog
- **Uzman:** 🔴 🟡
- **Sorun:** `LogService` `IDisposable` implement ediyor ama `AddSingleton` kayıtlı DI container onu dispose etmiyor. Uygulama kapanırken LiteDB WAL dosyaları temiz kapatılmayabilir.
- **Dosyalar:** `Program.cs`, `LogService.cs`, `LiteDbService.cs`
- **Yapılacak:**
  - `IHostApplicationLifetime.ApplicationStopping` hook'una bağlan:
    ```csharp
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        app.Services.GetRequiredService<LogService>().Dispose();
        app.Services.GetRequiredService<LiteDbService>().Dispose();
    });
    ```
  - Veya: `IHostedService.StopAsync()` içinde dispose et (T4-3 ile entegre)
  - `LiteDbService` → `IDisposable` implement et, `_db.Dispose()` çağır
- **Tahmini süre:** 1 saat
- [ ] Tamamlandı

---

### T4-17 · `GetLogs()` + `GetTotalCount()` Birleştir
- **Öncelik:** 🟡 Backlog
- **Uzman:** 🔴 🟡
- **Sorun:** `LogService.GetLogs()` ve `GetTotalCount()` ayrı çağrılıyor, iki DB round-trip. Her çağrıda `GetCollection<LogEntry>()` yeniden alınıyor.
- **Dosya:** `Proxy.Host/Services/LogService.cs`, ilgili Controller
- **Yapılacak:**
  ```csharp
  public (IEnumerable<LogEntry> Data, long Total) GetLogsWithTotal(
      int limit, int offset, /* filtre parametreleri */)
  {
      var col = _db.GetCollection<LogEntry>(CollectionName); // bir kere al
      // filtre uygula
      var query = BuildQuery(col, /* filtreler */);
      var total = query.Count(); // LiteDB query üzerinde count
      var data = query.OrderByDescending(x => x.Timestamp).Offset(offset).Limit(limit).ToEnumerable();
      return (data, total);
  }
  ```
  - Collection referansını field olarak cache'le: `private ILiteCollection<LogEntry>? _collection;`
  - `_collection ??= _db.GetCollection<LogEntry>(CollectionName);`
- **Tahmini süre:** 1 saat
- [ ] Tamamlandı

---

---

## 🎨 UI/UX SPRINT — Sofia R. Bulguları (5. Ajan)

> Kaynak: UI/UX Designer gözden geçirmesi — mevcut kod analizi (dashboard.html, logs.html, history.html, app.html, styles.css, material-theme.scss)
> Öncelik: 🔴 Hemen Yap → 🟡 Planla → 🟢 Backlog

---

### T5-1 · Dashboard Layout: max-width Kaldır, Full-Width Responsive Tablo
- **Öncelik:** 🔴 Kritik
- **Uzman:** 🎨 🟢
- **Sorun:** `styles.css` satır 14'te `.card-container { max-width: 600px; margin: 2rem auto; }` tanımlı. `dashboard.html` bu class'ı kullanıyor. Geniş monitörlerde (1920px) tablo yalnızca 600px genişliğinde duruyor — admin panel için kabul edilemez. Ops mühendisleri geniş ekranda çalışır; tablodaki alanlar kısıtlı görünüyor.
- **Dosyalar:** `Proxy.UI/src/styles.css`, `Proxy.UI/src/app/pages/dashboard/dashboard.html`, `Proxy.UI/src/app/pages/dashboard/dashboard.css`
- **Yapılacak:**
  - `styles.css` → `.card-container` max-width kaldır veya dashboard'a özgü class yaz
  - `dashboard.html` → `<div class="card-container">` → `<div class="dashboard-container">` olarak değiştir
  - `dashboard.css` → `.dashboard-container { padding: 24px; max-width: 100%; }` ekle
  - Tablo genişliğini `100%` yap (zaten var ama outer container kısıtlıyor)
  - Mat-card'lara `padding: 0 24px 24px` spacing ver
- **Kabul Kriteri:** 1440px monitörde tablo ekranın tamamını kullanmalı
- **Tahmini süre:** 30 dakika
- [x] Tamamlandı

---

### T5-2 · Aktif Nav Item CSS: `.active` Class'ı Sidenav'a Ekle
- **Öncelik:** 🔴 Kritik
- **Uzman:** 🎨 🟢
- **Sorun:** `app.html`'de `routerLinkActive="active"` kullanılmış ama `.active` class'ı ne `app.css`'te ne de `styles.css`'te tanımlı. Sonuç: aktif sayfa görsel olarak hiç vurgulanmıyor. Kullanıcı "neredeyim?" sorusuna cevap bulamıyor — temel navigasyon ilkesi ihlali.
- **Dosyalar:** `Proxy.UI/src/app/app.css` (veya `styles.css`)
- **Yapılacak:**
  - `app.css` → aktif nav item için stil ekle:
    ```css
    mat-nav-list a.active {
      background-color: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      border-radius: 8px;
      font-weight: 600;
    }
    mat-nav-list a.active mat-icon {
      color: var(--mat-sys-primary);
    }
    ```
  - Material 3 CSS variable'ları kullan — theme değişince otomatik uyum sağlar
  - `mat-list-item` içinde `routerLinkActive` için `mat-list-item.active` selector da dene (Material quirk)
- **Kabul Kriteri:** Aktif sayfada sidenav item'ı görsel olarak vurgulanmalı
- **Tahmini süre:** 15 dakika
- [x] Tamamlandı

---

### T5-3 · Yanlış İkon Düzelt + Sidenav Görsel İyileştirme
- **Öncelik:** 🟡 Orta
- **Uzman:** 🎨 🟢
- **Sorun:** `app.html` satır 9: Logs sayfası için `history` ikonu kullanılmış. Aynı `history` ikonu kavramsal olarak "Change History" sayfasını çağrıştırıyor — zaten ayrı bir sayfası var. Bilişsel karışıklık yaratıyor. Ayrıca sidenav'da kullanıcı profili/username bilgisi gösterilmiyor.
- **Dosyalar:** `Proxy.UI/src/app/app.html`, `Proxy.UI/src/app/app.ts`
- **Yapılacak:**
  - Logs ikonu: `history` → `receipt_long` (trafik log çağrışımı)
  - Change History ikonu: `manage_history` (zaten doğru, kalsın)
  - Dashboard ikonu: `dashboard` (doğru, kalsın)
  - Raw Config ikonu: `code` → `tune` veya `settings_applications` (daha açıklayıcı)
  - Sidenav alt kısmına logged-in user bilgisi ekle:
    ```html
    <div class="sidenav-footer">
      <mat-icon>account_circle</mat-icon>
      <span>{{ authService.username() }}</span>
    </div>
    ```
  - `AuthService`'e `username` signal'i ekle (JWT'den parse et)
- **Kabul Kriteri:** Her nav item ikonu sayfanın amacını doğru yansıtmalı
- **Tahmini süre:** 30 dakika
- [x] Tamamlandı

---

### T5-4 · Inline Style'ları Temizle — Dashboard CSS Class'larına Taşı
- **Öncelik:** 🟡 Orta
- **Uzman:** 🎨 🟢
- **Sorun:** `dashboard.html` içinde en az 6 inline style var:
  - `style="display:flex; gap: 8px;"`
  - `style="margin-top: 2rem;"`
  - `style="width: 100%; margin-top: 15px;"`
  - `style="color: #9c27b0;"` (hardcoded Material purple — theme değişince kırılır)
  - Bunlar bakım sorununa, tutarsız tasarıma ve theme uyumsuzluğuna yol açıyor.
- **Dosyalar:** `Proxy.UI/src/app/pages/dashboard/dashboard.html`, `dashboard.css`
- **Yapılacak:**
  - Tüm inline style'ları `dashboard.css`'e CSS class olarak taşı
  - `color: #9c27b0` → `color: var(--mat-sys-tertiary)` veya Material `color="accent"` kullan
  - Cluster section margin: `.clusters-section { margin-top: 2rem; }` class'ı
  - Action button row: `.header-actions { display: flex; gap: 8px; }` class'ı
  - Raw edit button: mat-icon-button'a `color="accent"` ekle (hardcoded renk yerine)
- **Kabul Kriteri:** `dashboard.html`'de sıfır inline `style=""` attribute
- **Tahmini süre:** 1 saat
- [x] Tamamlandı

---

### T5-5 · Empty State Component: İkon + Mesaj + CTA
- **Öncelik:** 🟡 Orta
- **Uzman:** 🎨 🟢
- **Sorun:** Dashboard'da `*matNoDataRow` sadece `"No routes configured yet."` ve `"No clusters configured yet."` metni gösteriyor. İlk kez ürünü açan kullanıcı ne yapacağını bilmiyor — onboarding fırsatı kaçıyor. Boş state'ler UX'te "zero state" olarak bilinir ve çok önemlidir.
- **Dosyalar:** Yeni `Proxy.UI/src/app/shared/empty-state/empty-state.ts`, `dashboard.html`
- **Yapılacak:**
  - Reusable `EmptyStateComponent` oluştur:
    ```typescript
    @Component({
      selector: 'app-empty-state',
      standalone: true,
      template: `
        <div class="empty-state">
          <mat-icon class="empty-icon">{{ icon }}</mat-icon>
          <h3>{{ title }}</h3>
          <p>{{ message }}</p>
          <button mat-flat-button color="primary" (click)="ctaClick.emit()">
            <mat-icon>{{ ctaIcon }}</mat-icon> {{ ctaLabel }}
          </button>
        </div>
      `
    })
    export class EmptyStateComponent {
      @Input() icon = 'inbox';
      @Input() title = 'Nothing here yet';
      @Input() message = '';
      @Input() ctaLabel = '';
      @Input() ctaIcon = 'add';
      @Output() ctaClick = new EventEmitter<void>();
    }
    ```
  - Route empty state: ikon `alt_route`, başlık "No routes configured", CTA "Add your first route"
  - Cluster empty state: ikon `hub`, başlık "No clusters configured", CTA "Add your first cluster"
  - `*matNoDataRow` yerine `@if (routes().length === 0)` bloğunda kullan
  - CSS: `.empty-state { text-align: center; padding: 48px 24px; }`, büyük icon (64px), muted text rengi
- **Kabul Kriteri:** Boş tabloda kullanıcıya ne yapacağı net gösterilmeli, tek tıkla ekleme diyaloğu açılmalı
- **Tahmini süre:** 2 saat
- [x] Tamamlandı

---

### T5-6 · Method Badge: PATCH, HEAD, OPTIONS Renkleri Ekle
- **Öncelik:** 🟢 Düşük
- **Uzman:** 🎨
- **Sorun:** `logs.css`'te GET (mavi), POST (yeşil), PUT (turuncu), DELETE (kırmızı) için badge renkleri tanımlı. Ancak PATCH, HEAD, OPTIONS için renk yok — bu method'larla gelen istekler renksiz (beyaz/şeffaf) badge gösteriyor. Görsel tutarsızlık.
- **Dosya:** `Proxy.UI/src/app/pages/logs/logs.css`
- **Yapılacak:**
  ```css
  .PATCH   { background-color: #50e3c2; color: #004d40; }  /* teal */
  .HEAD    { background-color: #9b59b6; color: white; }    /* mor */
  .OPTIONS { background-color: #95a5a6; color: white; }    /* gri */
  ```
  - Renk seçimi: Swagger UI renk paleti referans alındı (endüstri standardı)
  - `logs.html`'de `[ngClass]="log.method"` zaten var, sadece CSS eklemek yeterli
- **Kabul Kriteri:** Tüm HTTP method'ları renk kodlu badge göstermeli
- **Tahmini süre:** 15 dakika
- [x] Tamamlandı

---

### T5-7 · `aria-label` Eksikliklerini Gider — WCAG AA Uyumu
- **Öncelik:** 🟡 Orta
- **Uzman:** 🎨 🟢
- **Sorun:** Tüm `mat-icon-button` bileşenlerinde yalnızca `matTooltip` var, `aria-label` yok. Tooltip hover gerektirir — klavye navigasyonu yapan veya ekran okuyucu kullanan kullanıcılar için butonun amacı anlaşılamaz. WCAG 2.1 AA standardı (SC 4.1.2) ihlali. Kurumsal müşterilerde compliance riski.
- **Dosyalar:** `dashboard.html`, `logs.html`, `history.html`, `app.html`
- **Yapılacak:**
  - Tüm `mat-icon-button` bileşenlerine `aria-label` ekle:
    ```html
    <!-- Önce -->
    <button mat-icon-button (click)="editRoute(element)" matTooltip="Edit Route">
    <!-- Sonra -->
    <button mat-icon-button (click)="editRoute(element)" matTooltip="Edit Route" aria-label="Edit route {{ element.routeId }}">
    ```
  - Dashboard: edit, raw-edit, delete butonları için route/cluster ID'yi içeren aria-label
  - Logs: refresh, filter toggle, clear buttons
  - History: refresh, diff view button
  - App header: logout, sidenav toggle
  - `mat-paginator` için `aria-label` zaten var (logs.html'de mevcut)
- **Kabul Kriteri:** axe-core tarayıcı eklentisi ile 0 "missing accessible name" hatası
- **Tahmini süre:** 1 saat
- [x] Tamamlandı

---

### T5-8 · Loading Skeleton: History + Dashboard + Logs Sayfaları
- **Öncelik:** 🟡 Orta
- **Uzman:** 🎨 🟢
- **Sorun:** History sayfasında loading state: `<p style="padding:16px; color:#888">Loading...</p>`. Dashboard ve Logs'ta hiçbir loading indicator yok. 2026'da kullanıcı beklentisi skeleton loader — "içerik yükleniyor" sinyali verir, uygulamanın donmadığını hissettirir. Skeleton'lar aynı zamanda CLS (Cumulative Layout Shift) önler.
- **Dosyalar:** Yeni `Proxy.UI/src/app/shared/skeleton/`, `history.html`, `dashboard.html`, `logs.html`
- **Yapılacak:**
  - CSS-only skeleton bileşeni oluştur (external library gerekmez):
    ```css
    .skeleton {
      background: linear-gradient(90deg, #f0f0f0 25%, #e0e0e0 50%, #f0f0f0 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
      border-radius: 4px;
    }
    @keyframes shimmer {
      0% { background-position: 200% 0; }
      100% { background-position: -200% 0; }
    }
    ```
  - `SkeletonRowComponent`: tablo satırı şeklinde skeleton (N adet kolon, configurable)
  - History sayfası: loading=true iken 5 skeleton satır göster, `Loading...` text'ini kaldır
  - Dashboard: route/cluster tabloları ilk yüklemede 3 skeleton satır göster
  - Logs: `loadLogs()` süresince skeleton satırlar
  - `isLoading = signal(false)` → her sayfada signal-based loading state
- **Kabul Kriteri:** Her sayfada loading süresince anlamlı skeleton görünmeli, "Loading..." text olmamalı
- **Tahmini süre:** 3 saat
- [x] Tamamlandı

---

### T5-9 · Dashboard Summary Cards + Backend `/api/proxyconfig/summary` Endpoint
- **Öncelik:** 🟡 Orta
- **Uzman:** 🎨 🟢 🔴 🟡
- **Sorun:** Dashboard açıldığında kullanıcı "her şey yolunda mı?" sorusunun cevabını göremez — iki tabloyu taramak zorunda. Bir ops mühendisi sabah ilk açışta 3 saniyede durumu anlamalı. Summary cards (KPI kartları) bu ihtiyacı karşılar.
- **Backend Değişiklikler:**
  - `ProxyConfigController.cs` → yeni endpoint:
    ```csharp
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        var routes   = _db.Database.GetCollection<RouteConfigWrapper>("routes").Count();
        var clusters = _db.Database.GetCollection<ClusterConfigWrapper>("clusters").Count();
        var logCol   = /* LogService'den al veya inject et */;
        var since    = DateTime.UtcNow.AddHours(-24);
        var errCount = logCol.Query()
            .Where(x => x.Timestamp >= since && x.StatusCode >= 500)
            .Count();
        var totalReq = logCol.Query()
            .Where(x => x.Timestamp >= since)
            .Count();
        return Ok(new { routes, clusters, errorsLast24h = errCount, requestsLast24h = totalReq });
    }
    ```
  - `ProxyConfigService` (Angular) → `getSummary()` metodu ekle
- **Angular Değişiklikler:**
  - `dashboard.ts` → `summary = signal<SummaryData | null>(null)`, `ngOnInit`'te `loadSummary()` çağır
  - `dashboard.html` → tablolardan önce 4 summary card:
    ```
    ┌──────────┐  ┌──────────┐  ┌──────────────┐  ┌──────────────┐
    │ Routes   │  │ Clusters │  │ Requests/24h │  │ Errors/24h   │
    │    4     │  │    3     │  │    1,240     │  │  🔴 2        │
    └──────────┘  └──────────┘  └──────────────┘  └──────────────┘
    ```
  - Hata sayısı > 0 ise kart kırmızı vurgulu göster
  - Kartlara tıklanınca ilgili sayfaya navigate et (Errors → Logs + filtre uygula)
- **Kabul Kriteri:** Dashboard açılışında 4 KPI kartı görünmeli; hata varsa görsel uyarı
- **Tahmini süre:** Backend 2 saat + Angular 3 saat = 5 saat
- [x] Tamamlandı

---

### ~~T5-10~~ · Dark Mode — İPTAL EDİLDİ (kapsam dışı)
- **Öncelik:** 🟢 Backlog
- **Uzman:** 🎨 🟢
- **Sorun:** `material-theme.scss`'te `color-scheme: light` hardcoded. İşletim sistemi dark mode'dayken uygulama tamamen beyaz görünüyor — göz yorgunluğu, ops ekiplerinde gece çalışan mühendisler için ciddi sorun. Ayrıca tüm custom CSS'lerde hardcoded renkler var (`#666`, `#fafafa`, `#e0e0e0`) — bunlar dark mode'da görünmez olur.
- **Dosyalar:** `material-theme.scss`, `styles.css`, tüm component CSS'ler, `app.ts`
- **Yapılacak:**
  - `material-theme.scss` → dark class ekle:
    ```scss
    .dark-theme {
      color-scheme: dark;
      @include mat.theme((
        color: (
          primary: mat.$azure-palette,
          theme-type: dark,
        ),
      ));
    }
    ```
  - `app.ts` → `isDarkMode = signal(false)`, localStorage'dan oku
  - `app.html` → `<body [class.dark-theme]="isDarkMode()">` veya HostBinding
  - Toolbar'a toggle butonu: `mat-icon-button` ile `dark_mode` / `light_mode` ikonu
  - Tüm component CSS'lerde hardcoded renkleri `var(--mat-sys-*)` token'larıyla değiştir:
    - `#fafafa` → `var(--mat-sys-surface-variant)`
    - `#e0e0e0` → `var(--mat-sys-outline-variant)`
    - `#666` → `var(--mat-sys-on-surface-variant)`
  - localStorage key: `proxy-ui-theme` = `'dark'` | `'light'`
  - `prefers-color-scheme` media query ile sistem tercihini default olarak al
- **Kabul Kriteri:** Toggle çalışmalı, sayfa yenilemede tercih korunmalı, tüm sayfalar tutarlı dark görünmeli
- **Tahmini süre:** 5 saat
- [ ] Tamamlandı

---

## 📊 ÖZET TABLO

| ID | Başlık | Öncelik | Sprint | Süre |
|----|--------|---------|--------|------|
| T4-1 | MustChangePassword Enforcement | 🔴 Kritik | Bu release | 2 saat |
| T4-2 | LogWriterService Graceful Drain | 🔴 Kritik | Bu release | 30 dk |
| T4-3 | LiteDbService Constructor → StartAsync | 🔴 Kritik | Bu release | 30 dk |
| T4-4 | Raw Editor CanDeactivate Guard | 🔴 Kritik | Bu release | 1 saat |
| T4-5 | History Diff → diff2html | 🟠 Önemli | Bu release | 4 saat |
| T4-6 | Log Server-Side Filtre | 🟠 Önemli | Bu release | 5 saat |
| T4-7 | 404 Sayfası | 🟠 Önemli | Bu release | 30 dk |
| T4-8 | GetHistory() Tek Sorgu | 🟠 Önemli | Bu release | 1 saat |
| T4-9 | RecordHistory Async Channel | 🟠 Önemli | Sonraki sprint | 2 saat |
| T4-10 | LoadFromDb Cache | 🟠 Önemli | Sonraki sprint | 4 saat |
| T4-11 | Repository Pattern + Tests | 🟠 Önemli | Sonraki sprint | 2 gün |
| T4-12 | MatSort Kolon Sıralama | 🟠 Önemli | Sonraki sprint | 3 saat |
| T4-13 | Dark Mode | 🟡 Backlog | Major release | 2 gün |
| T4-14 | Mobile Responsive | 🟡 Backlog | Major release | 3 gün |
| T4-15 | Monaco Editor | 🟡 Backlog | Major release | 2 gün |
| T4-16 | Dispose Zinciri | 🟡 Backlog | Major release | 1 saat |
| T4-17 | GetLogs Birleştir | 🟡 Backlog | Major release | 1 saat |
| **T5-1** | **Dashboard max-width Fix** | 🔴 Kritik | UI Sprint | 30 dk |
| **T5-2** | **Aktif Nav CSS** | 🔴 Kritik | UI Sprint | 15 dk |
| **T5-3** | **Yanlış İkon + Sidenav İyileştirme** | 🟡 Orta | UI Sprint | 30 dk |
| **T5-4** | **Inline Style Temizliği** | 🟡 Orta | UI Sprint | 1 saat |
| **T5-5** | **Empty State Component** | 🟡 Orta | UI Sprint | 2 saat |
| **T5-6** | **Method Badge PATCH/HEAD/OPTIONS** | 🟢 Düşük | UI Sprint | 15 dk |
| **T5-7** | **aria-label WCAG AA** | 🟡 Orta | UI Sprint | 1 saat |
| **T5-8** | **Skeleton Loader** | 🟡 Orta | UI Sprint | 3 saat |
| **T5-9** | **Dashboard Summary Cards** | 🟡 Orta | UI Sprint | 5 saat |
| **T5-10** | **Dark Mode Toggle** | 🟢 Backlog | Major release | 5 saat |

**Bu Release Toplam:** ~14 saat
**Sonraki Sprint Toplam:** ~3 gün
**UI Sprint Toplam:** ~13 saat 50 dk (T5-1 → T5-9)
**Backlog Toplam:** ~9 gün + T5-10 (5 saat)

---

*Her madde tamamlandığında `[ ]` → `[x]` işaretle.*
