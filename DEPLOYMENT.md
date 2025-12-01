# Panduan Deployment - BPJS Biometric Automation Agent

## 📦 Paket Rilis

**Versi:** 1.0.1  
**Tanggal Rilis:** 29 November 2025  
**Platform:** Windows x64 (Windows 10/11, Windows Server 2016+)  
**Runtime:** Self-contained (sudah termasuk .NET 8.0, tidak perlu install runtime tambahan)

---

## 📁 Isi Folder Publish

Folder `publish/` berisi file yang diperlukan:

```
publish/
├── BiometricAgent.exe        # Aplikasi utama (single-file, ~82 MB)
├── asset/
│   └── heartbeat.ico         # Icon untuk system tray
├── config/
│   ├── config.json           # File konfigurasi (WAJIB diedit)
│   └── config.example.json   # Contoh konfigurasi
└── logs/                     # Folder log (dibuat otomatis)
```

**Catatan:** Semua dependensi sudah dikemas dalam `BiometricAgent.exe`, tidak ada file DLL terpisah.

---

## 🚀 Langkah Deployment di KIOSK

### Langkah 1: Copy File ke KIOSK

Copy seluruh folder `publish/` ke komputer KIOSK:

```powershell
# Contoh lokasi instalasi
C:\BiometricAgent\
```

Struktur folder di KIOSK setelah copy:
```
C:\BiometricAgent\
├── BiometricAgent.exe
├── asset\
│   └── heartbeat.ico
├── config\
│   └── config.json
└── logs\
```

---

### Langkah 2: Edit Konfigurasi

1. **Buka file `config/config.json`** dengan Notepad atau editor lain

2. **Sesuaikan pengaturan berikut:**

```json
{
  "HttpServer": {
    "Host": "127.0.0.1",
    "Port": 5000
  },
  "Applications": {
    "Frista": {
      "ExecutablePath": "D:\\Frista\\Frista.exe"
    },
    "Finger": {
      "ExecutablePath": "C:\\Program Files (x86)\\BPJS Kesehatan\\Aplikasi Sidik Jari BPJS Kesehatan\\After.exe"
    }
  },
  "Credentials": {
    "FristaUsername": "username_frista_anda",
    "FristaPassword": "PASSWORD_TERENKRIPSI",
    "FingerUsername": "username_finger_anda",
    "FingerPassword": "PASSWORD_TERENKRIPSI"
  }
}
```

**Yang perlu diubah:**
- `ExecutablePath` — Sesuaikan dengan lokasi Frista.exe dan After.exe di KIOSK
- `FristaUsername`, `FingerUsername` — Username login
- `FristaPassword`, `FingerPassword` — Password terenkripsi (lihat langkah 3)

---

### Langkah 3: Enkripsi Password (WAJIB)

Password harus dienkripsi menggunakan Windows DPAPI. Jalankan perintah ini:

```powershell
cd C:\BiometricAgent
.\BiometricAgent.exe --encrypt-password
```

**Contoh output:**
```
=== Biometric Agent - Password Encryption Utility ===

Enter password to encrypt: ********
Encrypted password (copy this to config.json):
AQAAANCMnd8BFdERjHoAwE/Cl+sB...

Example config.json usage:
  "FristaPassword": "AQAAANCMnd8BFdERjHoAwE/Cl+sB..."
```

**Copy hasil enkripsi ke `config.json`.**

⚠️ **PENTING:** Jalankan enkripsi dengan user yang sama yang akan menjalankan service!

---

### Langkah 4: Test Manual (Opsional)

Sebelum install sebagai service, test dulu secara manual:

```powershell
# Jalankan aplikasi
cd C:\BiometricAgent
.\BiometricAgent.exe

# Di terminal lain, test endpoint health
Invoke-RestMethod http://127.0.0.1:5000/health

# Test automation Frista (gunakan nomor BPJS valid)
Invoke-RestMethod "http://127.0.0.1:5000/run_exe?bpjs=0001234567890"

# Tekan Ctrl+C untuk stop
```

---

### Langkah 5: Install sebagai Windows Service

Buka **PowerShell sebagai Administrator**, lalu jalankan:

```powershell
# Install service
sc.exe create BiometricAgent binPath= "C:\BiometricAgent\BiometricAgent.exe" start= auto DisplayName= "BPJS Biometric Agent"

# Tambah deskripsi
sc.exe description BiometricAgent "Automasi aplikasi BPJS Frista dan Sidik Jari"

# Atur auto-restart jika crash
sc.exe failure BiometricAgent reset= 86400 actions= restart/60000

# Start service
sc.exe start BiometricAgent
```

**Verifikasi service berjalan:**
```powershell
Get-Service BiometricAgent
```

