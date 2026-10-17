using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media.Imaging;

namespace RiftRadar;

public sealed partial class StaticData
{
    public string Version {get; private set;}="";
    public JsonObject Champions {get;private set;}=new();
    public JsonObject Items {get;private set;}=new();
    public JsonArray Runes {get;private set;}=[];
    public JsonArray Queues {get;private set;}=[];
    readonly Store store;
    readonly JsonObject bundledItems;
    readonly Dictionary<string,BitmapSource> itemImages=[];
    readonly Dictionary<string,BitmapSource> sprites=[];
    readonly Dictionary<string,BitmapSource> championImages=[];
    public StaticData(Store store)
    {
        this.store=store;
        Version=Resource("version.txt").Trim();
        Champions=(J.Parse(Resource("champion.json"))["data"] as JsonObject) ?? new();
        Items=(J.Parse(Resource("item.json"))["data"] as JsonObject) ?? new();
        bundledItems=Items;
        Runes=J.Parse(Resource("runesReforged.json")) as JsonArray ?? [];
        Queues=J.Parse(Resource("queues.json")) as JsonArray ?? [];
        try
        {
            if(store.Cached("static:data",TimeSpan.FromDays(30)) is {} c)
            {
                var n=J.Parse(c); Version=n.S("version"); Champions=n["champions"]!.DeepClone().AsObject(); Items=n["items"]!.DeepClone().AsObject(); Runes=n["runes"]!.DeepClone().AsArray();
            }
        }
        catch(System.Text.Json.JsonException) {}
    }
    static string Resource(string name)
    {
        using var s=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/"+name))!.Stream;
        using var r=new StreamReader(s); return r.ReadToEnd();
    }
    public string ChampionName(int id) => Champions.Select(x=>x.Value).FirstOrDefault(x=>x.I("key")==id).S("name",id.ToString());
    public JsonNode? Champion(string name) => Champions.FirstOrDefault(x=>x.Key.Equals(name,StringComparison.OrdinalIgnoreCase) || x.Value.S("name").Equals(name,StringComparison.OrdinalIgnoreCase)).Value;
    public BitmapSource? ChampionImage(string name)
    {
        var champ=Champion(name);if(champ==null&&int.TryParse(name,out var numeric))champ=Champions.Select(x=>x.Value).FirstOrDefault(x=>x.I("key")==numeric);
        var code=champ.S("id");if(code.Length==0)return null;if(championImages.TryGetValue(code,out var image))return image;
        try
        {
            var local=Path.Combine(store.Root,"assets",Version,"champions",code+".png");var b=new BitmapImage();b.BeginInit();b.DecodePixelWidth=80;b.CacheOption=BitmapCacheOption.OnLoad;
            b.UriSource=File.Exists(local)?new Uri(local):new Uri("pack://application:,,,/Assets/Champions/"+code+".png");b.EndInit();b.Freeze();championImages[code]=b;return b;
        }
        catch(Exception e)when(e is IOException or ArgumentException or NotSupportedException){return null;}
    }
    public string ItemName(int id) => Items[id.ToString()].S("name",id==0 ? "—" : "Eşya "+id);
    public BitmapSource? ItemImage(string id)
    {
        if(id=="0"||string.IsNullOrEmpty(id))return null;
        if(itemImages.TryGetValue(id,out var cached))return cached;
        var definition=Items[id]?["image"];
        var filename=definition.S("sprite");
        var local=Path.Combine(store.Root,"assets",Version,filename);
        bool downloaded=System.Text.RegularExpressions.Regex.IsMatch(filename,@"^item\d+\.png$")&&File.Exists(local);
        if(!downloaded){definition=bundledItems[id]?["image"];filename=definition.S("sprite");}
        if(definition==null||!System.Text.RegularExpressions.Regex.IsMatch(filename,@"^item\d+\.png$"))return null;
        try
        {
            var key=downloaded?local:filename;
            if(!sprites.TryGetValue(key,out var sprite))
            {
                var b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;
                b.UriSource=downloaded?new Uri(local):new Uri("pack://application:,,,/Assets/Items/"+filename);b.EndInit();b.Freeze();sprites[key]=sprite=b;
            }
            var rect=new Int32Rect(definition.I("x"),definition.I("y"),definition.I("w"),definition.I("h"));
            if(rect.Width<1||rect.Height<1||rect.X<0||rect.Y<0||rect.X+rect.Width>sprite.PixelWidth||rect.Y+rect.Height>sprite.PixelHeight)return null;
            var crop=new CroppedBitmap(sprite,rect);crop.Freeze();itemImages[id]=crop;return crop;
        }
        catch(Exception e)when(e is IOException or ArgumentException or NotSupportedException){return null;}
    }
    public string RuneName(int id)
    {
        foreach(var t in Runes.Rows()) { if(t.I("id")==id) return t.S("name"); foreach(var slot in t.A("slots")) foreach(var rune in slot.A("runes")) if(rune.I("id")==id) return rune.S("name"); }
        return id.ToString();
    }
    public async Task Refresh(CancellationToken ct)
    {
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}) {Timeout=TimeSpan.FromSeconds(20),MaxResponseContentBufferSize=8_000_000};
        var versions=J.Parse(await client.GetStringAsync("https://ddragon.leagueoflegends.com/api/versions.json",ct));
        var v=versions[0]!.ToString();
        if(!System.Text.RegularExpressions.Regex.IsMatch(v,@"^\d+\.\d+\.\d+$")) throw new InvalidDataException();
        var baseUrl="https://ddragon.leagueoflegends.com/cdn/"+v+"/data/tr_TR/";
        var champions=J.Parse(await client.GetStringAsync(baseUrl+"champion.json",ct))["data"]!.DeepClone().AsObject();
        var items=J.Parse(await client.GetStringAsync(baseUrl+"item.json",ct))["data"]!.DeepClone().AsObject();
        var runes=J.Parse(await client.GetStringAsync(baseUrl+"runesReforged.json",ct)).AsArray();
        var imageDir=Path.Combine(store.Root,"assets",v);Directory.CreateDirectory(imageDir);
        foreach(var sprite in items.Select(x=>x.Value?["image"].S("sprite")).OfType<string>().Distinct())
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(sprite,@"^item\d+\.png$"))continue;
            var path=Path.Combine(imageDir,sprite);if(File.Exists(path))continue;
            var bytes=await client.GetByteArrayAsync("https://ddragon.leagueoflegends.com/cdn/"+v+"/img/sprite/"+sprite,ct);
            if(bytes.Length<8||bytes[0]!=137||bytes[1]!=80||bytes[2]!=78||bytes[3]!=71)throw new InvalidDataException();
            Store.Atomic(path,bytes);
        }
        var championDir=Path.Combine(imageDir,"champions");Directory.CreateDirectory(championDir);
        foreach(var code in champions.Select(x=>x.Key))
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(code,@"^[A-Za-z0-9_]+$"))continue;
            var path=Path.Combine(championDir,code+".png");if(File.Exists(path))continue;
            var bytes=await client.GetByteArrayAsync("https://ddragon.leagueoflegends.com/cdn/"+v+"/img/champion/"+code+".png",ct);
            if(bytes.Length<8||bytes[0]!=137||bytes[1]!=80||bytes[2]!=78||bytes[3]!=71)throw new InvalidDataException();Store.Atomic(path,bytes);
        }
        store.Cache("static:data",new JsonObject { ["version"]=v,["champions"]=champions.DeepClone(),["items"]=items.DeepClone(),["runes"]=runes.DeepClone() }.ToJsonString());
        Version=v; Champions=champions; Items=items; Runes=runes;
        itemImages.Clear();sprites.Clear();championImages.Clear();
    }
    public IEnumerable<ItemRow> ItemRows(string mode,string text)
    {
        string map=mode=="Arena" ? "30" : mode is "ARAM" or "ARAM Mayhem" ? "12" : "11";
        return Items.Where(x=>x.Value!=null).Select(x=>new ItemRow(x.Key,x.Value!))
            .Where(x=>(x.Raw["maps"]?.B(map) ?? false) && x.Raw["gold"].B("purchasable") && !x.Raw.B("hideFromAll"))
            .Where(x=>string.IsNullOrWhiteSpace(text) || (x.Name+" "+x.Description+" "+x.Id).Contains(text,StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(x=>x.Gold).ThenBy(x=>x.Name);
    }
}
public record ItemRow(string Id,JsonNode Raw)
{
    public string Name=>Raw.S("name");
    public int Gold=>Raw["gold"].I("total");
    public string Description=>J.Strip(Raw.S("description"));
    public string StatSummary=>string.Join(" · ",ItemDetails.Stats(this).Take(4).Select(x=>x.Amount+" "+x.Name));
    public string Tags=>string.Join(", ",Raw.A("tags").Select(x=>StatNames.Tags.GetValueOrDefault(x.ToString(),x.ToString())));
    public override string ToString()=>Name;
}
public static class StatNames
{
    public static readonly Dictionary<string,string> Tags=new(){["Damage"]="Saldırı gücü",["CriticalStrike"]="Kritik",["SpellDamage"]="Yetenek gücü",["LifeSteal"]="Can çalma",["Health"]="Can",["HealthRegen"]="Can yenileme",["Armor"]="Zırh",["SpellBlock"]="Büyü direnci",["AttackSpeed"]="Saldırı hızı",["CooldownReduction"]="Bekleme süresi",["AbilityHaste"]="Yetenek hızı",["OnHit"]="İsabet etkisi",["NonbootsMovement"]="Hareket",["Boots"]="Çizme",["Mana"]="Mana",["ManaRegen"]="Mana yenileme",["ArmorPenetration"]="Zırh delme",["MagicPenetration"]="Büyü nüfuzu",["Trinket"]="Ziynet",["Consumable"]="Tüketilebilir",["Vision"]="Görüş",["Active"]="Aktif",["Tenacity"]="Sıvışma",["Stealth"]="Gizlilik",["Slow"]="Yavaşlatma",["Aura"]="Hale",["GoldPer"]="Altın",["Jungle"]="Orman",["Lane"]="Koridor"};
    public static string Format(string key,double value)
    {
        var labels=new Dictionary<string,string>{["FlatHPPoolMod"]="Can",["FlatMPPoolMod"]="Mana",["FlatPhysicalDamageMod"]="Saldırı gücü",["FlatMagicDamageMod"]="Yetenek gücü",["FlatArmorMod"]="Zırh",["FlatSpellBlockMod"]="Büyü direnci",["FlatMovementSpeedMod"]="Hareket hızı",["PercentMovementSpeedMod"]="Hareket hızı",["PercentAttackSpeedMod"]="Saldırı hızı",["PercentLifeStealMod"]="Can çalma",["FlatCritChanceMod"]="Kritik ihtimali",["FlatHPRegenMod"]="Can yenileme"};
        var percentage=key.StartsWith("Percent")||key=="FlatCritChanceMod";
        return $"{labels.GetValueOrDefault(key,key)}: {(percentage?value*100:value):0.##}{(percentage?"%":"")}";
    }
}
public record BuildRow(int ItemId,string Eşya,int Maç,string Kullanım,string Kazanma,string Örneklem);
public static class BuildStats
{
    public static List<BuildRow> Items(IEnumerable<JsonNode> matches,StaticData data,string champ,string mode,string patch)
    {
        if(mode=="Arena") return []; // Riot policy prohibits Arena item win rates.
        var sample=matches.Where(Modes.PublicMatch).Where(m=>Modes.Accept(m,mode))
            .Where(m=>string.Join('.',m["info"].S("gameVersion").Split('.').Take(2))==patch)
            .Where(m=>!Modes.IsArena(m))
            .GroupBy(m=>m["metadata"].S("matchId")).Select(g=>g.First())
            .SelectMany(m=>m["info"].A("participants").Where(p=>string.IsNullOrWhiteSpace(champ) || p.S("championName").Equals(champ,StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return sample.SelectMany(p=>Enumerable.Range(0,6).Select(i=>p.I("item"+i)).Where(id=>id>0).Distinct().Select(id=>(id,win:p.B("win"))))
            .GroupBy(x=>x.id).Where(g=>data.Items[g.Key.ToString()] is {} item && (item["maps"].B("11") || item["maps"].B("12")) && !item.A("into").Any() && item["gold"].I("total")>=1800)
            .OrderByDescending(g=>g.Count())
            .Select(g=>new BuildRow(g.Key,data.ItemName(g.Key),g.Count(),$"{100.0*g.Count()/Math.Max(1,sample.Count):0.0}%",g.Count()>=10 ? $"{100.0*g.Count(x=>x.win)/g.Count():0.0}%" : "Az veri (<10)",$"{sample.Count} oyuncu-maç")).ToList();
    }
}
