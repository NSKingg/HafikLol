using System.Text.Json.Nodes;

namespace RiftRadar;

public sealed class LeaderEntry
{
    public required Player Player {get;init;}
    public int Position {get;set;}
    public string Name=>Player.RiotId;
    public string Rank {get;init;}="Derecesiz";
    public int RankScore {get;init;}=-1;
    public int? LP {get;init;}
    public int Games {get;init;}
    public double? Success {get;init;}
    public double? KDA {get;init;}
    public double? Damage {get;init;}
    public double? CS {get;init;}
    public double? Vision {get;init;}
    public string Updated=>Player.Updated==default?"Henüz yenilenmedi":Player.Updated.LocalDateTime.ToString("dd MMM HH:mm");
}

public static class Comparison
{
    static readonly string[] tiers=["IRON","BRONZE","SILVER","GOLD","PLATINUM","EMERALD","DIAMOND","MASTER","GRANDMASTER","CHALLENGER"];
    static readonly string[] tierNames=["Demir","Bronz","Gümüş","Altın","Platin","Zümrüt","Elmas","Ustalık","Üstatlık","Şampiyonluk"];
    public static LeaderEntry Entry(Player player,IEnumerable<JsonNode> matches,string mode,string queue)
    {
        var rank=player.Ranks.Rows().FirstOrDefault(x=>x.S("queueType")==queue);
        int tier=Array.IndexOf(tiers,rank.S("tier"));
        int division=rank.S("rank") switch{"I"=>3,"II"=>2,"III"=>1,_=>0};
        var rows=Analytics.For(matches,player,mode).Take(20).Where(r=>!r.Remake).ToList();
        return new LeaderEntry{Player=player,Rank=tier<0?"Derecesiz":tierNames[tier]+(tier<7?" "+rank.S("rank"):""),
            RankScore=tier<0?-1:tier*100000+(tier<7?division*1000:0)+rank.I("leaguePoints"),LP=tier<0?null:rank.I("leaguePoints"),Games=rows.Count,
            Success=rows.Count==0?null:100d*rows.Count(r=>r.Win)/rows.Count,
            KDA=rows.Count==0?null:rows.Sum(r=>r.P.N("kills")+r.P.N("assists"))/Math.Max(1,rows.Sum(r=>r.P.N("deaths"))),
            Damage=rows.Count==0?null:rows.Average(r=>r.Dpm),CS=rows.Count==0?null:rows.Average(r=>r.CsValue/r.Minutes),
            Vision=rows.Count==0?null:rows.Average(r=>r.P.N("visionScore")/r.Minutes)};
    }
    public static List<LeaderEntry> Sort(IEnumerable<LeaderEntry> entries,string metric)
    {
        double? Value(LeaderEntry e)=>metric switch{"KDA"=>e.KDA,"Başarı oranı"=>e.Success,"Hasar / dk"=>e.Damage,"CS / dk"=>e.CS,"Görüş / dk"=>e.Vision,_=>e.RankScore};
        var result=entries.OrderByDescending(e=>Value(e).HasValue).ThenByDescending(Value).ThenByDescending(e=>e.Games).ThenBy(e=>e.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        for(int i=0;i<result.Count;i++)result[i].Position=i+1;return result;
    }
    public static List<Metric> ExtraMetrics(IEnumerable<MatchRow> matches)
    {
        var rows=matches.ToList();
        return new[]{("soloKills","Tek başına alınan skor"),("skillshotsDodged","Kaçınılan yetenek atışı"),("skillshotsHit","İsabet eden yetenek atışı"),("controlWardsPlaced","Yerleştirilen kontrol totemi"),("earlyLaningPhaseGoldExpAdvantage","Erken koridor üstünlüğü"),("saveAllyFromDeath","Ölümden kurtarılan takım arkadaşı"),("kTurretsDestroyedBeforePlatesFall","Plakalar düşmeden yıkılan kule")}
            .Select(pair=>{var values=rows.Select(r=>r.P["challenges"]).Where(n=>n?[pair.Item1]!=null).Select(n=>n.N(pair.Item1)).ToList();return new Metric(pair.Item2,values.Count==0?"—":$"{values.Average():0.00}");}).ToList();
    }
}
