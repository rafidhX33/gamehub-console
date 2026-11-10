# Rancangan produk

## Tujuan

Laptop tetap Windows untuk aktivitas biasa, tetapi menjadi ruang bermain seperti konsol ketika pengguna membuka GameHub. Desktop, Console, dan Dock adalah tiga tampilan satu library; favorit dan riwayat konsisten di semuanya.

## Arah visual

Referensi pengguna menjadi acuan komposisi, bukan aset yang disalin. Panel utama lebar, sudut chamfer, notch kecil, sidebar kiri, hero game, cover horizontal, panel informasi kanan, dan dock bawah. Glow dipakai sebagai penanda pilihan, bukan dekorasi di setiap komponen.

- Latar: #121723 / #1E2637.
- Surface: #263143, dengan sudut asimetris 18–40 px.
- Aksen: coral #EF607D, violet #A895F4, cyan #65D4DB.
- Teks: #ECF0F8; sekunder #9CA9BC.
- Font: Segoe UI bawaan Windows.
- Spasi: kelipatan 4; area interaksi minimum sekitar 40 px.
- Cover asli jika tersedia; fallback berupa inisial dan landscape vektor, tanpa game fiktif.

## Alur utama

Login Windows → Console opsional → scan lokal → pilih game → Play → launcher menyerahkan input ke game. Esc kembali ke mode Desktop. Tombol Dock mengecilkan panel menjadi pemilih cover dan tombol Play.

## Responsif

Panel informasi kanan disembunyikan pada lebar <1120 px. Library dapat digulir horizontal. Desktop memiliki ukuran minimum; Console menggunakan seluruh monitor dengan artwork sebagai latar, kartu landscape dan tab pill mengikuti referensi kedua. Shell dihasilkan dari ukuran jendela sehingga sudut dan notch bukan gambar bitmap yang ditarik.

## Akses input

Keyboard, mouse dan XInput. Pilihan library memakai border coral, hover cyan, serta focus ring pada tombol. Aplikasi hanya memproses controller ketika jendelanya aktif. Versi 0.2 memiliki menu controller, keyboard layar, file browser dan pilihan hasil artwork. Login launcher pihak ketiga dan recovery Windows tetap berada di luar UI aplikasi.

## Batas visual versi awal

Belum menggunakan acrylic/Mica asli, transisi mode, parallax cover, atau animasi controller. `concept.svg` adalah rancangan Desktop/Dock dan `console-concept.svg` rancangan Console terbaru; keduanya bukan screenshot runtime. Pemeriksaan tampilan di Windows tetap diperlukan sebelum dinyatakan final.
