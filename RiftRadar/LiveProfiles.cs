using System.Text.Json.Nodes;

namespace RiftRadar;

public sealed class LiveProfile
{
    public required Player Player {get;init;}
    public List<JsonNode> Matches {get;set;}=[];
    public bool BasicLoaded {get;set;}
    public bool DetailsLoaded {get;set;}
    public int HistoryRequested {get;set;}
    public string Error {get;set;}="";
    public readonly SemaphoreSlim Gate=new(1,1);
}

public static class LiveComparison
{
    public static int SharedGames(IEnumerable<JsonNode> matches,string first,string second)
    {
        if(first.Length==0||second.Length==0||first==second)return 0;
        return matches.Where(Modes.PublicMatch).Where(m=>m["info"].N("gameCreation")>=DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds()).GroupBy(m=>m["metadata"].S("matchId")).Select(g=>g.First()).Count(m=>
        {
            var players=m["info"].A("participants");var a=players.FirstOrDefault(p=>p.S("puuid")==first);var b=players.FirstOrDefault(p=>p.S("puuid")==second);
            return a!=null&&b!=null&&a.I("teamId")>0&&a.I("teamId")==b.I("teamId")&&(!Modes.IsArena(m)||(a.I("playerSubteamId")>0&&a.I("playerSubteamId")==b.I("playerSubteamId")));
        });
    }
    public static string PremadeLabel(string id,string team,IEnumerable<(string Id,string Team)> roster,IReadOnlyDictionary<string,LiveProfile> profiles)
    {
        if(!profiles.TryGetValue(id,out var first)||first.Player.Puuid.Length==0)return "Pre bilgisi bilinmiyor";
        var peers=roster.Where(p=>p.Team==team&&p.Id!=id&&p.Id.Length>0).Where(p=>profiles.TryGetValue(p.Id,out var second)&&SharedGames(first.Matches.Concat(second.Matches),first.Player.Puuid,second.Player.Puuid)>=2).Select(p=>p.Id.Split('#')[0]).ToList();
        return peers.Count>0?"Olası pre · "+string.Join(", ",peers):"Pre kanıtı bulunamadı";
    }
    public static JsonNode? ChampionMastery(Player p,string champion,StaticData data)=>p.Mastery.Rows().FirstOrDefault(m=>m.I("championId")==data.Champion(champion).I("key"));
}
