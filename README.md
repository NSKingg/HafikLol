# HafikLol

<p align="center"><img src="RiftRadar/Assets/CatLogo.png" alt="HafikLol" width="140" /></p>

## İndir ve kullan

**[HafikLol'u Windows icin indir](https://github.com/NSKingg/HafikLol/releases/latest)** — `HafikLol.exe` dosyasını aç. 

Oyuncu aramak için Ayarlar’dan kendi sunucu adresini ve kişisel davet kodunu bir kez ekle. Bu depo herkese açık bir Riot API hizmeti veya davet kodu içermez. Kendi grubun için sunucu kurulumunu aşağıda bulabilirsin.

## Özellikler

- Riot ID ile arama, kayıtlı oyuncular ve LoL profil simgeleri.
- Genel bakış, ayrıntılı maç geçmişi ve daha fazla maç yükleme.
- Maç içinde skor tablosu, eşyalar, rünler ve tıklanabilir oyuncu profilleri.
- Şampiyon istatistikleri, ustalık amblemleri, rank ve Winrate sıralaması.
- Arkadaşlar için liderlik tablosu; Tek/Çift ve Esnek dereceleri.
- Otomatik yerel canlı maç takibi ve 10 oyunculuk görünüm.
- Uygulama içi kısa profiller: rank, Winrate, ustalık ve son maçlar.
- Açık/koyu tema, sistem tepsisi ve kapatınca tepsiye küçülme tercihi.
- Kedi logosuna tıklayınca küçük bir sürpriz.

Veri kapsamı Riot API’sinin sağladığı alanlara bağlıdır. Olası pre göstergeleri, geçmişte birlikte oynanan maçlardan çıkarılır; kesin parti bilgisi değildir.

## Sunucu

Cloudflare Worker ve Durable Object kaynakları: [RiftRadar.Server](RiftRadar.Server/README.md).

Sunucu Riot anahtarını saklar; masaüstü uygulaması yalnızca sunucu adresi ve kendine ait davet koduyla bağlanır. Yerel canlı maç verileri sunucuya gönderilmez. Sunucuya ait bir API anahtarı veya bağlantı dosyası depoda bulunmaz.

## Veri ve oyunla etkileşim

Oyuncular, tercihler ve önbellek Windows kullanıcı profilinde yerelde tutulur. Sunucu bağlantısı Windows DPAPI ile korunur.

Uygulama resmî HTTPS Riot API’sini ve salt okunur Live Client Data API’sini kullanır. Oyun belleği okuma, DLL enjeksiyonu, otomatik girdi veya oyun üstü katman içermez. Riot tarafından onaylanmış bir ürün değildir; sıfır yaptırım riski garantisi verilmez.

## Topluluk

[Hafik Discord sunucusu](https://discord.gg/6xym2VfUTu)

## Üçüncü taraf içerikler

League of Legends görselleri ve verileri Riot Games’e aittir. Discord logosu Discord’a aittir. İlgili kaynak adresleri varlık klasörlerindeki kaynak JSON dosyalarında tutulur. .NET bildirimleri [licenses](licenses) klasöründedir. Depo için ayrıca bir açık kaynak lisansı seçilmemiştir.

Rift Radar is not endorsed by Riot Games and does not reflect the views or opinions of Riot Games or anyone officially involved in producing or managing Riot Games properties. Riot Games and all associated properties are trademarks or registered trademarks of Riot Games, Inc.
