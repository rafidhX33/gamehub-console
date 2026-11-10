# Validasi

## Otomatis

`dotnet run --project tests/GameHub.Checks -c Release` memeriksa parsing VDF, library Steam tambahan, Epic URI, deduplikasi, manifest rusak, instalasi yang hilang, penyimpanan metadata, state rusak dan pembatalan scan.

`dotnet build GameHub.sln -c Release` mengompilasi C#, XAML dan Windows adapters. Workflow Windows juga membuat portable self-contained win-x64.

## Hasil awal lingkungan pengembangan

- SDK .NET 10.0.403, build Release: **0 warning, 0 error**.
- **11 pemeriksaan regresi versi 0.1 lulus** di Linux; versi 0.2 menambah pemeriksaan provider artwork dan validasi paket shell.
- Versi 0.2: 21 checks deterministik lulus; 2 smoke checks tambahan berhasil terhadap pencarian judul dan CDN Steam (total 23 dengan `--online-smoke`).
- Kompilasi WPF dilakukan dengan Windows targeting pack; UI Windows belum dijalankan di lingkungan Linux.
- Hasil workflow Windows tersedia di tab Actions repository.

## Tambahan 0.2 — wajib diuji manual

- [ ] Fullscreen dan layout landscape sesuai referensi pada 720p/1080p/4K.
- [ ] Semua menu, keyboard layar, file browser dan artwork bisa dipakai hanya dengan controller.
- [ ] Internet putus tetap memakai cover cache dan library bisa dimainkan.
- [ ] CustomShell pada akun Windows Pro terpisah; login, guardian, exit dan restore.
- [ ] Simulasikan executable UI gagal: setelah tiga kegagalan, Explorer pulih.
- [ ] Pemulihan melalui Task Manager dan skrip restore.
- [ ] Windows Home menolak custom shell, startup biasa tetap berfungsi.
- [ ] Game keluar mengaktifkan kembali hub jika proses berhasil dilacak.

## Manual pada Windows — belum ditandai lulus

- [ ] Buka portable GameHub.exe sebagai pengguna biasa.
- [ ] Uji Desktop/Console/Dock pada 100%, 150%, 200% DPI.
- [ ] Uji monitor 1366x768 dan 1920x1080; cek resize dan drag.
- [ ] Steam di C: dan library di drive kedua terdeteksi.
- [ ] Epic game menjalankan launcher resmi.
- [ ] Cover asli tampil; game tanpa cover memakai fallback.
- [ ] Tambahkan exe/lnk/url, jalankan, favoritkan, restart dan cek persistensi.
- [ ] Ubah artwork lalu rescan dan restart.
- [ ] Keyboard: Tab, panah, Enter, Esc, F11, Ctrl+F.
- [ ] Xbox controller: A/B/X/Y, LB/RB, Start, D-pad, stik kiri; cabut/pasang ulang.
- [ ] Input controller tidak menggerakkan launcher saat game aktif.
- [ ] Scan dapat dibatalkan dengan menutup aplikasi.
- [ ] Startup off secara default; opt-in membuka Console pada login.
- [ ] State rusak tetap tersedia dan tidak tertimpa otomatis.

CI tidak menggantikan pengujian visual dan perangkat fisik.
