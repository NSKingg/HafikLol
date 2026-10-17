using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RiftRadar;

public partial class MainWindow
{
    string leaderboardQueue="Tek / Çift",leaderboardSort="Lig";
    void RenderFriends()
    {
        Body.Children.Add(Ui.Text("Liderlik tablosu",24,null,true));Body.Children.Add(Ui.Space());
        Body.Children.Add(Ui.Text("Kayıtlı oyuncularını karşılaştır. İstatistikler seçili moddaki son 20 maçtan hesaplanır; yeniden oynanan maçlar hariçtir. Arena’da başarı ilk yarıda bitirmektir.",12,Ui.Muted));Body.Children.Add(Ui.Space());
        if(Players.Count==0){Body.Children.Add(Ui.Card(Ui.Text("Üstte İsim#Etiket ile oyuncu ara; kaydettiğin kişiler burada sıralanır.",14,Ui.Muted)));return;}
        var queue=new ComboBox{ItemsSource=new[]{"Tek / Çift","Esnek"},SelectedItem=leaderboardQueue,Width=155};
        var order=new ComboBox{ItemsSource=new[]{"Lig","KDA","Başarı oranı","Hasar / dk","CS / dk","Görüş / dk"},SelectedItem=leaderboardSort,Width=165};
        var board=Ui.Stack();var details=Ui.Stack();LeaderEntry? chosen=null;
        async Task Refresh(bool all)
        {
            if(busy)return;if(demo!=null){SetNotice("ÖRNEK MOD · Oyuncular ve sıralama kurgusaldır.");return;}
            var players=all?Players.ToList():chosen==null?[]:new List<Player>{chosen.Player};if(players.Count==0)return;
            await Run("Liderlik tablosu yenileniyor…",async ct=>
            {
                var failed=new List<string>();int refreshed=0;
                foreach(var player in players)
                {ct.ThrowIfCancellationRequested();try{await FetchPlayer(player,ct,false,false);refreshed++;}catch(OperationCanceledException){throw;}catch(Exception ex){failed.Add(player.RiotId+": "+SafeError.Message(ex));}}
                SetNotice($"{refreshed} oyuncu yenilendi."+(failed.Count>0?" "+string.Join(" · ",failed):""),failed.Count>0);
            });
            if(page=="friends"&&!closing)Fill();
        }
        var controls=Ui.Row(Ui.Stack(Ui.Caption("DERECELİ SIRA"),queue),Ui.Stack(Ui.Caption("SIRALAMA"),order),Ui.Button("Herkesi yenile",async()=>await Refresh(true)));
        Body.Children.Add(Ui.Card(controls));Body.Children.Add(board);Body.Children.Add(details);
        void Detail()
        {
            details.Children.Clear();if(chosen==null)return;var player=chosen.Player;
            var actions=Ui.Row(Ui.Button("Profili aç",()=>{UpdatePlayers(player);Navigate("overview");}),Ui.Button("Oyuncuyu yenile",async()=>await Refresh(false)),Ui.Button("Listeden kaldır",()=>{if(demo!=null)return;store.Players.Remove(player);store.Save();UpdatePlayers();Fill();}));
            details.Children.Add(Ui.Card(Ui.Stack(Ui.Row(Ui.ProfilePortrait(data,player,38),Ui.Stack(Ui.Text(player.RiotId,17,null,true),Ui.Text($"{Region.Find(player.Platform)} · Son yenileme: {chosen.Updated}",11,Ui.Muted),Ui.RankBadge(player,leaderboardQueue=="Esnek"?"RANKED_FLEX_SR":"RANKED_SOLO_5x5"))),Ui.Space(),actions)));
        }
        void Fill()
        {
            var selectedKey=chosen?.Player.Key;board.Children.Clear();
            var entries=Comparison.Sort(Players.Select(p=>Comparison.Entry(p,demo?.Matches??store.Matches(p),ModeBox.SelectedItem?.ToString()??Modes.All[0],leaderboardQueue=="Esnek"?"RANKED_FLEX_SR":"RANKED_SOLO_5x5")),leaderboardSort);
            chosen=entries.FirstOrDefault(e=>e.Player.Key==selectedKey)??entries.FirstOrDefault();
            var cards=new List<(LeaderEntry Entry,Border Card)>();
            void Highlight(){foreach(var item in cards){var active=item.Entry==chosen;item.Card.BorderBrush=active?Ui.Accent:Ui.Line;item.Card.Background=Ui.Brush(active?"#202C3D":"#1C2028");}Detail();}
            string Number(double? value,string format="0.00")=>value?.ToString(format,System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))??"—";
            foreach(var entry in entries)
            {
                var rank=RankVisuals.Entry(entry.Player,leaderboardQueue=="Esnek"?"RANKED_FLEX_SR":"RANKED_SOLO_5x5");
                var identity=Ui.Row(Ui.Text($"#{entry.Position}",22,RankVisuals.Color(rank),true),Ui.ProfilePortrait(data,entry.Player,48),Ui.Stack(Ui.Text(entry.Name,17,null,true),Ui.Text($"{Region.Find(entry.Player.Platform)} · {entry.Updated}",11,Ui.Muted)));
                var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.Children.Add(identity);
                var badge=Ui.RankBadge(rank,44);Grid.SetColumn(badge,1);header.Children.Add(badge);
                var stats=Ui.Row(Ui.StatChip("matches",entry.Games.ToString(),"maç"),Ui.StatChip("win",Number(entry.Success,"0.0")+"%","Winrate"),Ui.StatChip("kda",Number(entry.KDA),"KDA"),Ui.StatChip("damage",Number(entry.Damage,"N0"),"hasar/dk"),Ui.StatChip("cs",Number(entry.CS),"CS/dk"),Ui.StatChip("vision",Number(entry.Vision),"görüş/dk"));
                var content=Ui.Stack(header,Ui.Space(12),stats);
                var card=Ui.Card(content,16);card.Margin=new Thickness(0);card.BorderThickness=new Thickness(1);cards.Add((entry,card));
                var shell=new Border{CornerRadius=new CornerRadius(12),BorderBrush=RankVisuals.Color(rank),BorderThickness=new Thickness(4,0,0,0),Child=card};
                var button=Ui.Button("",()=>{chosen=entry;Highlight();});button.Content=shell;button.Padding=new Thickness(0);button.Margin=new Thickness(0,0,0,10);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.ToolTip="Oyuncu işlemlerini göster";board.Children.Add(button);
            }
            board.Children.Add(Ui.Text("Bir oyuncuya tıklayarak profilini açabilir, yenileyebilir veya listeden kaldırabilirsin. — henüz veri yok. Dereceler yukarıdaki sıra türüne aittir.",11,Ui.Muted));Highlight();
        }
        queue.SelectionChanged+=(_,_)=>{leaderboardQueue=queue.SelectedItem?.ToString()??"Tek / Çift";Fill();};
        order.SelectionChanged+=(_,_)=>{leaderboardSort=order.SelectedItem?.ToString()??"Lig";Fill();};Fill();
    }
}
