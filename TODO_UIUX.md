# TODO_UIUX.md — Frontend UI/UX İyileştirmeleri

> UI/UX Expert: Burak  
> Tarih: 2026-04-20

---

## 🔴 KRITIK — Hemen Yapılacak

### [UI] HistoryDiffDialog Çok Küçük ✅
- **Dosya**: `Proxy.UI/src/app/pages/history/history-diff-dialog.ts`
- **Çözüm**: `min-width: 1100px`, `max-width: 1200px`, `max-height: 85vh`

### [UX] Login Loading State Yok ✅
- **Dosya**: `Proxy.UI/src/app/pages/login/login.ts`, `login.html`
- **Çözüm**: `isLoading` signal + matSpinner eklendi

### [UX] ChangePassword Loading State Yok ✅
- **Dosya**: `Proxy.UI/src/app/pages/change-password/change-password.ts`, `change-password.html`
- **Çözüm**: `isLoading` signal + matSpinner eklendi

---

## 🟡 ORTA — Bu Sprintte

### [UI] Dashboard Table Responsive
- **Dosya**: `Proxy.UI/src/app/pages/dashboard/dashboard.html`
- **Sorun**: `mat-table` küçük ekranda taşıyor
- **Öneri**: Overflow container ekle, cards grid için flex breakpoints

### [UI] Logs Table 7 Kolon Taşıyor
- **Dosya**: `Proxy.UI/src/app/pages/logs/logs.html`
- **Sorun**: Path/destination kolonları uzun, taşıyor
- **Öneri**: Table overflow wrapper ekle

### [UI] Logs Status Renk Körlüğü
- **Dosya**: `logs.html`
- **Sorun**: 2xx=green, 4xx=orange, 5xx=red renk körlüğü dostu değil
- **Öneri**: Icon ekle veya tooltip ile status göster

### [UX] Filter Panel Mobilde Taşıyor
- **Dosya**: `logs.html`
- **Sorun**: 5 filter field tek satırda
- **Öneri**: Flex wrap et

### [UI] History Table Pagination Küçük
- **Dosya**: `history.html`
- **Sorun**: Pagination ikon butonları çok küçük
- **Öneri**: Label ekle, buton boyutu artır

---

## 🟡 ORTA — Sonraki Sprint

### [UX] Confirm Dialog Dar
- **Dosya**: `confirm-dialog.ts`
- **Sorun**: `width: 380px` mesajlarda yetersiz
- **Öneri**: `min-width: 450px`, warning icon ekle

### [UX] RawEditDialog Dar
- **Dosya**: `dialogs/raw-edit-dialog.ts`
- **Sorun**: `min-width: 520px` JSON için yetersiz
- **Öneri**: `min-width: 700px`

### [UI] Route/Cluster Dialog Array Field Taşıyor
- **Dosya**: `dialogs/route-dialog.html`, `cluster-dialog.html`
- **Sorun**: headers, queryParameters küçük ekranda taşıyor
- **Öneri**: Flex wrap ekle

### [UX] Login Input Validation Görünmüyor
- **Dosya**: `login.html`
- **Sorun**: `mat-error` kullanılmamış
- **Öneri**: Error mesajları ekle

### [UX] Dashboard Table Hover Yok
- **Sorun**: Hangi satırda olduğu belli değil
- **Öneri**: Row hover effect ekle

### [UX] Password Strength Indicator Yok
- **Dosya**: `change-password.html`
- **Sorun**: 8 karakter tek başına yeterli değil
- **Öneri**: Real-time strength göster

---

## 📋 Öncelik Sıralaması

| # | Öncelik | Issue |
|---|--------|-------|
| 1 | 🔴 Kritik | Diff dialog boyutu |
| 2 | 🔴 Kritik | Login loading |
| 3 | 🔴 Kritik | ChangePassword loading |
| 4 | 🟡 Orta | Dashboard table responsive |
| 5 | 🟡 Orta | Logs filter wrap |
| 6 | 🟡 Orta | Logs status icon |
| 7 | 🟡 Orta | History pagination |
| 8 | 🟡 Orta | Confirm dialog genişlik |

---

## Notlar

- Material Design token'ları (`$spacing`, `$typography`) kullanılmalı
- `mat-table` responsive değil - wrapper div ile overflow kontrolü şart
- Tüm dialog'lar `min-width: auto` yerine sabit değer alsın
- Auto-refresh interval'ler için loading spinner şart