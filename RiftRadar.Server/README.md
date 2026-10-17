# HafikLol sunucusu

Cloudflare Workers + SQLite Durable Object ile küçük bir arkadaş grubunun Riot API erişimini paylaşır. En fazla 10 davet özeti kabul edilir. Anahtarlar ve davetler kaynak koduna yazılmaz.

## Kurulum

Node.js ve pnpm gerekir.

```powershell
pnpm install --frozen-lockfile
pnpm exec wrangler login
```

`wrangler.jsonc` dosyasındaki Worker adını kendi hesabına uygun olarak seç. Ardından:

```powershell
pnpm exec wrangler secret put RIOT_API_KEY
```

Riot Developer Portal’dan aldığın yetkili anahtarı yalnızca secret istemine gir.

Her kullanıcı için farklı bir bağlantı dosyası üret:

```powershell
node tools/invite.mjs create https://SENIN-WORKER.workers.dev/ kendi.rrconnection
```

Araç tokenı dosyaya yazar ve SHA-256 özetini ekrana verir. Özetleri JSON string dizisi olarak `CLIENT_HASHES` secret’ına ekle (en fazla 10):

```powershell
pnpm exec wrangler secret put CLIENT_HASHES
pnpm exec wrangler deploy
```

Bağlantı dosyalarını kişilere ayrı ayrı ver. `.rrconnection`, `.dev.vars` ve gerçek secret değerlerini GitHub’a ekleme. Bir kullanıcının erişimini iptal etmek için onun özetini secret listesinden kaldır.

## Testler

```powershell
pnpm test
```

Riot API uç noktaları izin listesiyle sınırlıdır. Ortak önbellek ve istek sınırları uygulanır. Dağıtım ve kullanım için Riot’un anahtar türüne ve uygulama politikalarına uyulmalıdır. Cloudflare plan/kota koşullarını kendi hesabından kontrol et.
