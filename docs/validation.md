# Validasi

## Otomatis

`dotnet run --project tests/GameHub.Checks -c Release` memeriksa parsing VDF, library Steam tambahan, Epic URI, deduplikasi, manifest rusak, instalasi yang hilang, penyimpanan metadata, state rusak dan pembatalan scan.

`dotnet build GameHub.sln -c Release` mengompilasi C#, XAML dan Windows adapters. Workflow Windows juga membuat portable self-contained win-x64.

## Hasil awal lingkungan pengembangan

- SDK .NET 10.0.403, build Release: **0 warning, 0 error**.
- **11 pemeriksaan regresi lulus** di Linux.
- Kompilasi WPF dilakukan dengan Windows targeting pack; UI Windows belum dijalankan di lingkungan Linux.
- Hasil workflow Windows tersedia di tab Actions repository.

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
