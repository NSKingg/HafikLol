using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace RiftRadar;

public partial class MainWindow
{
    readonly LiveSessionTracker liveSession=new();
    string liveSignature="";
    TextBlock? liveClock;
    string LiveSignature()=>livePlayers?.ToJsonString()+":"+liveEvents.A("Events").Count()+":"+liveEvents.A("Events").LastOrDefault()?.I("EventID");
    string LiveLabel()=>liveGame==null?"Maç bağlantısı bekleniyor… Oyuna girdikten sonra otomatik bağlanır.":$"●  {liveGame.S("gameMode")}  ·  {TimeSpan.FromSeconds(Math.Max(0,liveGame.N("gameTime"))).ToString(@"mm\:ss")}  ·  Son okuma {liveUpdated?.LocalDateTime:HH:mm:ss}";
    void ResetLiveSession(){CancelRoster();liveOperation?.Cancel();livePlayers=null;liveGame=null;liveEvents=null;liveUpdated=null;liveSignature="";liveSession.Reset();}
    void StopLive(){monitoring=false;liveTimer.Stop();ResetLiveSession();}
    async Task StartLive()
    {
        if(closing||monitoring)return;monitoring=true;
        liveTimer.Interval=TimeSpan.FromSeconds(WindowState==WindowState.Minimized&&store.Preferences.Economy?15:store.Preferences.PollSeconds);
        liveTimer.Start();await PollLive();
    }
    async Task PollLive()
    {
        if(!monitoring||liveBusy||closing)return;liveBusy=true;liveOperation=new();
        try
        {
            if(demo!=null)
            {
                livePlayers=demo.LivePlayers();liveGame=new JsonObject{["gameTime"]=1245,["gameMode"]="CLASSIC"};
                liveEvents=new JsonObject{["Events"]=new JsonArray(new JsonObject{["EventTime"]=1224,["EventName"]="DragonKill"},new JsonObject{["EventTime"]=1180,["EventName"]="TurretKilled"})};
            }
            else
            {
                liveGame=await api.Live("gamestats",liveOperation.Token);
                if(liveGame!=null){livePlayers=await api.Live("playerlist",liveOperation.Token);liveEvents=null;}
                else{livePlayers=null;liveEvents=null;}
            }
            if(closing)return;
            liveUpdated=DateTimeOffset.Now;
            if(demo==null&&liveSession.Observe(liveGame)){CancelRoster();Navigate("live");}
            if(page=="live"&&matchView==null&&WindowState!=WindowState.Minimized)
            {var signature=LiveSignature();if(signature!=liveSignature){Body.Children.Clear();RenderLive();}else if(liveClock!=null)liveClock.Text=LiveLabel();}
            if(WindowState!=WindowState.Minimized)UpdateMetrics();
        }
        catch(OperationCanceledException){}
        catch(Exception ex){SetNotice(SafeError.Message(ex),true);}
        finally{liveOperation.Dispose();liveOperation=null;liveBusy=false;}
    }
    static string VisibleRiotId(JsonNode p)
    {
        var id=p.S("riotId");
        if(id.Length==0 && p.S("riotIdGameName").Length>0 && p.S("riotIdTagLine",p.S("riotIdTagline")).Length>0)id=p.S("riotIdGameName")+"#"+p.S("riotIdTagLine",p.S("riotIdTagline"));
        return id.Contains('#')?id:"";
    }
    void RenderLive()
    {
        liveSignature=LiveSignature();ContentScroll.Visibility=Visibility.Collapsed;LiveHost.Visibility=Visibility.Visible;AppFooter.Visibility=Visibility.Collapsed;
        var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
        liveClock=Ui.Text(LiveLabel(),13,liveGame==null?Ui.Muted:Ui.Accent,true);liveClock.Margin=new Thickness(0,0,0,10);layout.Children.Add(liveClock);
        FrameworkElement content;
        if(livePlayers!=null)content=BuildRoster(livePlayers.Rows());
        else if(spectator!=null)content=BuildRoster(spectator.A("participants"),true);
        else
        {
            var waiting=Ui.Stack(Ui.Text("Maç bekleniyor",23,null,true),Ui.Space(),Ui.Text("Oyuna girdiğinde oyuncular burada otomatik görünecek.",13,Ui.Muted),Ui.Space(),Ui.Button("Seçili oyuncunun maçını bul",async()=>await FindLive()));waiting.VerticalAlignment=VerticalAlignment.Center;waiting.HorizontalAlignment=HorizontalAlignment.Center;content=waiting;
        }
        Grid.SetRow(content,1);layout.Children.Add(content);LiveHost.Content=layout;
    }
    async Task FindLive()
    {
        if(selected==null){SetNotice("Önce oyuncu seç.");return;}
        if(demo!=null){SetNotice("Örnek modu: Canlı maç bölümünde kurgusal kadroyu görebilirsin.");return;}
        await Run("Aktif maç aranıyor…",async ct=>{spectator=await api.Spectator(selected.Puuid,Region.Find(selected.Platform),ct);SetNotice(spectator==null?"Aktif maç bulunamadı veya bu mod Spectator API tarafından sunulmuyor.":"Başlangıç kadrosu alındı. Bu görünüm anlık skor içermez.");Render();});
    }

}
public sealed class LiveRow
{
    public string Id {get;init;}="";public string Oyuncu{get;init;}="";public string Şampiyon{get;init;}="";public string Takım{get;init;}="";public string Seviye{get;init;}="";public string KDA{get;init;}="";public string CS{get;init;}="";
}
