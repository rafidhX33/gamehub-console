# Arsitektur

- `GameHub.Core`: model library, parser Valve KeyValues, discovery Steam/Epic, merge metadata, JSON state store. Tidak bergantung pada UI atau registry.
- `GameHub.App`: WPF .NET 10; adapter registry, process launch, system information, dan XInput. View utama memiliki mode Desktop, Console, Dock.
- `GameHub.Checks`: executable pengujian deterministik tanpa framework test eksternal. Fixture manifest dibuat di folder sementara.

Discovery berjalan melalui Task.Run dengan CancellationToken. Perubahan ObservableCollection dan binding hanya di dispatcher UI. Scan tidak memindai seluruh drive dan tidak menjalankan executable saat mendeteksi game.

State disimpan sebagai JSON melalui temporary file kemudian rename. ID Steam/Epic stabil; ID manual menggunakan SHA-256 path penuh. Rescan mempertahankan favorit dan waktu main. Data manual dipertahankan. Manifest rusak dilaporkan sebagai warning, tidak menghentikan manifest valid lain.

Launcher menggunakan UseShellExecute, bukan shell command concatenation. Steam/Epic dijalankan melalui protokol terdaftar. Tidak memerlukan administrator. Startup hanya menulis nilai milik aplikasi pada HKCU Run setelah diaktifkan pengguna.

## Pemilihan framework

Metadata rilis resmi yang dibaca 10 November 2026 mencantumkan .NET 10.0.14 sebagai rilis stabil aktif/LTS dan SDK 10.0.403. .NET 11.0.0-rc.3 masih prerelease/go-live. Karena itu aplikasi menargetkan `net10.0-windows` dengan WPF dan tidak memakai preview framework.

Sumber: https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json dan https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json.

Versi 0.2 menambah `GameHub.Session` (guardian), service artwork HTTP bounded dengan cache, overlay controller, keyboard layar dan file browser. Kebijakan CustomShell ditulis hanya setelah konfirmasi pengguna; tidak diterapkan di cloud atau lewat CI.

Tidak ada paket NuGet pihak ketiga pada versi ini. Pembaruan patch SDK dilakukan melalui global.json dan workflow yang membaca file tersebut.
