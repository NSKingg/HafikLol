using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RiftRadar;

public partial class MainWindow
{
    string championSort="Maç sayısı";
    FrameworkElement MasterySummary(Player player,int count=3)
    {
        var entries=player.Mastery.Rows().OrderByDescending(m=>m.N("championPoints")).Take(count).ToList();
        var panel=Ui.Stack(Ui.Text("Şampiyon ustalığı",17,null,true),Ui.Space(12));
        if(entries.Count==0)panel.Children.Add(Ui.Text("Ustalık bilgisi henüz yüklenmedi. Oyuncuyu yenileyerek getirebilirsin.",12,Ui.Muted));
        else
        {
            var grid=new UniformGrid{Columns=Math.Min(3,entries.Count)};
            foreach(var entry in entries)
            {
                var name=data.ChampionName(entry.I("championId"));var content=Ui.Row(Ui.Portrait(data,name,52),Ui.Stack(Ui.Text(name,16,null,true),Ui.MasteryBadge(entry,false,32),Ui.Text($"{entry.N("championPoints"):N0} puan",12,Ui.Muted)));
                var card=Ui.Card(content,12);card.Margin=new Thickness(0,0,10,0);grid.Children.Add(card);
            }
            panel.Children.Add(grid);
        }
        return Ui.Card(panel);
    }
    void RenderChampionPerformance()
    {
        var rows=Rows;var p=selected!;
        Body.Children.Add(Ui.Text("Şampiyon başına performans",21,null,true));Body.Children.Add(Ui.Space(12));
        var sort=new ComboBox{Width=190,ItemsSource=new[]{"Maç sayısı","Ustalık puanı","Ustalık seviyesi","Winrate","KDA","Hasar / dk","CS / dk","İsim"},SelectedItem=championSort};
        sort.SelectionChanged+=(_,_)=>{championSort=sort.SelectedItem?.ToString()??"Maç sayısı";ScrollAssist.Stop(ContentScroll);ContentScroll.ScrollToTop();Render();};
        Body.Children.Add(Ui.Card(Ui.Row(Ui.Stack(Ui.Caption("SIRALAMA"),sort),Ui.Text("Yüklenen maçlardaki şampiyonlar · ustalık sıralamasında eksik veriler sonda gösterilir.",12,Ui.Muted))));
        foreach(var group in ChampionSort.Order(rows,p,data,championSort))
        {
            var valid=group.Where(r=>!r.Remake).ToList();var mastery=LiveComparison.ChampionMastery(p,group.Key,data);
            var header=new DockPanel();
            var badge=Ui.MasteryBadge(mastery,true,64);DockPanel.SetDock(badge,Dock.Right);header.Children.Add(badge);header.Children.Add(Ui.Text(data.Champion(group.Key).S("name",group.Key),20,null,true));
            var stats=Ui.Row(Ui.StatChip("matches",group.Count().ToString(),"maç"),Ui.StatChip("win",valid.Count==0?"—":$"%{100d*valid.Count(r=>r.Win)/valid.Count:0}","Winrate"),Ui.StatChip("kda",$"{group.Sum(r=>r.P.N("kills")+r.P.N("assists"))/Math.Max(1,group.Sum(r=>r.P.N("deaths"))):0.00}","KDA"),Ui.StatChip("cs",$"{group.Average(r=>r.CsValue/r.Minutes):0.0}","CS/dk"),Ui.StatChip("damage",$"{group.Average(r=>r.Dpm):N0}","hasar/dk"),Ui.StatChip("participation",$"%{group.Average(r=>r.Kp):0}","katılım"));
            var content=Ui.Stack(header,Ui.Space(10),stats);var row=new DockPanel();var portrait=Ui.Portrait(data,group.Key,74);portrait.Margin=new Thickness(0,0,18,0);DockPanel.SetDock(portrait,Dock.Left);row.Children.Add(portrait);row.Children.Add(content);
            var share=100d*group.Count()/Math.Max(1,rows.Count);
            var progress=new ProgressBar{Minimum=0,Maximum=100,Value=share,Height=18,Foreground=Ui.Accent,Background=Ui.Line,Margin=new Thickness(0,7,0,0)};
            Body.Children.Add(Ui.Card(Ui.Stack(row,Ui.Space(7),Ui.Text($"Oynama payı · %{share:0} · Seçili filtrede {group.Count()} / {rows.Count} maç",11,Ui.Muted),progress),20));
        }
        if(rows.Count==0)Body.Children.Add(Ui.Card(Ui.Text("Bu filtrede henüz maç yok.",14,Ui.Muted)));
    }
}
