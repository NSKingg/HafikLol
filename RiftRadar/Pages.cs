using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RiftRadar;

public partial class MainWindow
{
    void RenderOverview()
    {
        if(NeedPlayer())return;var p=selected!;var rows=Rows.Take(20).ToList();var valid=rows.Where(x=>!x.Remake).ToList();
        var profile=Ui.Stack();var heading=new DockPanel();
        var actions=Ui.Row(Ui.Button(Players.Any(x=>x.Key==p.Key)?"✓  Kayıtlı":"+  Arkadaşlara ekle",SaveFriend),Ui.Button("CSV aktar",ExportRows));DockPanel.SetDock(actions,Dock.Right);heading.Children.Add(actions);
        var identity=Ui.Row(Ui.ProfilePortrait(data,p,56),Ui.Stack(Ui.Text(p.RiotId,25,null,true),Ui.Space(5),Ui.Row(Ui.Text($"Seviye {p.Level}",12,Ui.Muted),Ui.RankBadge(p))));heading.Children.Add(identity);profile.Children.Add(heading);Body.Children.Add(Ui.Card(profile,22));Body.Children.Add(MasterySummary(p));
        if(rows.Count==0){Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text("Bu filtrede henüz maç yok.",18),Ui.Space(),Ui.Text("Farklı bir mod seç veya Yenile ile maçları getir. Riot bazı modlar için veri döndürmeyebilir.",13,Ui.Muted))));return;}
        double Avg(Func<MatchRow,double> f)=>rows.Average(f);
        var arena=ModeBox.SelectedItem?.ToString()=="Arena";
        Body.Children.Add(Ui.Row(Ui.Stat("WINRATE",valid.Count>0?$"{100.0*valid.Count(x=>x.Win)/valid.Count:0}%":"—",$"{valid.Count(x=>x.Win)} başarılı · {valid.Count} maç"),Ui.Stat("KDA",$"{rows.Sum(x=>x.P.N("kills")+x.P.N("assists"))/Math.Max(1,rows.Sum(x=>x.P.N("deaths"))):0.00}",$"{Avg(x=>x.P.N("kills")):0.0} / {Avg(x=>x.P.N("deaths")):0.0} / {Avg(x=>x.P.N("assists")):0.0}"),Ui.Stat("HASAR / DK",$"{Avg(x=>x.Dpm):N0}","Şampiyonlara verilen"),Ui.Stat("CS / DK",$"{Avg(x=>x.CsValue/x.Minutes):0.0}","Minyon + orman"),Ui.Stat("KATILIM",$"{Avg(x=>x.Kp):0}%","Takım skoruna katkı")));
        var form=Ui.Stack(Ui.Text("Son maçlardaki form",15,null,true),Ui.Space(12));var blocks=new System.Windows.Controls.Primitives.UniformGrid{Columns=10,Rows=2};
        foreach(var r in rows.Take(20))
        {var b=Ui.Button(r.Arena?"#"+r.P.I("placement"):r.Remake?"R":r.Win?"W":"L",()=>OpenMatch(r));b.Padding=new Thickness(0);b.Height=48;b.FontSize=15;b.HorizontalAlignment=HorizontalAlignment.Stretch;b.Margin=new Thickness(0,0,5,6);b.Background=Ui.Brush(r.Remake?"#39393E":r.Win?"#233B60":"#512C39");b.Foreground=Ui.Outcome(r);b.ToolTip=$"{r.Champion} · {r.Mode} · {r.Kda}";blocks.Children.Add(b);}
        form.Children.Add(blocks);form.Children.Add(Ui.Text("En yeni maç sol üstte · Detay için bir maça tıkla.",11,Ui.Muted));
        var champPanel=Ui.Stack(Ui.Text("En çok oynadıkların",15,null,true),Ui.Space(12));
        foreach(var g in rows.GroupBy(x=>x.Champion).OrderByDescending(x=>x.Count()).Take(4))
        {var row=new DockPanel();var portrait=Ui.Portrait(data,g.Key,30);DockPanel.SetDock(portrait,Dock.Left);row.Children.Add(portrait);row.Children.Add(Ui.Bar(g.Key,$"{g.Count()} maç · {g.Average(x=>x.KdaValue):0.00} KDA",100.0*g.Count()/rows.Count,null,Ui.MasteryIcon(LiveComparison.ChampionMastery(p,g.Key,data),24)));champPanel.Children.Add(row);}
        Body.Children.Add(Ui.Columns(Ui.Card(form),Ui.Card(champPanel),1.25));
        Body.Children.Add(Ui.Text("Son maçlar",16,null,true));Body.Children.Add(Ui.Space(10));Body.Children.Add(HistoryTable(rows.Take(20)));
        Body.Children.Add(Ui.Space(8));Body.Children.Add(Ui.Text("Genel bakış seçili moddaki son 20 maç üzerinden hesaplanır. Arena için başarı, o maçtaki takım sayısının ilk yarısında bitirmektir. Yeniden oynanan maçlar oran hesabına katılmaz.",11,Ui.Muted));
    }
    FrameworkElement HistoryTable(IEnumerable<MatchRow> rows)
    {
        return Ui.MatchList(rows,data,OpenMatch,Math.Max(360,ActualHeight-350));
    }
    void RenderHistory()
    {
        if(NeedPlayer())return;var search=new TextBox{Width=210,ToolTip="Şampiyona göre ara"};var list=new StackPanel();
        var controls=Ui.Row(Ui.Stack(Ui.Caption("ŞAMPİYON FİLTRESİ"),search),Ui.Button("CSV aktar",ExportRows));
        Body.Children.Add(Ui.Card(controls));Body.Children.Add(Ui.Text("Her kart maç sonu eşya dizilimini gösterir. İki takımın Tab görünümünü açmak için karta tıkla; eşyanın üstünde bekleyerek statlarını ve pasiflerini oku.",12,Ui.Muted));Body.Children.Add(Ui.Space());Body.Children.Add(list);
        ListBox? history=null;
        async void LoadMore()
        {
            if(selected==null||busy)return;
            if(demo!=null){SetNotice("ÖRNEK MOD · Bu görünüm kurgusal verileri kullanıyor.");return;}
            var anchor=history?.Items.OfType<MatchRow>().LastOrDefault();
            await Run("Daha eski maçlar okunuyor…",ct=>FetchPlayer(selected,ct,true,false));
            if(page!="history"||closing)return;
            Fill();
            if(anchor!=null)history?.ScrollIntoView(history.Items.OfType<MatchRow>().FirstOrDefault(x=>x.Id==anchor.Id));
        }
        void Fill()
        {
            list.Children.Clear();var filtered=Rows.Where(x=>x.Champion.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToList();
            var footer=Ui.Stack(Ui.Text($"{filtered.Count} maç gösteriliyor",12,Ui.Muted),Ui.Space(),Ui.Button("Daha fazla yükle",LoadMore,true));
            history=Ui.MatchList(filtered,data,OpenMatch,Math.Max(360,ActualHeight-350),footer);list.Children.Add(history);
        }
        search.TextChanged+=(_,_)=>Fill();Fill();
    }
    void RenderChampions()
    {
        if(NeedPlayer())return;var rows=Rows;
        RenderChampionPerformance();
        Body.Children.Add(Ui.Text("¹ Arena maçlarında ilk yarı oranı. Modları ayrı filtreleyerek daha anlamlı karşılaştırabilirsin.",11,Ui.Muted));Body.Children.Add(Ui.Space());
        var mastery=selected!.Mastery.Rows().Select(x=>new{Şampiyon=data.ChampionName(x.I("championId")),Seviye=x.I("championLevel"),Puan=x.N("championPoints").ToString("N0"),SonOyun=x.N("lastPlayTime")>0?DateTimeOffset.FromUnixTimeMilliseconds((long)x.N("lastPlayTime")).LocalDateTime.ToString("dd MMM yyyy"):"—"}).ToList();
        var masteryTable=Ui.Table(mastery,("Şampiyon","Şampiyon",1.2),("Ustalık seviyesi","Seviye",1),("Ustalık puanı","Puan",1),("Son oyun","SonOyun",1));Ui.MasteryLevelColumn(masteryTable,"Seviye",1);masteryTable.RowHeight=88;Ui.ChampionColumn(masteryTable,data,"Şampiyon",0,56);Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text("Şampiyon ustalığı",16,null,true),Ui.Space(),masteryTable)));
        var ranks=Ui.Stack(Ui.Text("Resmî dereceler",16,null,true),Ui.Space());
        foreach(var rank in selected.Ranks.Rows())ranks.Children.Add(Ui.Card(Ui.Row(Ui.Stack(Ui.Text(RankVisuals.QueueName(rank.S("queueType")),13,Ui.Muted),Ui.RankBadge(rank,44)),Ui.Text($"{rank.I("wins")} G · {rank.I("losses")} M · %{100.0*rank.N("wins")/Math.Max(1,rank.N("wins")+rank.N("losses")):0.0} Winrate",13)),12));
        if(!selected.Ranks.Rows().Any())ranks.Children.Add(Ui.RankBadge(selected, size:44));
        Body.Children.Add(Ui.Card(ranks));
    }
    void RenderInsights()
    {
        if(NeedPlayer())return;var rows=Rows;if(rows.Count==0){Body.Children.Add(Ui.Text("Bu modda analiz edilecek maç yok."));return;}
        var count=rows.Count;double Avg(string key)=>rows.Average(x=>x.P.N(key));
        Body.Children.Add(Ui.Row(Ui.Stat("ALTIN / DK",$"{rows.Average(x=>x.P.N("goldEarned")/x.Minutes):N0}","Ortalama ekonomi"),Ui.Stat("GÖRÜŞ / DK",$"{rows.Average(x=>x.P.N("visionScore")/x.Minutes):0.00}","Modun doğasına göre yorumla"),Ui.Stat("ALINAN HASAR",$"{Avg("totalDamageTaken"):N0}","Maç başına"),Ui.Stat("KONTROL",$"{Avg("timeCCingOthers"):0.0} sn","Rakibe uygulanan CC")));
        var damage=Ui.Stack(Ui.Text("Hasar dağılımı",16,null,true),Ui.Space());
        var total=Avg("physicalDamageDealtToChampions")+Avg("magicDamageDealtToChampions")+Avg("trueDamageDealtToChampions");
        foreach(var (key,label,color) in new[]{("physicalDamageDealtToChampions","Fiziksel hasar","#E9B76C"),("magicDamageDealtToChampions","Büyü hasarı","#9EACF5"),("trueDamageDealtToChampions","Gerçek hasar","#A8D5BF")})damage.Children.Add(Ui.Bar(label,$"{Avg(key):N0}",100*Avg(key)/Math.Max(1,total),Ui.Brush(color)));
        Body.Children.Add(Ui.Card(damage));
        Body.Children.Add(RolePerformanceCard(rows));
        Body.Children.Add(TeamContributionCard(rows));
        var extras=Comparison.ExtraMetrics(rows);
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text("Ek performans istatistikleri",16,null,true),Ui.Space(),Ui.Table(extras,("İstatistik","Alan",2),("Maç başına ortalama","Değer",1)),Ui.Space(),Ui.Text("Ortalamalar yalnızca ilgili verinin Riot tarafından sağlandığı maçlardan hesaplanır. — veri yok demektir. Erken koridor üstünlüğü, Riot’un bildirdiği puandır.",11,Ui.Muted))));
        var multi=rows.Sum(x=>x.P.N("pentaKills"));Body.Children.Add(Ui.Text($"{count} maçta: {rows.Sum(x=>x.P.N("doubleKills")):0} ikide iki · {rows.Sum(x=>x.P.N("tripleKills")):0} üçte üç · {rows.Sum(x=>x.P.N("quadraKills")):0} dörtte dört · {multi:0} beşte beş. Bazı metrikler ilgili modda Riot tarafından sağlanmaz; eksik sayısal alanlar 0 kabul edilir.",11,Ui.Muted));
    }
}
