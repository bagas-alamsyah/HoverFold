# HoverFold

Aplikasi **Desktop Hover-Folder Manager** yang ringan dan aesthetic untuk Windows. Mengelompokkan shortcut aplikasi dan game di Desktop menjadi kotak folder **1x1** yang langsung mekar di tempat (*in-place hover popover*) saat didekati kursor—lengkap dengan kustomisasi tema (transparan, dark/black, light/white, dan warna custom).

## Fitur Utama
- **1x1 Desktop Folder Squircle**: Ukuran kompak layaknya ikon desktop dengan *preview* 2x2 ikon aplikasi mini tanpa tanda panah shortcut Windows (`↗`).
- **In-Place Hover Popover**: Otomatis mekar secara mulus saat kursor diarahkan ke folder dan menutup kembali saat kursor menjauh.
- **Ultra-Lightweight Native C# / WPF**: Mengonsumsi RAM sangat kecil (~20 MB) dan di-compile langsung menggunakan compiler bawaan Windows (`csc.exe`) tanpa ketergantungan runtime tambahan.
- **Hybrid Auto-Scan & Custom Drag-and-Drop**: Otomatis membaca shortcut `.lnk`, `.exe`, dan `.url` (Steam/Epic Games) dari Desktop, serta mendukung pembuatan folder baru dan *drag-and-drop* antar-folder.
- **Win + D & Wallpaper Engine Compatible**: Tetap tampil di atas permukaan Desktop (termasuk saat menekan `Win + D` atau memakai Wallpaper Engine) dan otomatis berada di belakang jendela aplikasi aktif.
- **Live Settings Panel (`⚙`)**: Kustomisasi tema (*Obsidian Dark*, *Frosted Glass*, *Campfire Warm*, *Midnight Slate*), transparansi background, jumlah kolom grid (3 atau 4 kolom), ukuran kotak 1x1, serta tombol cepat untuk menyembunyikan ikon desktop bawaan Windows.

## Cara Build & Menjalankan
1. Jalankan **[Run-AuraFolders.bat](./Run-AuraFolders.bat)** (otomatis meng-compile jika `AuraFolders.exe` belum ada dan menjalankannya), atau
2. Compile manual lewat PowerShell:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\build.ps1
   .\AuraFolders.exe
   ```
3. Untuk mematikan aplikasi saat development:
   - Klik kanan pada folder → **Tutup / Matikan AuraFolders**, atau
   - Buka **Settings (`⚙`)** → **Matikan AuraFolders**, atau
   - Jalankan **[Stop-AuraFolders.bat](./Stop-AuraFolders.bat)**.