Output yang diharapkan:
```
Status   Name             DisplayName
------   ----             -----------
Running  BiometricAgent   BPJS Biometric Agent
```

---

### Langkah 6: Verifikasi

Test apakah service sudah berjalan dengan benar:

```powershell
# Cek health endpoint
Invoke-RestMethod http://127.0.0.1:5000/health
```

Output yang diharapkan:
```json
{
  "status": "healthy",
  "version": "1.0.0.0",
  "timestamp": "2025-11-28T10:30:00Z"
}
```

---

## 🔧 Manajemen Service

### Perintah Dasar

```powershell
# Stop service
sc.exe stop BiometricAgent

# Start service
sc.exe start BiometricAgent

# Restart service
sc.exe stop BiometricAgent; sc.exe start BiometricAgent

# Hapus service (jika ingin uninstall)
sc.exe delete BiometricAgent
```

### Cek Status

```powershell
Get-Service BiometricAgent
```

### Cek Log

Log tersimpan di folder `logs/` (dibuat otomatis):

```powershell
Get-Content "C:\BiometricAgent\logs\biometric-agent*.log" -Tail 50
```

---

## 📊 API Endpoints

| Endpoint | Fungsi |
|----------|--------|
| `GET /health` | Cek status aplikasi |
| `GET /run_exe?bpjs={nomor}` | Jalankan automasi Frista |
| `GET /run_finger_exe?bpjs={nomor}` | Jalankan automasi Sidik Jari |
| `GET /stop_exe` | Stop proses Frista |
| `GET /stop_finger_exe` | Stop proses Sidik Jari |

**Contoh penggunaan:**
```
http://127.0.0.1:5000/run_exe?bpjs=0001234567890
http://127.0.0.1:5000/run_finger_exe?bpjs=0001234567890
```

---

## 🛠️ Troubleshooting

### Service Tidak Mau Start

```powershell
# Cek status detail
sc.exe query BiometricAgent

# Test jalankan manual untuk lihat error
cd C:\BiometricAgent
.\BiometricAgent.exe
```

**Kemungkinan masalah:**
- Path aplikasi di `config.json` salah
- Password tidak terenkripsi dengan benar
- File `config.json` tidak ditemukan

### Config.json Tidak Ditemukan

Pastikan struktur folder seperti ini:
```
C:\BiometricAgent\
├── BiometricAgent.exe
└── config\
    └── config.json     <-- WAJIB ADA
```

### Password Tidak Valid

Enkripsi ulang password:
```powershell
.\BiometricAgent.exe --encrypt-password
```

⚠️ **PENTING:** Harus dijalankan dengan user yang sama yang menjalankan service!

### Aplikasi Frista/Finger Tidak Terbuka

1. Cek path di `config.json` sudah benar
2. Pastikan aplikasi bisa dibuka secara manual
3. Tingkatkan `StartupTimeoutSeconds` di config jika aplikasi lambat

---

## ✅ Checklist Deployment

- [ ] File `BiometricAgent.exe` sudah dicopy ke KIOSK
- [ ] Folder `asset/` sudah dicopy (untuk tray icon)
- [ ] Folder `config/` sudah dicopy dengan `config.json`
- [ ] Path aplikasi di `config.json` sudah disesuaikan
- [ ] Username sudah diisi di `config.json`
- [ ] Password sudah dienkripsi dan diisi di `config.json`
- [ ] Service sudah diinstall dengan `sc.exe create`
- [ ] Service sudah distart dengan `sc.exe start`
- [ ] Health endpoint merespon dengan benar
- [ ] Test automasi dengan nomor BPJS valid

---

## 🖥️ System Tray Icon (Fitur Baru)

Saat aplikasi dijalankan **secara manual** (bukan sebagai Windows Service), akan muncul icon di system tray untuk monitoring:

### Fitur Tray Icon:
- **Icon Hijau** 🟢 = Service sehat dan berjalan normal
- **Icon Merah** 🔴 = Service bermasalah atau tidak merespon
- **Auto health check** setiap 30 detik

### Menu Konteks (Klik Kanan):
- **Check Health** — Cek status dan tampilkan notifikasi
- **Open Logs Folder** — Buka folder logs di Explorer
- **Exit** — Tutup aplikasi

### Catatan:
- Tray icon **hanya muncul saat dijalankan manual**
- Saat berjalan sebagai Windows Service, tray icon tidak akan muncul (normal)
- Icon menggunakan file `asset/heartbeat.ico`

---

## 📞 Bantuan

Jika ada masalah, cek:
1. Log aplikasi: `C:\BiometricAgent\logs\`
2. Windows Event Viewer: Application log
3. Jalankan manual untuk lihat error: `.\BiometricAgent.exe`

---

**Dibuat dengan .NET 8.0 | Single-file deployment | Siap produksi** 🚀
