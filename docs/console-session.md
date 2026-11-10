# Console session / custom user shell

GameHub 0.2 menyediakan dua cara masuk ke antarmuka konsol setelah login.

## 1. Startup biasa

Menu Start → Startup / shell information → Enable console at login. Explorer tetap menjadi shell Windows. Cara ini juga tersedia untuk Windows Home. Ini bukan penggantian shell.

## 2. Custom user shell — eksperimental

Pada Windows Pro, Enterprise, Education, dan IoT Enterprise yang mendukung kebijakan **Custom User Interface**, Windows dapat memulai `GameHub.Session.exe` sebagai antarmuka akun **menggantikan Explorer**. Windows tetap menjalankan kernel, driver, layanan, layar login, dan runtime game biasa.

Fitur ini belum diuji pada sesi login Windows nyata dalam lingkungan pengembangan Linux. Uji paket portable dan prosedur pemulihan pada akun Windows terpisah sebelum mengaktifkannya pada akun utama. Build CI hanya memvalidasi kompilasi dan paket, bukan perilaku Winlogon.

### Aktifkan memakai controller

1. Unduh dan ekstrak **seluruh** artifact terbaru.
2. Jalankan GameHub.exe sekali. Pada mode Console, Start membuka menu.
3. Startup / shell information → Enable GameHub as the Windows user shell.
4. Baca penjelasan lalu pilih **Confirm: use GameHub on next login**.
5. Pada login berikutnya, GameHub membuka sesi Console tanpa desktop Explorer.

Tidak ada auto-login atau pengubahan bootloader. File paket disalin ke `%LOCALAPPDATA%\GameHub\Session`, sehingga pengaturan shell tidak menunjuk folder Downloads yang mudah dipindahkan. `package.manifest.json` diperlukan untuk menyalin hanya file paket dan memvalidasi batas folder.

Policy yang digunakan hanya untuk akun pengguna:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System\Shell`

Nilainya menunjuk session guardian. GameHub menolak menimpa custom shell milik aplikasi/admin lain. Perubahan tidak diterapkan otomatis saat menjalankan aplikasi, scan game, atau mendownload cover. Menu meminta konfirmasi eksplisit.

### Kembali ke Windows

- Start → **Exit to Windows desktop** memulai Explorer dan mengakhiri sesi GameHub. Shell tetap GameHub pada login berikutnya.
- Start → Session → **Disable GameHub custom shell and open Windows** menghapus policy jika nilai itu cocok dengan marker milik GameHub, lalu memulai Explorer. Login selanjutnya kembali ke shell bawaan.
- Guardian mencoba menjalankan UI maksimum tiga kali setelah kegagalan proses; setelah itu policy milik GameHub dihapus dan Explorer dimulai. Ini tidak mendeteksi UI yang hang atau dialog crash OS yang masih menahan proses.

### Pemulihan di luar aplikasi

Tekan **Ctrl+Alt+Delete → Task Manager → Run new task**, kemudian:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%LOCALAPPDATA%\GameHub\Session\scripts\Restore-WindowsDesktop.ps1"
```

Gunakan perintah di dialog Run new task (yang mengembangkan `%LOCALAPPDATA%`). Jika menjalankan dari PowerShell, gunakan `$env:LOCALAPPDATA`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\GameHub\Session\scripts\Restore-WindowsDesktop.ps1"
```

Task Manager / recovery adalah fasilitas Windows dan dapat membutuhkan keyboard. Penggunaan GameHub sehari-hari—pilih game, main, favorit, cari, tambah file, pilih artwork, pengaturan dan keluar—memiliki jalur controller.

### Batas edisi dan sesi

- Windows Home tidak tercantum dalam dukungan Microsoft untuk CustomShell; aplikasi menolak aktivasi shell dan menawarkan startup biasa.
- Policy organisasi/ACL dapat menolak perubahan HKCU. Aplikasi menampilkan error tanpa mengganti policy lain.
- Startup / shell pertama kali dikonfigurasi setelah pengguna masuk Windows. GameHub tidak melewati PIN, password, UAC, anti-cheat atau login launcher pihak ketiga.
- Update instalasi shell yang sedang berjalan belum didukung. Pulihkan Explorer, keluar dari GameHub, lalu jalankan paket baru dan aktifkan ulang.

Sumber Microsoft: https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-winlogon#customshell
