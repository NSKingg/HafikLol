using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RiftRadar;

public static class SelfTests
{
    public static async Task<string> Run()
    {
        var results=new List<object>();int failed=0;
        async Task Test(string name,Func<Task> test){try{await test();results.Add(new{name,pass=true});}catch(Exception ex){failed++;results.Add(new{name,pass=false,error=ex.GetType().Name+": "+ex.Message});}}
        static void Assert(bool condition,string message="Assertion failed"){if(!condition)throw new Exception(message);}
        var root=Path.Combine(AppContext.BaseDirectory,"RiftRadar-tests-"+Guid.NewGuid().ToString("N"));
        try
        {
            await Test("Tray close hides, restores and explicit Exit fully closes",async()=>
            {
                var app=(App)System.Windows.Application.Current;var previous=app.IsExiting;MainWindow? window=null;bool ended=false;
                try
                {
                    var saved=new Store(Path.Combine(root,"tray"));window=new MainWindow(saved,true,false,true);window.Closed+=(_,_)=>ended=true;window.Show();await System.Windows.Threading.Dispatcher.Yield();
                    window.Close();Assert(!ended&&!window.IsVisible&&!window.ShowInTaskbar,"Close did not hide to tray");
                    window.RestoreFromTray();Assert(window.IsVisible&&window.ShowInTaskbar&&window.WindowState==System.Windows.WindowState.Normal);
                    var closed=new TaskCompletionSource();window.Closed+=(_,_)=>closed.TrySetResult();window.ExitFromTray();Assert(await Task.WhenAny(closed.Task,Task.Delay(3000))==closed.Task,"Tray Exit did not close");
                }
                finally{if(window!=null&&!ended)window.ExitFromTray();app.IsExiting=previous;}
            });
            await Test("Disabling close to tray persists and closes normally",async()=>
            {
                var app=(App)System.Windows.Application.Current;var previous=app.IsExiting;
                try
                {
                    var saved=new Store(Path.Combine(root,"tray-disabled"));saved.Preferences.CloseToTray=false;saved.Save();Assert(!new Store(saved.Root).Preferences.CloseToTray);
                    var window=new MainWindow(saved,true,false,true);var closed=new TaskCompletionSource();window.Closed+=(_,_)=>closed.TrySetResult();window.Show();await System.Windows.Threading.Dispatcher.Yield();window.Close();Assert(await Task.WhenAny(closed.Task,Task.Delay(3000))==closed.Task);
                }
                finally{app.IsExiting=previous;}
            });
            await Test("Theme updates existing surfaces and saved preferences without changing dark colors",()=>
            {
                var original=ThemePalette.Light;
                try{ThemePalette.Apply(false);var panel=ThemePalette.Brush("#1B1E24");var text=ThemePalette.Brush("#ECECEF");Assert(panel.Color==System.Windows.Media.Color.FromRgb(27,30,36));ThemePalette.Apply(true);Assert(panel.Color==System.Windows.Media.Colors.White&&text.Color==System.Windows.Media.Color.FromRgb(32,40,56));var themeStore=new Store(Path.Combine(root,"theme"));themeStore.Preferences.LightMode=true;themeStore.Save();Assert(new Store(themeStore.Root).Preferences.LightMode);ThemePalette.Apply(false);Assert(panel.Color==System.Windows.Media.Color.FromRgb(27,30,36));}
                finally{ThemePalette.Apply(original);}return Task.CompletedTask;
            });
            await Test("Match identities preserve tags, profile icons and in-app profile clicks",()=>
            {
                var participant=new JsonObject{["riotIdGameName"]="Aynı İsim",["riotIdTagline"]="TR1",["profileIcon"]=123,["championName"]="Ahri"};
                Assert(MatchIdentity.Name(participant)=="Aynı İsim#TR1");var player=MatchIdentity.Player(participant,"tr1");Assert(player.Icon==123&&player.RiotId=="Aynı İsim#TR1");
                var fixture=new Demo();var testStore=new Store(Path.Combine(root,"match-identity"));using var api=new RiotApi(testStore);string opened="";
                using var view=new MatchView(Analytics.For(fixture.Matches,fixture.Players[0])[0],new StaticData(testStore),api,Region.All[0],true,()=>{},(id,champion)=>opened=id+":"+champion);
                var button=(System.Windows.Controls.Button)view.PlayerButton(participant);Assert(button.IsEnabled);button.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Assert(opened=="Aynı İsim#TR1:Ahri");
                participant.Remove("riotIdTagline");Assert(MatchIdentity.Name(participant)=="Aynı İsim"&&!((System.Windows.Controls.Button)view.PlayerButton(participant)).IsEnabled);
                participant.Remove("profileIcon");Assert(MatchIdentity.Player(participant,"tr1").Icon==-1);return Task.CompletedTask;
            });
            await Test("Original rank emblems cover all tiers and selected queue",()=>
            {
                foreach(var tier in "IRON BRONZE SILVER GOLD PLATINUM EMERALD DIAMOND MASTER GRANDMASTER CHALLENGER UNRANKED".Split(' ')){var rank=new JsonObject{["tier"]=tier,["rank"]="IV",["leaguePoints"]=55};Assert(RankVisuals.Image(rank).IsFrozen&&RankVisuals.Color(rank)!=null);Assert(RankVisuals.Label(rank).Contains(tier=="UNRANKED"?"Derecesiz":"55 LP"));}
                var player=new Player{Ranks=new JsonArray(new JsonObject{["queueType"]="RANKED_FLEX_SR",["tier"]="GOLD",["rank"]="II",["leaguePoints"]=42})};Assert(RankVisuals.Entry(player)==null);Assert(RankVisuals.Label(RankVisuals.Entry(player,"RANKED_FLEX_SR"))=="Altın II · 42 LP");return Task.CompletedTask;
            });
            await Test("Leaderboard ranks use the selected queue and sort tiers before LP",()=>
            {
                var one=new Player{GameName="Master",Ranks=new JsonArray(new JsonObject{["queueType"]="RANKED_SOLO_5x5",["tier"]="MASTER",["rank"]="I",["leaguePoints"]=2000})};
                var two=new Player{GameName="Grandmaster",Ranks=new JsonArray(new JsonObject{["queueType"]="RANKED_SOLO_5x5",["tier"]="GRANDMASTER",["rank"]="I",["leaguePoints"]=10})};
                var entries=Comparison.Sort(new[]{Comparison.Entry(one,[],Modes.All[0],"RANKED_SOLO_5x5"),Comparison.Entry(two,[],Modes.All[0],"RANKED_SOLO_5x5")},"Lig");
                Assert(entries[0].Player==two&&entries[0].Position==1);
                var flex=Comparison.Entry(one,[],Modes.All[0],"RANKED_FLEX_SR");Assert(flex.Rank=="Derecesiz"&&flex.LP==null&&flex.KDA==null);
                return Task.CompletedTask;
            });
            await Test("Leaderboard stats use the same recent sample and missing challenges stay missing",()=>
            {
                var fixture=new Demo();var entry=Comparison.Entry(fixture.Players[0],fixture.Matches,Modes.All[0],"RANKED_SOLO_5x5");Assert(entry.Games==20&&entry.KDA>0);
                var entries=Comparison.Sort(new[]{new LeaderEntry{Player=new Player{GameName="Empty"}},new LeaderEntry{Player=new Player{GameName="Played"},KDA=0,Games=1}},"KDA");Assert(entries[0].Name=="Played#");
                var metrics=Comparison.ExtraMetrics(Analytics.For(fixture.Matches,fixture.Players[0]));Assert(metrics.Count==7&&metrics.All(m=>!m.Alan.Contains("skillshots")&&!m.Alan.Contains("soloKills")));
                Assert(metrics.Single(m=>m.Alan=="Yerleştirilen kontrol totemi").Değer=="—");return Task.CompletedTask;
            });
            await Test("Automatic live navigation happens once per game and ignores brief connection loss",()=>
            {
                var tracker=new LiveSessionTracker();JsonNode Game(double time)=>new JsonObject{["gameTime"]=time,["gameMode"]="CLASSIC"};
                Assert(!tracker.Observe(null));Assert(tracker.Observe(Game(100)));
                Assert(!tracker.Observe(Game(105)));Assert(!tracker.Observe(null));Assert(!tracker.Observe(null));Assert(!tracker.Observe(Game(120)));
                Assert(tracker.Observe(Game(0)),"New game clock did not trigger navigation");
                Assert(!tracker.Observe(Game(5)));tracker.Observe(null);tracker.Observe(null);tracker.Observe(null);Assert(tracker.Observe(Game(10)));
                tracker.Reset();Assert(tracker.Observe(Game(200)));return Task.CompletedTask;
            });
            await Test("Premade hints require distinct recent shared teams and respect Arena subteams",()=>
            {
                var fixture=new Demo();var a=fixture.Players[0];var b=fixture.Players[1];
                Assert(LiveComparison.SharedGames(fixture.Matches,a.Puuid,b.Puuid)>=2);
                Assert(LiveComparison.SharedGames(new[]{fixture.Matches[0],fixture.Matches[0]},a.Puuid,b.Puuid)==1,"Duplicate matches counted twice");
                Assert(LiveComparison.SharedGames(fixture.Matches,a.Puuid,"DEMO_ENEMY_5")==0,"Opponents counted as premades");
                Assert(LiveComparison.SharedGames(fixture.Matches.Where(Modes.IsArena),a.Puuid,fixture.Players[2].Puuid)==0,"Different Arena teams counted together");
                var old=fixture.Matches[0].DeepClone();old["info"]!["gameCreation"]=DateTimeOffset.UtcNow.AddDays(-40).ToUnixTimeMilliseconds();Assert(LiveComparison.SharedGames(new[]{old},a.Puuid,b.Puuid)==0);
                var profiles=new Dictionary<string,LiveProfile>{{a.RiotId,new LiveProfile{Player=a,Matches=fixture.Matches}},{b.RiotId,new LiveProfile{Player=b,Matches=fixture.Matches}}};
                Assert(LiveComparison.PremadeLabel(a.RiotId,"ORDER",new[]{(a.RiotId,"ORDER"),(b.RiotId,"ORDER")},profiles).StartsWith("Olası pre"));
                Assert(!LiveComparison.PremadeLabel(a.RiotId,"ORDER",new[]{(a.RiotId,"ORDER"),(b.RiotId,"CHAOS")},profiles).StartsWith("Olası pre"));return Task.CompletedTask;
            });
            await Test("Player preview opens inside the app, closes cleanly and never adds opponents to saved players",async()=>
            {
                var app=(App)System.Windows.Application.Current;var previous=app.IsExiting;
                var saved=new Store(Path.Combine(root,"preview"));var window=new MainWindow(saved,true,false,false);
                try
                {
                    window.Show();await System.Windows.Threading.Dispatcher.Yield();var before=System.Windows.Application.Current.Windows.Count;
                    window.OpenProfilePreview("Efe#ÖRNEK","Ahri");await System.Windows.Threading.Dispatcher.Yield();
                    Assert(window.ProfileOverlay.Visibility==System.Windows.Visibility.Visible&&window.ProfileOverlay.Children.Count==1);
                    Assert(System.Windows.Application.Current.Windows.Count==before,"Separate profile window opened");Assert(saved.Players.Count==0,"Opponent was saved automatically");
                    window.CloseProfilePreview();Assert(window.ProfileOverlay.Visibility==System.Windows.Visibility.Collapsed&&window.ProfileOverlay.Children.Count==0);
                    window.Navigate("champions");window.UpdateLayout();Assert(window.Body.Children.Count>0);window.Navigate("overview");window.UpdateLayout();
                    var closed=new TaskCompletionSource();window.Closed+=(_,_)=>closed.TrySetResult();window.Close();Assert(await Task.WhenAny(closed.Task,Task.Delay(3000))==closed.Task);
                }
                finally{app.IsExiting=previous;}
            });
            await Test("Live roster uses two native rows of five and keeps its preview inside the same window",async()=>
            {
                var app=(App)System.Windows.Application.Current;var previous=app.IsExiting;
                var window=new MainWindow(new Store(Path.Combine(root,"live-roster-ui")),true,true,false);
                try
                {
                    window.Show();await System.Windows.Threading.Dispatcher.Yield();window.Navigate("live");window.UpdateLayout();
                    IEnumerable<T> Descendants<T>(System.Windows.DependencyObject node) where T:System.Windows.DependencyObject
                    {for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);i++){var child=System.Windows.Media.VisualTreeHelper.GetChild(node,i);if(child is T found)yield return found;foreach(var nested in Descendants<T>(child))yield return nested;}}
                    var teams=Descendants<System.Windows.Controls.Primitives.UniformGrid>(window.LiveHost).ToList();Assert(teams.Count==2&&teams.All(team=>team.Columns==5&&team.Children.Count==5));
                    Assert(window.ContentScroll.Visibility==System.Windows.Visibility.Collapsed&&window.AppFooter.Visibility==System.Windows.Visibility.Collapsed);
                    Assert(!Descendants<System.Windows.Controls.ScrollViewer>(window.LiveHost).Any(),"Live screen still scrolls");
                    foreach(var card in teams.SelectMany(team=>team.Children.OfType<System.Windows.Controls.Button>()))
                    {var point=card.TranslatePoint(new System.Windows.Point(),window.LiveHost);Assert(point.Y>=0&&point.Y+card.ActualHeight<=window.LiveHost.ActualHeight+1,"Player card outside viewport");}
                    window.Height=720;window.Width=1080;window.UpdateLayout();
                    foreach(var card in teams.SelectMany(team=>team.Children.OfType<System.Windows.Controls.Button>()))
                    {var point=card.TranslatePoint(new System.Windows.Point(),window.LiveHost);Assert(point.Y>=0&&point.Y+card.ActualHeight<=window.LiveHost.ActualHeight+1,"Small window clips player cards");}
                    window.Height=850;window.Width=1280;window.UpdateLayout();
                    void Capture(string name)
                    {
                        window.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(window);
                        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(AppContext.BaseDirectory,name));encoder.Save(file);
                    }
                    if(Environment.GetEnvironmentVariable("HAFIKLOL_UI_QA")=="1")
                    {
                        await Task.Delay(2500);Capture("live-roster-qa.png");window.OpenProfilePreview("Efe#ÖRNEK","Ahri");await Task.Delay(250);Capture("profile-preview-qa.png");window.CloseProfilePreview();
                        window.Navigate("champions");await Task.Delay(250);Capture("champions-qa.png");window.Navigate("overview");await Task.Delay(250);Capture("overview-mastery-qa.png");window.ContentScroll.ScrollToVerticalOffset(400);window.UpdateLayout();Capture("overview-form-qa.png");
                        window.Navigate("friends");await Task.Delay(250);Capture("leaderboard-qa.png");window.SetLightTheme(true);await Task.Delay(350);Capture("light-theme-qa.png");window.SetLightTheme(false);window.Navigate("insights");await Task.Delay(250);window.ContentScroll.ScrollToVerticalOffset(380);window.UpdateLayout();Capture("insights-role-qa.png");window.ContentScroll.ScrollToVerticalOffset(850);window.UpdateLayout();Capture("insights-contribution-qa.png");
                    }
                    var closed=new TaskCompletionSource();window.Closed+=(_,_)=>closed.TrySetResult();window.Close();Assert(await Task.WhenAny(closed.Task,Task.Delay(3000))==closed.Task);
                }
                finally{app.IsExiting=previous;}
            });
            await Test("Original mastery and role assets load offline and missing mastery remains hidden",()=>
            {
                foreach(var level in new[]{1,3,4,5,6,7,8,9,10,12,100})Assert(GameVisuals.Mastery(level).IsFrozen);
                Assert(GameVisuals.MasteryAssetLevel(12)==10&&GameVisuals.MasteryAssetLevel(3)==0);
                Assert(Ui.MasteryIcon(null)==null&&Ui.MasteryIcon(new JsonObject{["championLevel"]=0})==null);
                foreach(var role in new[]{"TOP","JUNGLE","MIDDLE","BOTTOM","UTILITY"})Assert(GameVisuals.RoleIcon(role).IsFrozen);
                Assert(GameVisuals.RoleName("UTILITY")=="Destek"&&GameVisuals.RoleName("MIDDLE")=="Orta koridor");return Task.CompletedTask;
            });
            await Test("Champion sorting uses numeric mastery and win rates with missing mastery last",()=>
            {
                var fixture=new Demo();var p=fixture.Players[0];p.Mastery.Add(new JsonObject{["championId"]=222,["championLevel"]=4,["championPoints"]=1000});var data=new StaticData(new Store(Path.Combine(root,"champion-sort")));var rows=Analytics.For(fixture.Matches,p);
                var mastery=ChampionSort.Order(rows,p,data,"Ustalık puanı").Select(g=>g.Key).ToList();Assert(mastery[0]=="Ahri"&&mastery[1]=="Jinx");
                var wins=ChampionSort.Order(rows,p,data,"Kazanma oranı").Select(g=>g.Key).ToList();Assert(wins.IndexOf("Jinx")<wins.IndexOf("Ahri"));
                var levels=ChampionSort.Order(rows,p,data,"Ustalık seviyesi");Assert(levels[0].Key=="Ahri");return Task.CompletedTask;
            });
            await Test("Role summaries separate modes, exclude remakes and contribution distinguishes missing from zero",()=>
            {
                var fixture=new Demo();var rows=Analytics.For(fixture.Matches,fixture.Players[0]);var summary=InsightStats.Roles(rows);
                Assert(summary.Any(r=>r.Rol=="ARAM")&&summary.Any(r=>r.Rol=="ARENA")&&summary.Sum(r=>r.Maç)==rows.Count);
                var remake=fixture.Matches[0].DeepClone();remake["info"]!["gameDuration"]=100;var added=Analytics.For(fixture.Matches.Concat(new[]{remake}),fixture.Players[0]);Assert(InsightStats.Roles(added).Sum(r=>r.Maç)==rows.Count);
                var contribution=InsightStats.Contribution(rows);Assert(contribution.Single(r=>r.Alan=="Alınan kontrol totemi").Değer!="—");
                Assert(InsightStats.Contribution(new[]{new MatchRow{Raw=fixture.Matches[0],P=new JsonObject{["puuid"]="empty"}}}).All(r=>r.Değer=="—"));return Task.CompletedTask;
            });
            await Test("Riot ID parsing, URL encoding, route allowlist",()=>{var id=RiotApi.SplitId("Şafak Oyuncu#TR1");Assert(id.Name=="Şafak Oyuncu"&&id.Tag=="TR1");Assert(RiotApi.Url("tr1","/lol/test").Host=="tr1.api.riotgames.com");bool rejected=false;try{RiotApi.Url("evil.example","/lol/test");}catch(ArgumentException){rejected=true;}Assert(rejected);return Task.CompletedTask;});
            var store=new Store(root);
            await Test("Searched profiles persist across restart, rename, and repeat searches",()=>
            {
                var folder=Path.Combine(root,"searches");var saved=new Store(folder);
                var p=new Player{Puuid="saved-player",GameName="Original",TagLine="TR1",Icon=685,Note="Notum",MatchIds=["TR1_old"]};saved.RememberPlayer(p);
                var changed=new Player{Puuid=p.Puuid,GameName="Renamed",TagLine="NSK",Icon=700,MatchIds=["TR1_new"]};saved.RememberPlayer(changed);
                var restarted=new Store(folder);Assert(restarted.Players.Count==1&&restarted.Players[0].GameName=="Renamed"&&restarted.Players[0].Icon==700);
                Assert(restarted.Players[0].Note=="Notum"&&restarted.Players[0].MatchIds.Count==2&&restarted.Preferences.Selected==p.Key);return Task.CompletedTask;
            });
            await Test("Server address validation rejects insecure URLs and Riot keys as invitations",()=>{RemoteApi.Validate("https://private.example/",new string('a',43));foreach(var url in new[]{"http://private.example","https://user:pass@private.example","https://private.example/x","https://private.example/?invite=secret","https://127.0.0.1"}){bool rejected=false;try{RemoteApi.Validate(url,new string('a',43));}catch(ApiException){rejected=true;}Assert(rejected);}bool keyRejected=false;try{RemoteApi.Validate("https://private.example","RGAPI-"+new string('a',40));}catch(ApiException){keyRejected=true;}Assert(keyRejected);return Task.CompletedTask;});
            await Test("Remote connection uses bearer invitation, caches data, and encrypts persistence",async()=>{var handler=new RemoteFakeHandler();using var remote=new RemoteApi(store,"https://private.example/",new string('a',43),handler);Assert(await remote.Check(CancellationToken.None));remote.Save();Assert(!File.ReadAllText(Path.Combine(root,"server.dpapi")).Contains(new string('a',43)));var restartedHandler=new RemoteFakeHandler();using var restored=new RemoteApi(new Store(root),handler:restartedHandler);Assert(restored.Address=="https://private.example/"&&await restored.Check(CancellationToken.None));Assert(restartedHandler.Auth=="Bearer "+new string('a',43));await remote.Get("tr1","/lol/status/v4/platform-data","status",TimeSpan.FromMinutes(1),CancellationToken.None);await remote.Get("tr1","/lol/status/v4/platform-data","status",TimeSpan.FromMinutes(1),CancellationToken.None);Assert(handler.Calls==2&&remote.CacheHits==1&&handler.Auth=="Bearer "+new string('a',43));remote.Forget();Assert(!File.Exists(Path.Combine(root,"server.dpapi")));});
            await Test("Windows DPAPI roundtrip and encrypted persistence",()=>{const string fake="RGAPI-test-only-not-a-real-key";store.SaveKey(fake,true);Assert(store.LoadKey()==fake);Assert(!File.ReadAllText(Path.Combine(root,"key.dpapi")).Contains(fake));store.SaveKey("",false);Assert(store.LoadKey()=="");return Task.CompletedTask;});
            await Test("Cache persistence, TTL, corrupt data and pruning",()=>{store.Cache("x","{\"ok\":true}");Assert(store.Cached("x",TimeSpan.FromHours(1))!=null);Assert(store.Cached("x",TimeSpan.FromTicks(-1))==null);File.WriteAllText(Directory.GetFiles(store.CacheDir)[0],"broken");Assert(store.Cached("x",TimeSpan.FromHours(1))==null);return Task.CompletedTask;});
            await Test("Mode isolation, Arena placements, custom match exclusion",()=>{var demo=new Demo();var aram=Analytics.For(demo.Matches,demo.Players[0],"ARAM");Assert(aram.Count==6&&aram.All(x=>x.Queue==450));var mayhem=Analytics.For(demo.Matches,demo.Players[0],"ARAM Mayhem");Assert(mayhem.Count==6&&mayhem.All(x=>x.Queue==2400));var arena=Analytics.For(demo.Matches,demo.Players[0],"Arena");Assert(arena.Count==6&&arena.All(x=>x.Arena));var m=demo.Matches[0].DeepClone();m["info"]!["queueId"]=0;Assert(!Modes.PublicMatch(m));store.SaveMatch(m);Assert(store.Cached("match:"+m["metadata"].S("matchId"),TimeSpan.FromDays(1))==null);return Task.CompletedTask;});
            await Test("Stats, zero deaths, participation and CSV formula safety",()=>{var d=new Demo();var r=Analytics.For(d.Matches,d.Players[0])[0];r.P["deaths"]=0;r.P["championName"]="=CMD()";Assert(double.IsFinite(r.KdaValue));Assert(r.Kp>=0&&r.Kp<=100);Assert(Analytics.Csv([r]).Contains("'=CMD()"));return Task.CompletedTask;});
            await Test("Rate gate conservative rolling window",()=>{var gate=new RateGate();var now=DateTimeOffset.UtcNow;for(int i=0;i<90;i++)gate.Record(now.AddSeconds(-30));Assert(gate.Delay("tr1","match",now).TotalSeconds>89);Assert(gate.Delay("tr1","match",now.AddSeconds(121))==TimeSpan.Zero);return Task.CompletedTask;});
            await Test("API header, cache hit and JSON parse",async()=>{var h=new FakeHandler((_,_)=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"puuid\":\"test\"}")});using var api=new RiotApi(store,h){ApiKey="RGAPI-fake"};var a=await api.Account("Oyuncu#TR1",Region.All[0],CancellationToken.None);await api.Account("Oyuncu#TR1",Region.All[0],CancellationToken.None);Assert(a.S("puuid")=="test"&&h.Calls==1&&api.CacheHits==1);Assert(h.Token=="RGAPI-fake");});
            await Test("403 fails once and never retries",async()=>{var h=new FakeHandler((_,_)=>new HttpResponseMessage(HttpStatusCode.Forbidden));using var api=new RiotApi(store,h){ApiKey="RGAPI-fake"};try{await api.Status(Region.All[0],CancellationToken.None);throw new Exception("Expected 403");}catch(ApiException ex){Assert(ex.Code==403&&h.Calls==1);}});
            await Test("Spectator 404 is a normal no-game result",async()=>{var h=new FakeHandler((_,_)=>new HttpResponseMessage(HttpStatusCode.NotFound));using var api=new RiotApi(store,h){ApiKey="RGAPI-fake"};Assert(await api.Spectator("abc",Region.All[0],CancellationToken.None)==null);});
            await Test("429 honors Retry-After; cancellation stops queued request",async()=>{var h=new FakeHandler((_,_)=>{var r=new HttpResponseMessage(HttpStatusCode.TooManyRequests);r.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(12));return r;});using var api=new RiotApi(store,h){ApiKey="RGAPI-fake"};try{await api.Spectator("abc",Region.All[0],CancellationToken.None);}catch(ApiException e){Assert(e.Code==429);}using var ct=new CancellationTokenSource(50);try{await api.Spectator("abc",Region.All[0],ct.Token);}catch(OperationCanceledException){}Assert(h.Calls==1);});
            await Test("500 uses one bounded retry",async()=>{var h=new FakeHandler((_,count)=>new HttpResponseMessage(count==1?HttpStatusCode.InternalServerError:HttpStatusCode.OK){Content=new StringContent("{}")});using var api=new RiotApi(store,h){ApiKey="RGAPI-fake"};await api.Spectator("abc",Region.All[0],CancellationToken.None);Assert(h.Calls==2);});
            await Test("Arena items cannot produce win rates",()=>{var data=new StaticData(store);var d=new Demo();Assert(BuildStats.Items(d.Matches,data,"Ahri","Arena","16.20").Count==0);Assert(data.Items.Count>100&&data.Champions.Count>100);return Task.CompletedTask;});
            await Test("New Arena queues use actual team count and remain excluded from item win rates",()=>{var d=new Demo();var m=d.Matches[0].DeepClone();m["info"]!["queueId"]=1750;m["info"]!["mapId"]=30;m["info"]!["gameMode"]="CHERRY";var template=m["info"]!.A("participants").First().DeepClone();var players=new JsonArray();for(int i=0;i<18;i++){var p=template.DeepClone();p["puuid"]=i==0?d.Players[0].Puuid:"ARENA_TEST_"+i;p["playerSubteamId"]=i/3+1;p["placement"]=3;p["championName"]="Ahri";players.Add(p);}m["info"]!["participants"]=players;var row=Analytics.For([m],d.Players[0],"Arena").Single();Assert(row.Arena&&row.ArenaTeams==6&&row.Win&&row.Mode=="Arena");row.P["placement"]=4;Assert(!row.Win&&row.Result=="#4 sıra");m["info"]!["queueId"]=9999;Assert(Modes.Accept(m,"Arena")&&!Modes.Accept(m,"Diğer"));Assert(BuildStats.Items([m],new StaticData(store),"Ahri","Tüm modlar","16.20").Count==0);return Task.CompletedTask;});
            await Test("Every catalog item has an offline original sprite and safe crop",()=>{var data=new StaticData(store);foreach(var item in data.Items){var image=data.ItemImage(item.Key);Assert(image!=null&&image.IsFrozen&&image.PixelWidth==item.Value!["image"].I("w"),"Missing item icon: "+item.Key);}Assert(data.ItemImage("0")==null&&data.ItemImage("nonexistent")==null);return Task.CompletedTask;});
            await Test("Every champion has a frozen offline portrait including display names and numeric IDs",()=>{var data=new StaticData(store);foreach(var champ in data.Champions){var image=data.ChampionImage(champ.Key);Assert(image!=null&&image.IsFrozen,"Missing champion: "+champ.Key);Assert(ReferenceEquals(image,data.ChampionImage(champ.Value.S("name"))));Assert(ReferenceEquals(image,data.ChampionImage(champ.Value.S("key"))));}Assert(data.ChampionImage("unknown-champion")==null);return Task.CompletedTask;});
            await Test("Item details preserve stat quantities and passive descriptions",()=>{var data=new StaticData(store);var raw=data.Items["3089"]!;var item=new ItemRow("3089",raw);var stats=ItemDetails.Stats(item);Assert(stats.Any(x=>x.Amount=="130"&&x.Name.Contains("Yetenek")),item.StatSummary);Assert(J.Strip(raw.S("description")).Contains("30"));Assert(item.StatSummary.Contains("130"));return Task.CompletedTask;});
            await Test("Match inventories retain all six equipment slots and trinket",()=>{var d=new Demo();var row=Analytics.For(d.Matches,d.Players[0])[0];row.P["item0"]=3089;row.P["item3"]=0;row.P["item6"]=3340;Assert(row.Items.Length==7&&row.Items[0]==3089&&row.Items[3]==0&&row.Items[6]==3340);return Task.CompletedTask;});
            await Test("Stat families preserve AD/AP/health/lifesteal distinctions",()=>{Assert(StatTheme.Resolve("FlatPhysicalDamageMod")==StatTheme.AD);Assert(StatTheme.Resolve("FlatMagicDamageMod")==StatTheme.AP);Assert(StatTheme.Resolve("300 Can")==StatTheme.Health);Assert(StatTheme.Resolve("%10 Can Çalma")==StatTheme.Lifesteal);Assert(StatTheme.Resolve("riotIdGameName",true)==null);Assert(StatTheme.Health.Image.IsFrozen&&StatTheme.AP.Image.IsFrozen);return Task.CompletedTask;});
            await Test("No key means no outbound API request",async()=>{var h=new FakeHandler((_,_)=>new HttpResponseMessage(HttpStatusCode.OK));using var api=new RiotApi(store,h);try{await api.Spectator("missing-key",Region.All[0],CancellationToken.None);throw new Exception("Expected key error");}catch(ApiException e){Assert(e.Code==401&&h.Calls==0);}});
            await Test("Header-defined method limits are respected",()=>{var gate=new RateGate();using var response=new HttpResponseMessage(HttpStatusCode.OK);response.Headers.Add("X-Method-Rate-Limit","1:20");response.Headers.Add("X-Method-Rate-Limit-Count","1:20");gate.Observe(response,"tr1","spectator");Assert(gate.Delay("tr1","spectator",DateTimeOffset.UtcNow).TotalSeconds>19);Assert(gate.Delay("tr1","league",DateTimeOffset.UtcNow)==TimeSpan.Zero);return Task.CompletedTask;});
            await Test("Partial history survives a forbidden later match",async()=>{var d=new Demo();var m=d.Matches[0].DeepClone();m["metadata"]!["matchId"]="TR1_partial-test";var h=new FakeHandler((request,_)=>request.RequestUri!.AbsolutePath.EndsWith("/ids")?new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("[\"TR1_partial-test\",\"TR1_blocked-test\",\"TR1_never-requested\"]")}:request.RequestUri.AbsolutePath.EndsWith("TR1_partial-test")?new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(m.ToJsonString())}:new HttpResponseMessage(HttpStatusCode.Forbidden));using var api=new RiotApi(store,h){ApiKey="RGAPI-fake"};var result=await api.History(d.Players[0],0,20,null,CancellationToken.None);Assert(result.Count==1&&api.LastHistoryCount==1&&api.HistoryWarning.Length>0&&h.Calls==3);});
            await Test("Saved builds survive HTTP cache clearing",()=>{store.SaveBuilds(new JsonArray(new JsonObject{["name"]="Test"}));store.ClearCache();Assert(store.LoadBuilds().Count==1);return Task.CompletedTask;});
            await Test("Metric flattening includes array values and hides PUUID",()=>{var values=Analytics.Flatten(J.Parse("{\"puuid\":\"secret-id\",\"numbers\":[1,2],\"nested\":{\"metric\":3}}")).OfType<Metric>().ToList();Assert(values.Count==3&&values.Any(x=>x.Alan=="numbers.0")&&!values.Any(x=>x.Alan.Contains("puuid")));return Task.CompletedTask;});
            await Test("Live API endpoint allowlist rejects other APIs",async()=>{using var api=new RiotApi(store);bool rejected=false;try{await api.Live("replay/game",CancellationToken.None);}catch(ArgumentException){rejected=true;}Assert(rejected);});
            await Test("Separate backend starts, encrypts key and exits cleanly",async()=>{store.Preferences.UseServer=false;using var api=new BackendClient(store);await api.Start();var pid=api.BackendPid;Assert(pid>0&&!api.HasKey);await api.Configure("RGAPI-test-not-real",true);Assert(api.HasKey);await api.Configure("",false);Assert(!api.HasKey);await api.Stop();bool exited;try{exited=System.Diagnostics.Process.GetProcessById(pid).HasExited;}catch(ArgumentException){exited=true;}Assert(exited);});
            await Test("Native window exits after synchronous server cleanup without reentrant Close",async()=>
            {
                var app=(App)System.Windows.Application.Current;var previous=app.IsExiting;
                try
                {
                    var window=new MainWindow(new Store(Path.Combine(root,"exit-test")),false,true,false);
                    var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    window.Closed+=(_,_)=>closed.TrySetResult();window.Show();window.Close();
                    Assert(await Task.WhenAny(closed.Task,Task.Delay(3000))==closed.Task,"Window did not close");
                }
                finally{app.IsExiting=previous;}
            });
            await Test("Match details stay in the main window and Back preserves the filtered history",async()=>
            {
                var app=(App)System.Windows.Application.Current;var previous=app.IsExiting;
                try
                {
                    var folder=Path.Combine(root,"navigation");var saved=new Store(folder);var d=new Demo();saved.RememberPlayer(d.Players[0]);foreach(var m in d.Matches)saved.SaveMatch(m);
                    var data=new StaticData(saved);var iconDir=Path.Combine(folder,"assets",data.Version,"profileicons");Directory.CreateDirectory(iconDir);
                    using(var image=System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Champions/Ahri.png"))!.Stream)
                    using(var output=File.Create(Path.Combine(iconDir,"0.png")))image.CopyTo(output);
                    var cached=data.ProfileImage(0);Assert(cached!=null&&cached.IsFrozen&&ReferenceEquals(cached,await data.ProfileImageAsync(0)));
                    var window=new MainWindow(saved,false,false,false);window.Show();await System.Windows.Threading.Dispatcher.Yield();window.Navigate("history");window.UpdateLayout();
                    var card=(System.Windows.Controls.Border)window.Body.Children[0];var controls=(System.Windows.Controls.WrapPanel)card.Child;
                    var filter=((System.Windows.Controls.StackPanel)controls.Children[0]).Children.OfType<System.Windows.Controls.TextBox>().Single();filter.Text="Ahri";
                    var before=System.Windows.Application.Current.Windows.Count;var original=window.Body.Children[0];var row=Analytics.For(d.Matches,d.Players[0])[0];
                    window.OpenMatch(row);Assert(window.MatchHost.Content is MatchView&&System.Windows.Application.Current.Windows.Count==before);
                    window.CloseMatch();Assert(window.MatchHost.Content==null&&ReferenceEquals(original,window.Body.Children[0])&&filter.Text=="Ahri");
                    var closed=new TaskCompletionSource();window.Closed+=(_,_)=>closed.TrySetResult();window.Close();Assert(await Task.WhenAny(closed.Task,Task.Delay(3000))==closed.Task);
                }
                finally{app.IsExiting=previous;}
            });
            await Test("Wheel distance handles partial deltas and clamps at edges",()=>
            {
                Assert(ScrollAssist.WheelTarget(100,1000,400,-120,3)==172);
                Assert(ScrollAssist.WheelTarget(100,1000,400,-60,3)==136);
                Assert(ScrollAssist.WheelTarget(990,1000,400,-120,3)==1000);
                Assert(ScrollAssist.WheelTarget(10,1000,400,120,3)==0);
                Assert(ScrollAssist.WheelTarget(100,1000,400,-120,-1)==500);
                Assert(ScrollAssist.WheelTarget(100,1000,400,-120,0)==100);
                return Task.CompletedTask;
            });
            await Test("Continuous wheel motion stays monotonic, reverses cleanly and stops without moving content transforms",()=>
            {
                var content=new System.Windows.Controls.Border{Height=2000};
                var view=new System.Windows.Controls.ScrollViewer{Content=content,VerticalScrollBarVisibility=System.Windows.Controls.ScrollBarVisibility.Auto};
                view.Measure(new System.Windows.Size(400,300));view.Arrange(new System.Windows.Rect(0,0,400,300));view.UpdateLayout();
                ScrollAssist.Move(view,-120);
                double previous=0;
                for(int i=0;i<10;i++){ScrollAssist.Advance(view,.016);view.UpdateLayout();Assert(view.VerticalOffset>=previous&&view.VerticalOffset<=72);previous=view.VerticalOffset;}
                Assert(previous>0,"Wheel did not scroll");
                ScrollAssist.Move(view,-120);ScrollAssist.Advance(view,.016);view.UpdateLayout();Assert(view.VerticalOffset>=previous);
                var beforeReverse=view.VerticalOffset;ScrollAssist.Move(view,120);ScrollAssist.Advance(view,.016);view.UpdateLayout();Assert(view.VerticalOffset<=beforeReverse);
                Assert(content.RenderTransform.Value.IsIdentity,"Content was translated outside its viewport");
                ScrollAssist.Stop(view);var stopped=view.VerticalOffset;Assert(!ScrollAssist.Advance(view,.05));view.UpdateLayout();Assert(view.VerticalOffset==stopped);
                return Task.CompletedTask;
            });
            await Test("Match cards cover the viewport throughout scrolling and direction changes",async()=>
            {
                var fixture=new Demo();var data=new StaticData(store);var rows=Analytics.For(fixture.Matches,fixture.Players[0]);
                var list=Ui.MatchList(rows,data,_=>{},240);
                var testWindow=new System.Windows.Window{Width=1040,Height=300,Content=list,ShowInTaskbar=false};testWindow.Show();await System.Windows.Threading.Dispatcher.Yield();
                try {
                list.Measure(new System.Windows.Size(1000,240));list.Arrange(new System.Windows.Rect(0,0,1000,240));list.UpdateLayout();
                T? Find<T>(System.Windows.DependencyObject node) where T:System.Windows.DependencyObject
                {for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);i++){var child=System.Windows.Media.VisualTreeHelper.GetChild(node,i);if(child is T result)return result;var nested=Find<T>(child);if(nested!=null)return nested;}return null;}
                var viewer=Find<System.Windows.Controls.ScrollViewer>(list)??throw new Exception("Missing list scroll viewer");Assert(viewer.ScrollableHeight>0,$"No scroll extent: {viewer.ExtentHeight}/{viewer.ViewportHeight}");
                Assert(System.Windows.Controls.VirtualizingPanel.GetIsVirtualizing(list));
                foreach(var direction in new[]{-120,-120,-120,120,-120,120})
                {
                    ScrollAssist.Move(viewer,direction);
                    for(int frame=0;frame<12;frame++)
                    {
                        ScrollAssist.Advance(viewer,.016);list.UpdateLayout();
                        var visible=Enumerable.Range(0,list.Items.Count).Select(i=>list.ItemContainerGenerator.ContainerFromIndex(i)).OfType<System.Windows.Controls.ListBoxItem>()
                            .Select(item=>(Top:item.TranslatePoint(new System.Windows.Point(),viewer).Y,Height:item.ActualHeight)).Where(r=>r.Top+r.Height>0&&r.Top<viewer.ViewportHeight).OrderBy(r=>r.Top).ToList();
                        Assert(visible.Count>0,"All match cards disappeared");
                        Assert(visible[0].Top<=8&&visible[^1].Top+visible[^1].Height>=viewer.ViewportHeight-8,"Blank region appeared while scrolling");
                        Assert(list.RenderTransform.Value.IsIdentity,"Match list was visually shifted out of bounds");
                    }
                }
                ScrollAssist.Stop(viewer);
                }finally{testWindow.Close();}
            });
            await Test("Wheel scrolling leaves exhausted inner panes and continues on the outer page",()=>
            {
                var inner=new System.Windows.Controls.ScrollViewer{Height=80,Content=new System.Windows.Controls.Border{Height=20}};
                var stack=new System.Windows.Controls.StackPanel();stack.Children.Add(inner);stack.Children.Add(new System.Windows.Controls.Border{Height=1500});
                var outer=new System.Windows.Controls.ScrollViewer{Content=stack,VerticalScrollBarVisibility=System.Windows.Controls.ScrollBarVisibility.Auto};
                outer.Measure(new System.Windows.Size(400,300));outer.Arrange(new System.Windows.Rect(0,0,400,300));outer.UpdateLayout();
                Assert(!ScrollAssist.CanMove(inner,-120)&&ReferenceEquals(ScrollAssist.OuterTarget(inner,-120),outer));
                outer.ScrollToBottom();outer.UpdateLayout();Assert(ScrollAssist.OuterTarget(inner,-120)==null&&ReferenceEquals(ScrollAssist.OuterTarget(inner,120),outer));return Task.CompletedTask;
            });
        }
        finally{try{Directory.Delete(root,true);}catch{}}
        return JsonSerializer.Serialize(new{passed=results.Count-failed,failed,tests=results},Store.Json);
    }
    sealed class RemoteFakeHandler:HttpMessageHandler
    {
        public int Calls;public string Auth="";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;Auth=request.Headers.Authorization?.ToString()??"";return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(request.Method==HttpMethod.Get?"{\"protocol\":1,\"ready\":true}":"{\"name\":\"TR\"}")});}
    }
    sealed class FakeHandler(Func<HttpRequestMessage,int,HttpResponseMessage> respond):HttpMessageHandler
    {
        public int Calls{get;private set;}public string Token{get;private set;}="";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;Token=request.Headers.GetValues("X-Riot-Token").FirstOrDefault()??"";return Task.FromResult(respond(request,Calls));}
    }
}

