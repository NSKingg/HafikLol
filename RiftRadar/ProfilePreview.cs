using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RiftRadar;

public partial class MainWindow
{
    CancellationTokenSource? previewWork;
    internal void CloseProfilePreview()
    {
        previewWork?.Cancel();previewWork?.Dispose();previewWork=null;
        ProfileOverlay.Children.Clear();ProfileOverlay.Visibility=Visibility.Collapsed;
    }
    internal async void OpenProfilePreview(string id,string champion)
    {
        CloseProfilePreview();previewWork=new();var ct=previewWork.Token;
        var panel=Ui.Stack();var close=Ui.Button("✕",CloseProfilePreview);close.HorizontalAlignment=HorizontalAlignment.Right;
        var heading=new DockPanel();DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);heading.Children.Add(Ui.Text("Oyuncu profili",19,null,true));panel.Children.Add(heading);panel.Children.Add(Ui.Space());
        var body=Ui.Stack(Ui.Text(id,19,null,true),Ui.Space(),Ui.Text("Profil yükleniyor…",13,Ui.Muted));panel.Children.Add(body);
        var scroll=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=580};
        var card=new Border{Child=scroll,Width=430,MaxHeight=640,Padding=new Thickness(20),Margin=new Thickness(24),Background=Ui.Panel,BorderBrush=Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
        ProfileOverlay.Children.Add(card);ProfileOverlay.Visibility=Visibility.Visible;Motion.Reveal(card);close.Focus();
        try
        {
            var cached=KnownProfile(id);if(cached!=null)ShowProfilePreview(body,cached,champion,false);
            if(!api.HasKey&&demo==null){body.Children.Add(Ui.Space());body.Children.Add(Ui.Text("Güncel istatistikleri yüklemek için sunucu bağlantısı gerekli.",12,Ui.Muted));return;}
            var region=RegionBox.SelectedItem as Region??Region.All[0];
            var profile=await LoadLiveProfile(id,20,true,region,ct);ct.ThrowIfCancellationRequested();
            ShowProfilePreview(body,profile,champion,true);RefreshLiveCards();
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            if(ct.IsCancellationRequested)return;
            body.Children.Add(Ui.Space());body.Children.Add(Ui.Text(SafeError.Message(ex),12,Ui.Red));
        }
    }
    void ShowProfilePreview(StackPanel panel,LiveProfile profile,string champion,bool completed)
    {
        panel.Children.Clear();var p=profile.Player;
        panel.Children.Add(Ui.Row(Ui.ProfilePortrait(data,p,48),Ui.Stack(Ui.Text(p.RiotId,20,null,true),Ui.RankBadge(p))));panel.Children.Add(Ui.Space(14));
        var rows=Analytics.For(profile.Matches,p).Take(20).ToList();var valid=rows.Where(r=>!r.Remake).ToList();
        panel.Children.Add(Ui.Row(Ui.Stat("KAZANMA / İLK YARI",valid.Count==0?"—":$"%{100d*valid.Count(r=>r.Win)/valid.Count:0}",$"{valid.Count} maç"),Ui.Stat("KDA",rows.Count==0?"—":$"{rows.Sum(r=>r.P.N("kills")+r.P.N("assists"))/Math.Max(1,rows.Sum(r=>r.P.N("deaths"))):0.00}","Son maçlar")));
        panel.Children.Add(Ui.Text("Arena’da başarı, ilk yarıda bitirmektir. Yeniden oynanan maçlar kazanma oranına katılmaz.",10,Ui.Muted));panel.Children.Add(Ui.Space(12));
        var mastery=LiveComparison.ChampionMastery(p,champion,data);
        panel.Children.Add(Ui.Row(Ui.Portrait(data,champion,52),Ui.Stack(Ui.Text(data.Champion(champion).S("name",champion),17,null,true),Ui.MasteryBadge(mastery,true,32))));
        panel.Children.Add(Ui.Space(14));panel.Children.Add(Ui.Text("En yüksek ustalıkların",14,null,true));
        foreach(var m in p.Mastery.Rows().OrderByDescending(m=>m.N("championPoints")).Take(3))
            panel.Children.Add(Ui.Card(Ui.Row(Ui.Portrait(data,data.ChampionName(m.I("championId")),32),Ui.Stack(Ui.Text(data.ChampionName(m.I("championId")),12,null,true),Ui.MasteryBadge(m,true,28))),10));
        panel.Children.Add(Ui.Text("Son maçlar",14,null,true));panel.Children.Add(Ui.Space(8));
        foreach(var row in rows.Take(5))
        {
            var detail=Ui.Row(Ui.Portrait(data,row.Champion,34),Ui.Stack(Ui.Text($"{row.Champion} · {row.Result}",12,Ui.Outcome(row),true),Ui.Text($"{row.Kda} · {row.Mode} · {row.Date}",10,Ui.Muted)));
            panel.Children.Add(Ui.Card(detail,10));
        }
        if(rows.Count==0)panel.Children.Add(Ui.Text("Maç geçmişi henüz yok.",12,Ui.Muted));
        if(!completed){panel.Children.Add(Ui.Space());panel.Children.Add(Ui.Text("Güncel profil yükleniyor…",11,Ui.Muted));}
    }
    void ProfileBackdropClick(object sender,MouseButtonEventArgs e){if(ReferenceEquals(e.OriginalSource,ProfileOverlay))CloseProfilePreview();}
}
