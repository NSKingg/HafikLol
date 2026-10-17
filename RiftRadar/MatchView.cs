using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RiftRadar;

public sealed class MatchView : UserControl, IDisposable
{
    readonly Action<string,string>? openProfile;readonly MatchRow match;readonly StaticData data;readonly RiotApi api;readonly Region region;readonly bool demo;readonly CancellationTokenSource lifetime=new();
    public MatchView(MatchRow row,StaticData data,RiotApi api,Region region,bool demo,Action back,Action<string,string>? openProfile=null)
    {
        this.openProfile=openProfile;match=row;this.data=data;this.api=api;this.region=region;this.demo=demo;
        var outer=new DockPanel();
        var goBack=Ui.Button("←  Geri",back);goBack.HorizontalAlignment=HorizontalAlignment.Left;goBack.Margin=new Thickness(0,0,0,12);DockPanel.SetDock(goBack,Dock.Top);outer.Children.Add(goBack);
        var header=Ui.Stack(Ui.Row(Ui.Portrait(data,row.Champion,50),Ui.Stack(Ui.Text($"{row.Champion}  ·  {row.Result}",25,Ui.Outcome(row),true),Ui.Space(5),Ui.Text($"{row.Mode}  ·  {row.Kda}  ·  {row.Duration}  ·  {row.Date}  ·  Yama {row.Patch}",12,Ui.Muted))),Ui.Space(18));DockPanel.SetDock(header,Dock.Top);outer.Children.Add(header);
        var tabs=new TabControl {Background=Ui.Panel,BorderBrush=Ui.Line,Foreground=Brushes.Black};
        tabs.Items.Add(Tab("Skor tablosu",Scoreboard()));tabs.Items.Add(Tab("Eşyalar ve rünler",Loadout()));tabs.Items.Add(Tab("Tüm metrikler",AllMetrics()));tabs.Items.Add(Tab("Zaman çizelgesi",Timeline()));outer.Children.Add(tabs);Content=outer;
    }
    bool disposed;
    public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();lifetime.Dispose();}
    static TabItem Tab(string name,UIElement content)=>new(){Header=name,Padding=new Thickness(17,10,17,10),Content=new ScrollViewer{Content=content,Margin=new Thickness(14)}};
    UIElement Scoreboard()
    {
        var panel=new StackPanel();
        var participants=match.Info.A("participants").Select(p=>new MatchRow{Raw=match.Raw,P=p}).ToList();
        foreach(var team in participants.GroupBy(x=>match.Arena?x.P.I("playerSubteamId"):x.P.I("teamId")))
        {
            panel.Children.Add(Ui.Text(match.Arena?$"Takım {team.Key}":team.Key==100?"Mavi takım":"Kırmızı takım",16,null,true));panel.Children.Add(Ui.Space(10));
            var rows=team.Select(r=>new{Oyuncu=MatchIdentity.Name(r.P),Participant=r.P,Şampiyon=r.Champion,KDA=r.Kda,Hasar=r.Damage,Altın=r.P.N("goldEarned").ToString("N0"),CS=r.Cs,Görüş=r.Vision,Items=r.Items,IsYou=r.P.S("puuid")==match.P.S("puuid")}).ToList();
            var table=Ui.Table(rows,("Oyuncu","Oyuncu",1.4),("Şampiyon","Şampiyon",1.1),("K / D / A","KDA",1),("CS · /dk","CS",1),("Hasar","Hasar",.8),("Altın","Altın",.8),("Görüş","Görüş",.6));Ui.ChampionColumn(table,data,"Şampiyon",1);Ui.InventoryColumn(table,data,"Items");table.RowHeight=64;PlayerColumn(table);
            var rowStyle=new Style(typeof(DataGridRow));var own=new DataTrigger{Binding=new System.Windows.Data.Binding("IsYou"),Value=true};own.Setters.Add(new Setter(DataGridRow.BackgroundProperty,ThemePalette.Resource("#253246")));rowStyle.Triggers.Add(own);table.RowStyle=rowStyle;
            panel.Children.Add(table);panel.Children.Add(Ui.Space(16));
        }
        foreach(var t in match.Info.A("teams"))panel.Children.Add(Ui.Text($"Takım {t.I("teamId")}: "+string.Join(" · ",(t["objectives"] as JsonObject??new()).Select(x=>$"{x.Key}: {x.Value.I("kills")}")),12,Ui.Muted));
        return panel;
    }
    internal FrameworkElement PlayerButton(JsonNode participant)
    {
        var p=MatchIdentity.Player(participant,region.Platform);
        var name=Ui.Text(MatchIdentity.Name(participant),12,null,true);name.VerticalAlignment=VerticalAlignment.Center;
        var button=Ui.Button("",()=>openProfile?.Invoke(p.RiotId,participant.S("championName")));button.Content=Ui.Row(Ui.ProfilePortrait(data,p,36),name);button.Padding=new Thickness(4);button.Margin=new Thickness(0);button.Background=Brushes.Transparent;button.HorizontalContentAlignment=HorizontalAlignment.Left;
        button.IsEnabled=openProfile!=null&&p.GameName.Length>0&&p.TagLine.Length>0;button.ToolTip=button.IsEnabled?"Profili aç · rank, Winrate ve ustalık":"Bu maçta oyuncunun tam Riot ID’si paylaşılmamış.";return button;
    }
    void PlayerColumn(DataGrid table)
    {
        var content=new FrameworkElementFactory(typeof(ContentControl));content.SetBinding(ContentControl.ContentProperty,new System.Windows.Data.Binding("Participant"){Converter=new MatchPlayerConverter(PlayerButton)});
        table.Columns[0]=new DataGridTemplateColumn{Header="Oyuncu",Width=new DataGridLength(2,DataGridLengthUnitType.Star),MinWidth=200,CellTemplate=new DataTemplate{VisualTree=content},SortMemberPath="Oyuncu"};
    }
    UIElement Loadout()
    {
        var panel=new StackPanel();
        foreach(var r in match.Info.A("participants").Select(p=>new MatchRow{Raw=match.Raw,P=p}))
        {
            var items=Ui.ItemStrip(data,Enumerable.Range(0,7).Select(i=>r.P.I("item"+i)),true,35);
            var runes=r.P["perks"].A("styles").SelectMany(x=>x.A("selections")).Select(x=>data.RuneName(x.I("perk")));
            var augmentIds=Enumerable.Range(1,6).Select(i=>r.P.I("playerAugment"+i)).Where(x=>x>0);
            panel.Children.Add(Ui.Card(Ui.Stack(Ui.Row(PlayerButton(r.P),Ui.Portrait(data,r.Champion,32),Ui.Text(r.Champion,16,null,true)),Ui.Space(8),items,Ui.Space(8),Ui.Text("Rünler: "+string.Join(" · ",runes),12,Ui.Muted),Ui.Text(augmentIds.Any()?"API'deki augment kimlikleri: "+string.Join(", ",augmentIds):"",11,Ui.Muted))));
        }
        panel.Children.Add(Ui.Text("İsimler güncel Data Dragon kataloğundan çözülür; eski yamadaki eşya ve rün etkileri değişmiş olabilir. Augment kimliği sunulmayan modlarda augment çıkarımı yapılmaz.",11,Ui.Muted));return panel;
    }
    UIElement AllMetrics()
    {
        var participants=match.Info.A("participants").ToList();var select=new ComboBox{ItemsSource=participants.Select(p=>MatchIdentity.Name(p)+" · "+p.S("championName")).ToList(),SelectedIndex=participants.FindIndex(p=>p.S("puuid")==match.P.S("puuid"))};
        var search=new TextBox{ToolTip="Riot metrik adını ara",Margin=new Thickness(0,10,0,10)};var table=Ui.Table(Array.Empty<Metric>(),("Riot alan adı","Alan",2),("Değer","Değer",1));table.MaxHeight=500;
        void Fill(){if(select.SelectedIndex<0)return;table.ItemsSource=Analytics.Flatten(participants[select.SelectedIndex]).OfType<Metric>().Where(m=>m.Alan.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToList();}
        select.SelectionChanged+=(_,_)=>Fill();search.TextChanged+=(_,_)=>Fill();Fill();
        return Ui.Stack(Ui.Text("Riot'un döndürdüğü tüm oyuncu metrikleri. Alan adları özgün API adlarıdır; modda bulunmayan alanlar listelenmez.",12,Ui.Muted),Ui.Space(),select,search,table);
    }
    UIElement Timeline()
    {
        var output=new StackPanel();var note=Ui.Text("Maç bitişi sonrası zaman çizelgesi: takım altın farkı, dakika dakika kendi CS/altın/seviyen ve olaylar. Ayrı API isteği yalnızca yükleye bastığında yapılır.",12,Ui.Muted);
        var button=new Button{Content="Zaman çizelgesini yükle",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,16)};
        button.Click+=async(_,_)=>
        {
            if(demo){note.Text="Örnek maçların zaman çizelgesi yok. Gerçek bir maçta API üzerinden yüklenir.";return;}
            button.IsEnabled=false;
            try
            {
                var timeline=await api.Timeline(match.Id,region,lifetime.Token);if(timeline==null)return;
                var frames=timeline["info"].A("frames").ToList();var pid=match.P.I("participantId").ToString();
                var teams=match.Info.A("participants").ToDictionary(p=>p.I("participantId").ToString(),p=>p.I("teamId"));
                var values=frames.Select(f=>
                {
                    var player=f["participantFrames"]?[pid];double goldDiff=0;
                    if(f["participantFrames"] is JsonObject pfs)goldDiff=pfs.Sum(p=>teams.GetValueOrDefault(p.Key)==100?p.Value.N("totalGold"):-p.Value.N("totalGold"));
                    return new {Dakika=$"{f.N("timestamp")/60000:0}",Altın=player.N("totalGold").ToString("N0"),CS=player.N("minionsKilled")+player.N("jungleMinionsKilled"),Seviye=player.I("level"),MaviFark=goldDiff};
                }).ToList();
                output.Children.Clear();
                if(!match.Arena)
                {
                    output.Children.Add(Ui.Text("Mavi takımın altın farkı",16,null,true));output.Children.Add(Ui.Space());
                    double max=values.Count>0?Math.Max(1,values.Max(x=>Math.Abs(x.MaviFark))):1;
                    foreach(var v in values.Where((_,i)=>i%3==0))output.Children.Add(Ui.Bar(v.Dakika+". dakika",$"{v.MaviFark:+#,0;-#,0;0}",Math.Abs(v.MaviFark)/max*100,v.MaviFark>=0?Ui.Accent:Ui.Red));
                }
                output.Children.Add(Ui.Table(values,("Dakika","Dakika",.7),("Altının","Altın",1),("CS","CS",.7),("Seviye","Seviye",.7),("Mavi altın farkı","MaviFark",1.2)));output.Children.Add(Ui.Space());
                var events=frames.SelectMany(f=>f.A("events")).Select(e=>new{Zaman=TimeSpan.FromMilliseconds(e.N("timestamp")).ToString(@"mm\:ss"),Olay=e.S("type"),Oyuncu=e.S("participantId",e.S("killerId","—")),ItemId=e.I("itemId"),Detay=e.I("itemId")>0?data.ItemName(e.I("itemId")):e.S("monsterType",e.S("buildingType"))}).ToList();
                var eventTable=Ui.Table(events,("Zaman","Zaman",.7),("Olay","Olay",1.7),("Oyuncu ID","Oyuncu",.7),("Detay / eşya","Detay",1.5));Ui.ItemColumn(eventTable,data,"ItemId","Detay",3);output.Children.Add(eventTable);note.Text=$"{frames.Count} dakika kaydı · {events.Count} olay.";
            }
            catch(Exception ex){if(!lifetime.IsCancellationRequested)note.Text=SafeError.Message(ex);}
            finally{button.IsEnabled=true;}
        };
        return Ui.Stack(note,button,output);
    }
}
