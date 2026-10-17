using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RiftRadar;

public static class J
{
    public static string S(this JsonNode? n, string k, string fallback = "") => n?[k]?.ToString() ?? fallback;
    public static double N(this JsonNode? n, string k) => double.TryParse(n.S(k), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    public static int I(this JsonNode? n, string k) => (int)n.N(k);
    public static bool B(this JsonNode? n, string k) => bool.TryParse(n.S(k), out var v) && v;
    public static IEnumerable<JsonNode> A(this JsonNode? n, string k) => (n?[k] as JsonArray)?.OfType<JsonNode>() ?? [];
    public static IEnumerable<JsonNode> Rows(this JsonNode? n) => (n as JsonArray)?.OfType<JsonNode>() ?? [];
    public static string Strip(string s) => System.Net.WebUtility.HtmlDecode(Regex.Replace(s.Replace("<br>", "\n").Replace("<br/>", "\n"), "<[^>]+>", ""));
    public static JsonNode Parse(string s) => JsonNode.Parse(s) ?? new JsonObject();
}

public record Region(string Label, string Platform, string Cluster)
{
    public static readonly Region[] All = [new("TR", "tr1", "europe"), new("EUW", "euw1", "europe"), new("EUNE", "eun1", "europe"), new("NA", "na1", "americas"), new("KR", "kr", "asia"), new("RU", "ru", "europe"), new("BR", "br1", "americas"), new("LAN", "la1", "americas"), new("LAS", "la2", "americas"), new("OCE", "oc1", "sea"), new("JP", "jp1", "asia"), new("SEA", "sg2", "sea"), new("TW", "tw2", "sea"), new("VN", "vn2", "sea")];
    public override string ToString() => Label;
    public static Region Find(string p) => All.FirstOrDefault(x => x.Platform == p) ?? All[0];
}
public sealed class Preferences
{
    public bool UseServer { get; set; } = true;
    public string Platform { get; set; } = "tr1";
    public string Selected { get; set; } = "";
    public int MatchCount { get; set; } = 20;
    public int PollSeconds { get; set; } = 5;
    public bool Economy { get; set; } = true;
    public bool LightMode { get; set; }
    public bool CloseToTray { get; set; } = true;
}
public sealed class Player
{
    public string Puuid { get; set; } = "";
    public string GameName { get; set; } = "";
    public string TagLine { get; set; } = "";
    public string Platform { get; set; } = "tr1";
    public string Note { get; set; } = "";
    public int Level { get; set; }
    public int Icon { get; set; }
    public DateTimeOffset Updated { get; set; }
    public DateTimeOffset LastViewed { get; set; }
    public List<string> MatchIds { get; set; } = [];
    public int HistoryCursor { get; set; }
    public JsonArray Ranks { get; set; } = [];
    public JsonArray Mastery { get; set; } = [];
    public string Key => Platform + ":" + Puuid;
    public string RiotId => GameName + "#" + TagLine;
    public string RankText => Ranks.Rows().FirstOrDefault(x => x.S("queueType") == "RANKED_SOLO_5x5") is { } r ? $"{r.S("tier")} {r.S("rank")} · {r.I("leaguePoints")} LP" : "Derecesiz";
    public override string ToString() => RiotId;
}
public static class Modes
{
    public static readonly string[] All = ["Tüm modlar", "Sihirdar Vadisi", "Dereceli Solo", "Dereceli Flex", "ARAM", "ARAM Mayhem", "Arena", "Diğer"];
    public static string Label(int q) => q switch { 420 => "Dereceli Solo", 440 => "Dereceli Flex", 400 => "Normal seçim", 430 => "Normal kapalı", 480 => "Hızlı oyun", 490 => "Hızlı oyun", 450 => "ARAM", 1700 or 1710 or 1750 => "Arena", 2400 => "ARAM Mayhem", _ => "Mod " + q };
    public static bool IsArena(int q) => q is 1700 or 1710 or 1750;
    public static bool IsArena(JsonNode match) => IsArena(match["info"].I("queueId")) || match["info"].S("gameMode")=="CHERRY" || match["info"].I("mapId")==30;
    public static bool IsMayhem(int q) => q == 2400;
    public static bool Accept(JsonNode match, string mode)
    {
        var i = match["info"]; var q = i.I("queueId");var arena=IsArena(match);
        return mode switch { "Tüm modlar" => true, "Sihirdar Vadisi" => i.I("mapId") == 11 && !arena, "Dereceli Solo" => q == 420, "Dereceli Flex" => q == 440, "ARAM" => q == 450&&!arena, "ARAM Mayhem" => IsMayhem(q)&&!arena, "Arena" => arena, "Diğer" => i.I("mapId") != 11 && q != 450 && !IsMayhem(q) && !arena, _ => true };
    }
    public static bool PublicMatch(JsonNode m) => m["info"].I("queueId") != 0 && m["info"].S("gameType") != "CUSTOM_GAME";
}
public sealed class MatchRow
{
    public required JsonNode Raw { get; init; }
    public required JsonNode P { get; init; }
    public JsonNode Info => Raw["info"]!;
    public string Id => Raw["metadata"].S("matchId");
    public string Champion => P.S("championName");
    public int Queue => Info.I("queueId");
    public bool Arena => Modes.IsArena(Raw);
    public int Placement=>P.I("placement")>0?P.I("placement"):P.I("subteamPlacement");
    public int ArenaTeams=>Info.A("participants").Select(x=>x.I("playerSubteamId")).Where(x=>x>0).Distinct().Count();
    public bool Win => Arena ? Placement > 0 && Placement <= (ArenaTeams>=2?ArenaTeams/2:Queue==1700?2:4) : P.B("win");
    public bool Remake => Info.N("gameDuration") < 300 && !Arena;
    public string Result => Remake ? "Yeniden" : Arena ? Placement>0?$"#{Placement} sıra":"Arena" : Win ? "Zafer" : "Bozgun";
    public double Seconds => Math.Max(1, Info.N("gameDuration"));
    public double Minutes => Seconds / 60;
    public string Duration => TimeSpan.FromSeconds(Seconds).ToString(@"mm\:ss");
    public string Mode => Arena?"Arena":Modes.Label(Queue);
    public string Kda => $"{P.I("kills")} / {P.I("deaths")} / {P.I("assists")}";
    public double KdaValue => (P.N("kills") + P.N("assists")) / Math.Max(1, P.N("deaths"));
    public double CsValue => P.N("totalMinionsKilled") + P.N("neutralMinionsKilled");
    public string Cs => $"{CsValue:0} · {CsValue / Minutes:0.0}/dk";
    public double Dpm => P.N("totalDamageDealtToChampions") / Minutes;
    public string Damage => P.N("totalDamageDealtToChampions").ToString("N0");
    public string Vision => P.N("visionScore").ToString("0");
    public int[] Items => Enumerable.Range(0,7).Select(i=>P.I("item"+i)).ToArray();
    public double Kp
    {
        get { var kills = Info.A("participants").Where(x => x.I("teamId") == P.I("teamId") && (!Arena || x.I("playerSubteamId") == P.I("playerSubteamId"))).Sum(x => x.N("kills")); return kills > 0 ? (P.N("kills") + P.N("assists")) / kills * 100 : 0; }
    }
    public string Participation => $"{Kp:0}%";
    public string Date => DateTimeOffset.FromUnixTimeMilliseconds((long)Info.N("gameCreation")).LocalDateTime.ToString("dd MMM HH:mm");
    public string Role => P.S("teamPosition", "—");
    public string Patch => string.Join('.', Info.S("gameVersion").Split('.').Take(2));
}
public static class Analytics
{
    public static List<MatchRow> For(IEnumerable<JsonNode> data, Player p, string mode = "Tüm modlar", string champ = "") => data.Where(Modes.PublicMatch).Where(m => Modes.Accept(m, mode)).Select(m => (m, p: m["info"].A("participants").FirstOrDefault(x => x.S("puuid") == p.Puuid))).Where(x => x.p != null && (string.IsNullOrWhiteSpace(champ) || x.p.S("championName").Contains(champ, StringComparison.OrdinalIgnoreCase))).Select(x => new MatchRow { Raw = x.m, P = x.p! }).OrderByDescending(x => x.Info.N("gameCreation")).ToList();
    public static IEnumerable<object> Flatten(JsonNode node, string prefix = "")
    {
        if (node is JsonObject o) foreach (var kv in o) { if (kv.Value == null || kv.Key.Contains("puuid", StringComparison.OrdinalIgnoreCase)) continue; if (kv.Value is JsonObject or JsonArray) { foreach (var a in Flatten(kv.Value, prefix + kv.Key + ".")) yield return a; } else yield return new Metric(prefix + kv.Key, kv.Value.ToString()); }
        else if (node is JsonArray arr) { for (var i=0; i<arr.Count; i++) if (arr[i] is {} child) foreach(var a in Flatten(child, prefix+i+".")) yield return a; }
        else yield return new Metric(prefix.TrimEnd('.'),node.ToString());
    }
    public static string Csv(IEnumerable<MatchRow> rows)
    {
        static string Q(string s) { if (s.Length>0 && "=+-@\t\r".Contains(s[0])) s="'"+s; return "\"" + s.Replace("\"", "\"\"") + "\""; }
        var text = new System.Text.StringBuilder("Tarih;Şampiyon;Mod;Sonuç;K;D;A;CS;Hasar;Görüş;Süre;Yama\r\n");
        foreach(var r in rows) text.AppendLine(string.Join(';', new[]{r.Date,r.Champion,r.Mode,r.Result,r.P.S("kills"),r.P.S("deaths"),r.P.S("assists"),r.CsValue.ToString("0"),r.Damage,r.Vision,r.Duration,r.Patch}.Select(Q)));
        return text.ToString();
    }
}
public record Metric(string Alan, string Değer);
public sealed class ApiException(int code, string message) : Exception(message) { public int Code { get; } = code; }
public static class SafeError
{
    public static string Message(Exception e) => e switch { ApiException => e.Message, OperationCanceledException => "İşlem iptal edildi veya bağlantı zaman aşımına uğradı.", System.Net.Http.HttpRequestException => "Bağlantı kurulamadı. İnterneti ve Riot servis durumunu kontrol et.", IOException => "Dosyaya erişilemedi. Klasörün yazılabilir olduğunu kontrol et.", _ => "İşlem tamamlanamadı. Ayarları kontrol edip tekrar dene." };
}
