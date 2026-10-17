using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace RiftRadar;

public partial class MainWindow
{
    readonly Dictionary<string,LiveProfile> liveProfiles=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,Action> liveCardUpdates=new(StringComparer.OrdinalIgnoreCase);
    CancellationTokenSource? rosterWork;
    string rosterKey="";
    List<(string Id,string Team)> visibleRoster=[];
    void CancelRoster(){rosterWork?.Cancel();rosterKey="";liveProfiles.Clear();liveCardUpdates.Clear();CloseProfilePreview();}
    LiveProfile? KnownProfile(string id)
    {
        if(liveProfiles.TryGetValue(id,out var known))return known;
        var saved=(demo?.Players??store.Players).FirstOrDefault(p=>string.Equals(p.RiotId,id,StringComparison.OrdinalIgnoreCase)&&p.Platform==(RegionBox.SelectedItem as Region??Region.All[0]).Platform);
        if(saved==null)return null;
        var copy=new Player{Puuid=saved.Puuid,GameName=saved.GameName,TagLine=saved.TagLine,Platform=saved.Platform,Icon=saved.Icon,Level=saved.Level,Ranks=(JsonArray)saved.Ranks.DeepClone(),Mastery=(JsonArray)saved.Mastery.DeepClone()};
        var profile=new LiveProfile{Player=copy,Matches=demo?.Matches??store.Matches(saved),DetailsLoaded=saved.Mastery.Rows().Any(),BasicLoaded=saved.Ranks.Rows().Any()};liveProfiles[id]=profile;return profile;
    }
    async Task<LiveProfile> LoadLiveProfile(string id,int historyCount,bool details,Region region,CancellationToken ct)
    {
        var profile=KnownProfile(id);
        if(profile==null){var split=RiotApi.SplitId(id);profile=new LiveProfile{Player=new Player{GameName=split.Name,TagLine=split.Tag,Platform=region.Platform}};liveProfiles[id]=profile;}
        await profile.Gate.WaitAsync(ct);
        try
        {
            var p=profile.Player;
            if(demo!=null){var participant=demo.Matches.SelectMany(m=>m["info"].A("participants")).FirstOrDefault(n=>string.Equals(n.S("riotIdGameName")+"#"+n.S("riotIdTagline"),id,StringComparison.OrdinalIgnoreCase));if(participant!=null)p.Puuid=participant.S("puuid");profile.Matches=demo.Matches;profile.BasicLoaded=true;profile.DetailsLoaded=true;profile.HistoryRequested=20;return profile;}
            if(p.Puuid.Length==0){var account=await api.Account(id,region,ct);p.Puuid=account.S("puuid");if(p.Puuid.Length==0)throw new ApiException(404,"Oyuncu bulunamadı.");p.GameName=account.S("gameName",p.GameName);p.TagLine=account.S("tagLine",p.TagLine);}
            if(!profile.BasicLoaded){p.Ranks=(await api.Ranks(p.Puuid,region,ct))?.AsArray()??[];profile.BasicLoaded=true;RefreshLiveCards();}
            if(details&&!profile.DetailsLoaded)
            {
                var summoner=await api.Summoner(p.Puuid,region,ct);p.Icon=summoner.I("profileIconId");p.Level=summoner.I("summonerLevel");
                p.Mastery=(await api.Mastery(p.Puuid,region,ct))?.AsArray()??[];profile.DetailsLoaded=true;
            }
            if(profile.HistoryRequested<historyCount&&profile.Matches.Count<historyCount)
            {
                var ids=await api.Get(region.Cluster,"/lol/match/v5/matches/by-puuid/"+Uri.EscapeDataString(p.Puuid)+$"/ids?start=0&count={historyCount}","matchids",TimeSpan.FromSeconds(60),ct);
                var history=new List<JsonNode>();
                foreach(var matchId in ids.Rows().Select(n=>n.ToString()))
                {
                    ct.ThrowIfCancellationRequested();JsonNode? match=profile.Matches.FirstOrDefault(m=>m["metadata"].S("matchId")==matchId);
                    if(match==null&&store.Cached("match:"+matchId,TimeSpan.FromDays(180)) is {} cached)match=J.Parse(cached);
                    match??=await api.Get(region.Cluster,"/lol/match/v5/matches/"+Uri.EscapeDataString(matchId),"match",TimeSpan.FromDays(180),ct,true);
                    if(match!=null&&Modes.PublicMatch(match)){store.SaveMatch(match);history.Add(match);}
                }
                profile.Matches=history;profile.HistoryRequested=historyCount;
            }
            profile.Error="";return profile;
        }
        catch(Exception ex)when(ex is not OperationCanceledException){profile.Error=SafeError.Message(ex);throw;}
        finally{profile.Gate.Release();}
    }
    void RefreshLiveCards(){foreach(var update in liveCardUpdates.Values.ToList())update();}
    void EnsureRosterProfiles()
    {
        var region=RegionBox.SelectedItem as Region??Region.All[0];
        var key=region.Platform+":"+string.Join("|",visibleRoster.Select(p=>p.Id+":"+p.Team));
        if(key==rosterKey||visibleRoster.Count==0||(!api.HasKey&&demo==null))return;
        if(rosterKey.Length>0&&!rosterKey.StartsWith(region.Platform+":"))liveProfiles.Clear();
        rosterWork?.Cancel();rosterWork?.Dispose();rosterWork=new();rosterKey=key;
        var token=rosterWork.Token;var ids=visibleRoster.Select(p=>p.Id).Where(id=>id.Contains('#')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _=FillRosterProfiles(ids,region,token);
    }
    async Task FillRosterProfiles(List<string> ids,Region region,CancellationToken ct)
    {
        foreach(var id in ids)
        {
            try{await LoadLiveProfile(id,3,false,region,ct);}
            catch(OperationCanceledException){return;}
            catch(ApiException ex)when(ex.Code is 401 or 403 or 429 or 503){RefreshLiveCards();return;}
            catch{ /* Other players remain available when one profile is missing. */ }
            if(ct.IsCancellationRequested||closing)return;RefreshLiveCards();
        }
    }
    FrameworkElement LivePlayerCard(JsonNode player,string team)
    {
        var id=VisibleRiotId(player);var champion=player.S("championName");if(champion.Length==0)champion=data.ChampionName(player.I("championId"));
        var rank=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Left};var premade=Ui.Text("Pre bilgisi bekleniyor…",10,Ui.Muted);
        var top=Ui.Stack(rank);var bottom=Ui.Stack(Ui.Text(data.Champion(champion).S("name",champion),18,null,true),Ui.Text(id.Length>0?id:"Kimlik gizli",12),Ui.Space(6));
        if(player["scores"]!=null)bottom.Children.Add(Ui.Text($"{player["scores"].I("kills")} / {player["scores"].I("deaths")} / {player["scores"].I("assists")} · {player["scores"].I("creepScore")} CS",12,StatTheme.Kills.Color));
        bottom.Children.Add(Ui.Space(6));bottom.Children.Add(premade);
        var grid=new Grid{ClipToBounds=true};
        var artwork=new Image{Source=data.ChampionImage(champion),Stretch=Stretch.UniformToFill,Opacity=.75};grid.Children.Add(artwork);
        grid.Children.Add(new Border{Background=new LinearGradientBrush(new GradientStopCollection{new(Color.FromArgb(235,15,19,27),0),new(Color.FromArgb(35,15,19,27),.4),new(Color.FromArgb(250,15,19,27),1)},90)});
        top.Margin=new Thickness(13);top.VerticalAlignment=VerticalAlignment.Top;grid.Children.Add(top);
        bottom.Margin=new Thickness(13);bottom.VerticalAlignment=VerticalAlignment.Bottom;grid.Children.Add(bottom);
        var button=Ui.Button("",()=>{if(id.Length>0)OpenProfilePreview(id,champion);});button.Content=new Border{Child=grid,CornerRadius=new CornerRadius(10),ClipToBounds=true,BorderBrush=Ui.Brush(team is "ORDER" or "100"?"#3D6EAD":"#85505F"),BorderThickness=new Thickness(1)};button.Padding=new Thickness(0);button.Margin=new Thickness(0,0,8,10);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.VerticalContentAlignment=VerticalAlignment.Stretch;
        button.ToolTip=id.Length>0?"Kısa profil · son maçlar, kazanma oranı ve ustalık":"Bu oyuncunun kimliği oyun tarafından paylaşılmıyor.";button.IsEnabled=id.Length>0;
        artwork.Loaded+=async(_,_)=>{var image=await data.LoadingArt(champion);if(artwork.IsLoaded&&image!=null)artwork.Source=image;};
        void Update()
        {
            var profile=KnownProfile(id);
            rank.Content=profile?.BasicLoaded==true?Ui.RankBadge(profile.Player,size:32):Ui.Text(profile?.Error.Length>0?"Lig alınamadı":api.HasKey?"Lig bilgisi yükleniyor…":"Lig için sunucu bağlantısı gerekli",12,Ui.Accent,true);
            premade.Text=LiveComparison.PremadeLabel(id,team,visibleRoster,liveProfiles);premade.Foreground=premade.Text.StartsWith("Olası")?Ui.Accent:Ui.Muted;
            premade.ToolTip="Son 30 günde en az iki farklı maçta aynı takımda oynama göstergesi. Kesin parti bilgisi değildir; veri bulunmaması solo oynandığı anlamına gelmez.";
        }
        if(id.Length>0)liveCardUpdates[id]=Update;Update();return button;
    }
    FrameworkElement BuildRoster(IEnumerable<JsonNode> players,bool spectatorMode=false)
    {
        var list=players.ToList();string Team(JsonNode p)=>spectatorMode?p.I("teamId").ToString():p.S("team");
        visibleRoster=list.Select(p=>(VisibleRiotId(p),Team(p))).ToList();liveCardUpdates.Clear();
        var layout=new Grid();var groups=list.GroupBy(Team).ToList();
        for(int index=0;index<groups.Count;index++)
        {
            var team=groups[index];layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
            var section=new Grid();section.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});section.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
            var label=Ui.Caption(team.Key is "ORDER" or "100"?"MAVİ TAKIM":team.Key is "CHAOS" or "200"?"KIRMIZI TAKIM":"TAKIM "+team.Key);section.Children.Add(label);
            var cards=new UniformGrid{Columns=Math.Min(5,team.Count())};foreach(var player in team)cards.Children.Add(LivePlayerCard(player,team.Key));Grid.SetRow(cards,1);section.Children.Add(cards);
            Grid.SetRow(section,index);layout.Children.Add(section);
        }
        EnsureRosterProfiles();return layout;
    }
}
