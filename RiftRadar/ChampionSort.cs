namespace RiftRadar;

public static class ChampionSort
{
    public static List<IGrouping<string,MatchRow>> Order(IEnumerable<MatchRow> rows,Player p,StaticData data,string sort)
    {
        double? Value(IGrouping<string,MatchRow> group)
        {
            var mastery=LiveComparison.ChampionMastery(p,group.Key,data);var valid=group.Where(r=>!r.Remake).ToList();
            return sort switch
            {
                "Ustalık puanı"=>mastery==null?null:mastery.N("championPoints"),"Ustalık seviyesi"=>mastery==null?null:mastery.N("championLevel"),
                "Winrate" or "Kazanma oranı"=>valid.Count==0?null:100d*valid.Count(r=>r.Win)/valid.Count,
                "KDA"=>group.Sum(r=>r.P.N("kills")+r.P.N("assists"))/Math.Max(1,group.Sum(r=>r.P.N("deaths"))),
                "Hasar / dk"=>group.Average(r=>r.Dpm),"CS / dk"=>group.Average(r=>r.CsValue/r.Minutes),_=>group.Count()
            };
        }
        var groups=rows.GroupBy(r=>r.Champion);
        return sort=="İsim"?groups.OrderBy(g=>data.Champion(g.Key).S("name",g.Key),StringComparer.CurrentCultureIgnoreCase).ToList():groups.OrderByDescending(g=>Value(g).HasValue).ThenByDescending(Value).ThenByDescending(g=>g.Count()).ThenBy(g=>g.Key).ToList();
    }
}
