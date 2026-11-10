# Cover dan banner online

GameHub mencari dan mengunduh gambar ke cache lokal; tidak hanya membaca cache Steam yang sudah ada.

- Game Steam: App ID instalasi dipakai langsung agar tidak salah judul.
- Epic / manual: judul dikirim ke endpoint store search Steam. Satu kecocokan judul persis setelah normalisasi dapat dipakai otomatis.
- Judul mirip atau ambigu: **View / Game options → Find / change online artwork** membuka hasil pencarian yang bisa dipilih dengan controller.
- Judul file kurang cocok: **Search another title** membuka keyboard layar. D-pad/stik memilih huruf, A mengetik, X menghapus, Y/Start selesai.
- Mengganti artwork tidak mengubah executable, platform, atau protokol peluncuran.

Sumber:

- `https://store.steampowered.com/api/storesearch/`
- `https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/library_600x900.jpg`
- `library_hero.jpg`; `header.jpg` sebagai fallback.

Endpoint dapat berubah dan tidak menjamin semua judul tersedia. **Game yang tidak ada di katalog Steam, termasuk Genshin Impact, tidak dijamin memiliki cover dari provider ini.** Pencarian tidak memasangkan game yang hanya mirip secara otomatis. Integrasi SteamGridDB/IGDB atau provider berlisensi untuk game non-Steam masih pengembangan berikutnya; tidak ada API key yang diminta di chat.

Gambar tetap merupakan milik pemegang hak masing-masing. Repository tidak menyertakan cover game yang diunduh. Cache hanya di laptop pengguna pada `%LOCALAPPDATA%\GameHub\artwork`. Hasil pencarian disimpan tujuh hari; gambar yang tersedia dipakai kembali secara offline. Background hydration dibatasi 24 game yang masih memerlukan artwork per scan; game terpilih tetap bisa dicari manual dari menu controller.

Online artwork aktif secara default karena fitur ini diminta pengguna. Dapat dimatikan melalui Start → Online artwork. Permintaan mengirim judul pencarian atau App ID, tidak mengirim daftar file, akun, atau folder instalasi. Timeout, batas ukuran, validasi signature JPEG/PNG, dan pembatalan mencegah respons HTML/terlalu besar diperlakukan sebagai artwork.

Library dan game tetap bisa dijalankan saat offline. Tidak ada statistik progres permainan yang dibuat dari artwork atau judul.
