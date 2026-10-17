using System.Windows;
using System.Windows.Controls;

namespace RiftRadar;

public sealed record RolePerformance(string Rol,int Maç,double KDA,double? Başarı,double Hasar);
public sealed record ContributionMetric(string Alan,string Değer,string Dakika);

public static class InsightStats
{
    public static string RoleKey(MatchRow row)
    {
        if(row.Arena)return "ARENA";
        if(row.Info.S("gameMode")=="ARAM"||row.Info.I("mapId")==12)return "ARAM";
        var key=row.Role.ToUpperInvariant();return key is "TOP" or "JUNGLE" or "MIDDLE" or "BOTTOM" or "UTILITY"?key:"UNKNOWN";
    }
    public static List<RolePerformance> Roles(IEnumerable<MatchRow> rows)
    {
        string[] order=["TOP","JUNGLE","MIDDLE","BOTTOM","UTILITY","ARAM","ARENA","UNKNOWN"];
        return rows.Where(r=>!r.Remake).GroupBy(RoleKey).OrderBy(g=>Array.IndexOf(order,g.Key)).Select(g=>new RolePerformance(g.Key,g.Count(),g.Sum(r=>r.P.N("kills")+r.P.N("assists"))/Math.Max(1,g.Sum(r=>r.P.N("deaths"))),100d*g.Count(r=>r.Win)/g.Count(),g.Average(r=>r.Dpm))).ToList();
    }
    public static List<ContributionMetric> Contribution(IEnumerable<MatchRow> matches)
    {
        var rows=matches.Where(r=>!r.Remake).ToList();
        return new[]{("totalHealsOnTeammates","Takım arkadaşına iyileştirme"),("totalDamageShieldedOnTeammates","Takım arkadaşına kalkan"),("damageDealtToBuildings","Yapılara hasar"),("damageDealtToObjectives","Hedeflere hasar"),("wardsPlaced","Yerleştirilen totem"),("wardsKilled","Temizlenen totem"),("visionWardsBoughtInGame","Alınan kontrol totemi")}
            .Select(pair=>{var available=rows.Where(r=>r.P[pair.Item1]!=null).ToList();return new ContributionMetric(pair.Item2,available.Count==0?"—":$"{available.Average(r=>r.P.N(pair.Item1)):N1}",available.Count==0?"—":$"{available.Average(r=>r.P.N(pair.Item1)/r.Minutes):N2}");}).ToList();
    }
}

public partial class MainWindow
{
    FrameworkElement RolePerformanceCard(IEnumerable<MatchRow> rows)
    {
        var table=Ui.Table(InsightStats.Roles(rows),("Rol","Rol",1.7),("Maç","Maç",.55),("KDA","KDA",.8),("Başarı / ilk yarı %","Başarı",1.2),("Hasar/dk","Hasar",1));
        Ui.RoleColumn(table,"Rol");table.RowHeight=58;table.Language=System.Windows.Markup.XmlLanguage.GetLanguage("tr-TR");
        foreach(var col in table.Columns.OfType<DataGridTextColumn>())if(col.Binding is System.Windows.Data.Binding b)b.StringFormat=b.Path.Path=="Maç"?"0":b.Path.Path=="Hasar"?"N0":"0.00";
        return Ui.Card(Ui.Stack(Ui.Text("Role göre performans",18,null,true),Ui.Space(12),table,Ui.Space(8),Ui.Text("Roller oyun verisine göre gruplandırılır. ARAM ve Arena ayrı gösterilir; bu modlarda koridor rolü atanmaz. Yeniden oynanan maçlar hariçtir.",11,Ui.Muted)));
    }
    FrameworkElement TeamContributionCard(IEnumerable<MatchRow> rows)
    {
        var table=Ui.Table(InsightStats.Contribution(rows),("Katkı türü","Alan",2),("Maç başına ortalama","Değer",1),("Dakika başına ortalama","Dakika",1));table.RowHeight=54;
        return Ui.Card(Ui.Stack(Ui.Text("Takıma katkı",18,null,true),Ui.Space(12),table,Ui.Space(8),Ui.Text("Yeniden oynanan maçlar hariçtir. Ortalamalar ilgili veriyi sağlayan maçlardan hesaplanır; — veri yok demektir.",11,Ui.Muted)));
    }
}
