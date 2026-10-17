using System.IO;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RiftRadar;

public partial class MainWindow
{
    void RenderServerSettings()
    {
        var address=new TextBox{Text=api.ServerAddress,ToolTip="https://sunucun.workers.dev",MaxLength=250};
        var invite=new PasswordBox{MaxLength=128,ToolTip="Kişisel davet kodun. Riot API anahtarı değildir."};
        if(api.HasSavedServer)
            Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text("✓  Sunucu bağlantın kayıtlı",18,Ui.Accent,true),Ui.Space(8),Ui.Text("Her açılışta otomatik bağlanır. Davet kodu alanının boş görünmesi normal; kayıtlı kod şifreli saklanır. Sadece farklı bir sunucuya veya davete geçmek istersen aşağıdaki alanları değiştir.",13,Ui.Muted))));
        async Task Connect(string server,string code)
        {
            await Run("Sunucu bağlantısı kontrol ediliyor…",async ct=>{await api.ConnectServer(server.Trim(),code.Trim(),ct);invite.Clear();KeyBadge();Render();SetNotice("Bağlantı kaydedildi. Sonraki açılışlarda otomatik bağlanacak.");});
        }
        var open=Ui.Button("Bağlantı dosyasını aç",async()=>
        {
            var dialog=new OpenFileDialog{Filter="Rift Radar bağlantısı|*.rrconnection",Title="Kişisel bağlantı dosyanı seç"};
            if(dialog.ShowDialog(this)!=true)return;
            try
            {
                if(new FileInfo(dialog.FileName).Length>4096)throw new IOException("Bağlantı dosyası çok büyük.");
                var settings=J.Parse(await File.ReadAllTextAsync(dialog.FileName));
                if(settings.I("protocol")!=1)throw new IOException("Bağlantı dosyasının sürümü desteklenmiyor.");
                await Connect(settings.S("server"),settings.S("invite"));
            }
            catch(Exception ex){SetNotice(SafeError.Message(ex),true);}
        },true);
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text("Arkadaş grubunun sunucusuna bağlan",20,null,true),Ui.Space(8),Ui.Text("Sunucu sahibinden aldığın kişisel bağlantı dosyasını bir kez aç. Daha sonra yalnızca oyuncunun İsim#Etiket bilgisini girmen yeterli. Riot anahtarı sunucuda kalır.",13,Ui.Muted),Ui.Space(),open,Ui.Space(),Ui.Caption("VEYA SUNUCU ADRESİ VE DAVET KODU GİR"),address,Ui.Space(8),invite,Ui.Space(),Ui.Row(Ui.Button("Bağlantıyı kontrol et ve kaydet",async()=>await Connect(address.Text,invite.Password)),Ui.Button("Bağlantıyı kaldır",()=>{api.DisconnectServer();KeyBadge();Render();SetNotice("Bu bilgisayardaki sunucu bağlantısı kaldırıldı.");})),Ui.Space(),Ui.Text("Davet kodu bu Windows hesabına bağlı şifreli saklanır. Bağlantı dosyanı yalnızca sana ait tut; dosyayı alan kişi senin erişimini kullanabilir. Arkadaşlar ve dizilimler bilgisayarında saklanır. Kendi maçının yerel canlı verileri sunucuya gönderilmez.",12,Ui.Muted))));
    }
}
