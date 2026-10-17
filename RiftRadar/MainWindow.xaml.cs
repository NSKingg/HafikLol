using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RiftRadar;

public partial class MainWindow : Window
{
    readonly Store store;
    readonly BackendClient api;
    readonly StaticData data;
    Demo? demo;
    List<Player> Players=>demo?.Players??store.Players;
    Player? selected;
    List<JsonNode> matches=[];
    List<MatchRow> Rows=>selected==null?[]:Analytics.For(matches,selected,ModeBox.SelectedItem?.ToString()??"Tüm modlar");
    string page="overview";
    bool busy,ready,closing;
    CancellationTokenSource? operation;
    CancellationTokenSource? liveOperation;
    bool monitoring,liveBusy;
    readonly DispatcherTimer metricsTimer=new(){Interval=TimeSpan.FromSeconds(10)};
    readonly DispatcherTimer liveTimer=new();
    JsonNode? livePlayers,liveGame,liveEvents,spectator;
    DateTimeOffset? liveUpdated;
    readonly Dictionary<string,Button> nav=[];
    public MainWindow(Store store,bool example,bool autoLive=true,bool enableTray=true)
    {
        InitializeComponent();Ui.DarkTitle(this);this.store=store;api=new BackendClient(store);data=new StaticData(store);if(example)demo=new();
        RegionBox.ItemsSource=Region.All;RegionBox.SelectedItem=Region.Find(store.Preferences.Platform);
        ModeBox.ItemsSource=Modes.All;ModeBox.SelectedIndex=0;
        SearchBox.Text="";
        foreach(var (id,label) in new[]{("overview","Genel bakış"),("live","Canlı maç"),("history","Maç geçmişi"),("champions","Şampiyonlar"),("friends","Liderlik tablosu"),("insights","Performans")})
        {
            var key=id;var b=Ui.Button(label,()=>Navigate(key));b.Content=SidebarVisuals.Label(id,label);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Padding=new Thickness(13,12,5,12);b.FontSize=13;b.Margin=new Thickness(0,0,0,7);b.Background=System.Windows.Media.Brushes.Transparent;Navigation.Children.Add(b);nav[id]=b;
        }
        SettingsNav.Content=SidebarVisuals.Label("settings","Ayarlar");
        ThemePalette.Apply(store.Preferences.LightMode);ThemeSwitchHost.Content=CreateThemeSwitch();
        PlayerBox.ItemTemplate=Ui.PlayerTemplate(data);
        ready=true;UpdatePlayers();Render();
        Loaded+=async(_,_)=>
        {
            if(autoLive)_=StartLive();
            try{await api.Start();KeyBadge();SetNotice(demo!=null?"ÖRNEK MOD · Tüm oyuncular, maçlar ve canlı veriler kurgusaldır.":api.HasKey?"Hazır · Riot ID ile oyuncu arayabilirsin.":"Ayarlar’dan sunucu bağlantını ekle. Eşyalar ve kendi canlı maçın çevrimdışı da kullanılabilir.");}
            catch(Exception ex){SetNotice((api.HasSavedServer?"Kayıtlı bağlantın korunuyor; adres veya davet kodunu yeniden girmen gerekmiyor. ":"Sunucu bağlantısı kurulamadı. ")+SafeError.Message(ex),true);}
            KeyBadge();Render();UpdateMetrics();
        };
        metricsTimer.Tick+=(_,_)=>{if(WindowState!=WindowState.Minimized)UpdateMetrics();};metricsTimer.Start();
        liveTimer.Tick+=async(_,_)=>await PollLive();
        Closing+=async(_,e)=>
        {
            if(closing)return;
            if(!exitRequested&&store.Preferences.CloseToTray&&trayIcon!=null){e.Cancel=true;HideToTray();return;}
            e.Cancel=true;closing=true;
            if(Application.Current is App app)app.IsExiting=true;
            try
            {
                // Leave the original Closing event before Close can be called again,
                // even when the server client finishes cleanup synchronously.
                await Dispatcher.Yield(DispatcherPriority.Background);
                DisposeTray();CloseProfilePreview();CloseMatch();StopLive();operation?.Cancel();metricsTimer.Stop();
                await api.Stop();
            }
            catch { /* Cleanup errors must not interrupt an explicit exit. */ }
            finally { Close(); }
        };
        if(enableTray)InitializeTray();
        StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized && store.Preferences.Economy)liveTimer.Interval=TimeSpan.FromSeconds(15);else liveTimer.Interval=TimeSpan.FromSeconds(store.Preferences.PollSeconds);if(WindowState!=WindowState.Minimized&&page=="live")Render();};
    }
    void KeyBadge(){KeyStatus.Content=api.HasKey?"●  Sunucu hazır":api.HasSavedServer?"●  Bağlantı kayıtlı":"Sunucuya bağlan";KeyStatus.ToolTip=api.HasSavedServer?"Sunucu bağlantın bu Windows hesabında kayıtlı. Sonraki açılışlarda otomatik kullanılır.":"Sunucu adresi ve kişisel davet koduyla bir kez bağlan. Riot anahtarı sunucuda tutulur.";}
    void SetNotice(string text,bool error=false){if(closing)return;Notice.Text=text;NoticeBorder.Background=Ui.Brush(error?"#392A30":"#26262B");}
    void UpdateMetrics()
    {
        if(closing)return;
        var p=Process.GetCurrentProcess();long memory=p.WorkingSet64;
        try{if(api.BackendPid>0)memory+=Process.GetProcessById(api.BackendPid).WorkingSet64;}catch{}
        ResourceText.Text=$"{memory/1024/1024} MB toplam RAM · {(monitoring?"Canlı takip açık":"Boşta ağ isteği yok")}";
        StatusText.Text=$"{(demo!=null?"Örnek veri":selected?.RiotId??"Hazır")}  ·  {api.Requests} API isteği / {api.CacheHits} önbellek  ·  Veri {data.Version}";
    }
    void UpdatePlayers(Player? prefer=null)
    {
        ready=false;var items=Players.OrderByDescending(p=>p.LastViewed).ToList();if(prefer!=null && !items.Any(x=>x.Key==prefer.Key))items.Add(prefer);
        PlayerBox.ItemsSource=items;selected=prefer??items.FirstOrDefault(x=>x.Key==store.Preferences.Selected)??items.FirstOrDefault();PlayerBox.SelectedItem=selected;
        matches=selected==null?[]:demo?.Matches??store.Matches(selected);ready=true;
    }
    internal void Navigate(string id){CloseProfilePreview();CloseMatch();page=id;ScrollAssist.Stop(ContentScroll);ContentScroll.ScrollToTop();Render();Motion.Reveal(Body);}
    void PlayerChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ready)return;CloseMatch();selected=PlayerBox.SelectedItem as Player;matches=selected==null?[]:demo?.Matches??store.Matches(selected);
        if(demo==null){store.Preferences.Selected=selected?.Key??"";store.Save();}Render();
    }
    void ModeChanged(object sender,SelectionChangedEventArgs e){if(ready){CloseMatch();Render();}}
    void SettingsClick(object sender,RoutedEventArgs e)=>Navigate("settings");
    void CancelClick(object sender,RoutedEventArgs e)=>operation?.Cancel();
    async void SearchClick(object sender,RoutedEventArgs e)=>await Lookup(SearchBox.Text);
    async void SearchKey(object sender,KeyEventArgs e){if(e.Key==Key.Enter)await Lookup(SearchBox.Text);}
    async void RefreshClick(object sender,RoutedEventArgs e){if(selected!=null)await RefreshPlayer(selected);else Navigate("settings");}
    async Task Run(string title,Func<CancellationToken,Task> work)
    {
        if(closing)return;
        if(busy){SetNotice("Bir işlem sürüyor. Tamamlanmasını bekle veya İptal'e bas.");return;}
        busy=true;operation=new();CancelButton.Visibility=Visibility.Visible;RefreshButton.IsEnabled=false;SetNotice(title);
        try{await work(operation.Token);UpdateMetrics();}
        catch(Exception ex){SetNotice(SafeError.Message(ex),true);}
        finally{busy=false;operation.Dispose();operation=null;CancelButton.Visibility=Visibility.Collapsed;RefreshButton.IsEnabled=true;KeyBadge();}
    }
    async Task Lookup(string id)
    {
        CloseMatch();
        if(demo!=null){SetNotice("Örnek mod açık. Gerçek oyuncu aramak için Ayarlar'dan örnek modu kapat.");return;}
        await Run("Oyuncu aranıyor…",async ct=>
        {
            var r=RegionBox.SelectedItem as Region??Region.All[0];var a=await api.Account(id.Trim(),r,ct);
            var existing=store.Players.FirstOrDefault(x=>x.Puuid==a.S("puuid")&&x.Platform==r.Platform);
            var p=existing??new Player {Puuid=a.S("puuid"),GameName=a.S("gameName"),TagLine=a.S("tagLine"),Platform=r.Platform};
            var s=await api.Summoner(p.Puuid,r,ct);p.Level=s.I("summonerLevel");p.Icon=s.I("profileIconId");
            p.GameName=a.S("gameName",p.GameName);p.TagLine=a.S("tagLine",p.TagLine);store.RememberPlayer(p);
            UpdatePlayers(p);Navigate("overview");await FetchPlayer(p,ct,false);
        });
    }
    async Task RefreshPlayer(Player p,bool more=false)
    {
        if(demo!=null){SetNotice("ÖRNEK MOD · Bu görünüm kurgusal verileri kullanıyor.");return;}
        await Run(more?"Daha eski maçlar okunuyor…":"Oyuncu bilgileri yenileniyor…",ct=>FetchPlayer(p,ct,more));
    }
    async Task FetchPlayer(Player p,CancellationToken ct,bool more,bool render=true)
    {
        var r=Region.Find(p.Platform);var warnings=new List<string>();
        // Optional endpoints cannot discard successfully fetched history.
        if(!more)
        {
            var summoner=await api.Summoner(p.Puuid,r,ct);p.Level=summoner.I("summonerLevel");p.Icon=summoner.I("profileIconId");
            try{p.Ranks=(await api.Ranks(p.Puuid,r,ct))?.AsArray()??[];}catch(ApiException e)when(e.Code is 404 or >=500){warnings.Add("Derece bilgisi alınamadı.");}
            try{p.Mastery=(await api.Mastery(p.Puuid,r,ct))?.AsArray()??[];}catch(ApiException e)when(e.Code is 404 or >=500){warnings.Add("Ustalık bilgisi alınamadı.");}
        }
        var start=more?p.HistoryCursor:0;
        var incoming=await api.History(p,start,store.Preferences.MatchCount,new Progress<string>(s=>SetNotice(s)),ct);
        p.HistoryCursor=more?start+api.LastHistoryCount:Math.Max(p.HistoryCursor,api.LastHistoryCount);
        p.MatchIds=incoming.Select(x=>x["metadata"].S("matchId")).Concat(p.MatchIds).Distinct().Take(500).ToList();p.Updated=DateTimeOffset.Now;
        store.Save();if(selected?.Key==p.Key){matches=store.Matches(p);if(render)Render();}
        SetNotice($"{incoming.Count} maç okundu · {p.MatchIds.Count} maç yerelde kayıtlı. "+string.Join(" ",warnings)+" "+api.HistoryWarning);
    }
    void Render()
    {
        if(!ready||closing||matchView!=null)return;ScrollAssist.Stop(ContentScroll);Body.Children.Clear();LiveHost.Content=null;LiveHost.Visibility=Visibility.Collapsed;ContentScroll.Visibility=page=="live"?Visibility.Collapsed:Visibility.Visible;AppFooter.Visibility=page=="live"?Visibility.Collapsed:Visibility.Visible;foreach(var n in nav)n.Value.Background=Ui.Brush(n.Key==page?"#253249":"#17191E");
        switch(page){case"settings":RenderSettings();break;case"friends":RenderFriends();break;case"live":RenderLive();break;case"history":RenderHistory();break;case"champions":RenderChampions();break;case"insights":RenderInsights();break;default:RenderOverview();break;}
    }
    bool NeedPlayer()
    {
        if(selected!=null)return false;
        if(api.HasKey)return true;
        var panel=Ui.Stack(Ui.Text("Takımın için bir başlangıç noktası.",24,null,true),Ui.Space(10),Ui.Text("Sunucuna bağlandıktan sonra İsim#Etiket ile oyuncu ara. Riot anahtarını sunucu yönetir. Kendi açık maçının canlı verileri sunucusuz da çalışır. Varsayılan sunucun TR.",14,Ui.Muted),Ui.Space(22),Ui.Row(Ui.Button(api.HasKey?"Sunucu bağlantısı":"Sunucuya bağlan",()=>Navigate("settings"),true),Ui.Button("Kendi canlı maçım",()=>Navigate("live")),Ui.Button("Örnek görünümü aç",()=>{demo=new();UpdatePlayers();SetNotice("ÖRNEK MOD · Tüm veriler kurgusaldır.");Render();})));
        Body.Children.Add(Ui.Card(panel,28));
        Body.Children.Add(Ui.Row(Ui.Stat("OYUNCU ANALİZİ","KDA + CS","Hasar, görüş ve ekonomi"),Ui.Stat("BİRLİKTE OYNA","Arkadaşlar","Karşılaştırma ve ortak maçlar"),Ui.Stat("MAÇ SIRASINDA","Canlı takip","Salt okunur yerel API")));
        return true;
    }
    void SaveFriend()
    {
        if(selected==null||demo!=null)return;
        if(!store.Players.Any(x=>x.Key==selected.Key))store.Players.Add(selected);
        store.Save();UpdatePlayers(selected);Render();SetNotice("Oyuncu arkadaş listesine eklendi.");
    }
    void ExportRows()
    {
        var dlg=new SaveFileDialog{FileName="RiftRadar-maclar.csv",Filter="CSV dosyası|*.csv"};
        if(dlg.ShowDialog(this)==true){File.WriteAllText(dlg.FileName,Analytics.Csv(Rows),new UTF8Encoding(true));SetNotice("Filtrelenen maçlar CSV olarak kaydedildi.");}
    }
}
