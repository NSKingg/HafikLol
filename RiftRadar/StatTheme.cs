using System.Globalization;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RiftRadar;

public sealed record StatVisual(string Key,string Name,string Hex,string Icon)
{
    public Brush Color => MakeBrush(Hex);
    static Brush MakeBrush(string hex)=>Ui.Brush(hex);
    public BitmapSource Image=>StatTheme.Image(Icon);
}
public static class StatTheme
{
    public static readonly StatVisual Health=new("health","Can","#2CB878","health"),HealthRegen=new("healthregen","Can yenileme / iyileştirme","#47C487","healthregen"),Mana=new("mana","Mana","#38B7E6","mana"),ManaRegen=new("manaregen","Mana yenileme","#4DBCDC","manaregen"),AD=new("ad","Saldırı gücü / fiziksel hasar","#F1A04B","attackdamage"),AP=new("ap","Yetenek gücü / büyü hasarı","#9D7AFF","abilitypower"),Armor=new("armor","Zırh","#E6C46A","armor"),MR=new("mr","Büyü direnci","#61D6DF","magicresist"),AttackSpeed=new("attackspeed","Saldırı hızı","#E9D27D","attackspeed"),Haste=new("haste","Yetenek hızı","#E9E2C8","abilityhaste"),Movement=new("movement","Hareket hızı","#DCD9CA","movementspeed"),Crit=new("crit","Kritik vuruş","#EDEBDD","criticalstrike"),Lifesteal=new("lifesteal","Can çalma","#D84C67","lifesteal"),Gold=new("gold","Altın","#DFC16B","gold"),Damage=new("damage","Hasar","#F58256","damage"),Vision=new("vision","Görüş","#D9C679","vision"),Shield=new("shield","Kalkan","#83D7BB","shield"),Participation=new("participation","Takım katkısı","#BEC9B8","participation"),Duration=new("duration","Süre","#D6C37C","duration"),Kills=new("kills","Skor / KDA","#EACB69","kills"),Level=new("level","Seviye / deneyim","#DDD4B4","level"),PhysicalPen=new("physicalpen","Zırh delme","#E78D71","physicalpen"),MagicPen=new("magicpen","Büyü nüfuzu","#C18AE8","magicpen"),Tenacity=new("tenacity","Sıvışma","#CEC39E","tenacity"),Generic=new("generic","İstatistik","#C5C1AE","generic");
    static readonly Dictionary<string,BitmapSource> images=[];
    public static BitmapSource Image(string name)
    {
        if(images.TryGetValue(name,out var cached))return cached;
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=new Uri("pack://application:,,,/Assets/Stats/hud-"+name+".png");bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();images[name]=bitmap;return bitmap;
    }
    static string Clean(string s)=>new string(s.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant().Replace('ı','i');
    public static StatVisual? Resolve(string text,bool metric=false)
    {
        var k=Clean(text);
        bool Has(params string[] words)=>words.Any(k.Contains);
        if(Has("puuid","riotid","gamename","tagline","championname","teamposition","participantid","teamid","subteamid","item0","item1","item2","item3","item4","item5","item6","augment","perks.","gametype","position"))return null;
        if(Has("kacinilan yetenek"))return Movement;
        if(Has("isabet eden yetenek"))return Damage;
        if(Has("koridor ustunlugu"))return Gold;
        if(Has("yikilan kule"))return Damage;
        if(Has("healthregen","hpregen","heals","heal","iyilest","can yenile"))return HealthRegen;
        if(Has("manaregen","mpregen","mana yenile"))return ManaRegen;
        if(Has("armorpenetr","physicalpen","zirh del"))return PhysicalPen;
        if(Has("magicpenetr","magicpen","buyu nuf"))return MagicPen;
        if(Has("lifesteal","can cal"))return Lifesteal;
        if(Has("health","hppool","can"))return Health;
        if(Has("shield","kalkan"))return Shield;
        if(Has("abilitypower","magicdamagemod","magicdamage","yetenek gucu","buyu hasar")||k=="ap")return AP;
        if(Has("attackdamage","physicaldamage","saldiri gucu","fiziksel")||k=="ad")return AD;
        if(Has("spellblock","magicres","buyu dir"))return MR;
        if(Has("armor","zirh"))return Armor;
        if(Has("attackspeed","saldiri hizi"))return AttackSpeed;
        if(Has("abilityhaste","cooldown","cdr","yetenek hizi","bekleme"))return Haste;
        if(Has("movementspeed","hareket"))return Movement;
        if(Has("crit","kritik"))return Crit;
        if(Has("mana","mppool"))return Mana;
        if(Has("gold","altin"))return Gold;
        if(Has("vision","ward","gorus","totem"))return Vision;
        if(Has("damage","hasar"))return Damage;
        if(Has("kills","kda","skor","doublekill","triplekill","quadrakill","pentakill")||k.Contains("k / d / a"))return Kills;
        if(Has("particip","katilim","katki","assist","takim")||k.StartsWith("kp ")||k=="kp")return Participation;
        if(Has("duration","sure","dakika","time","timestamp"))return Duration;
        if(Has("tenacity","sivis","slowres"))return Tenacity;
        if(Has("level","seviye","experience","deneyim","xp"))return Level;
        if(Has("neutralminion","totalminion","creepscore","cs/dk","cs / dk","csvalue")||k.StartsWith("cs ")||k=="cs")return Gold;
        if(Has("win","basari","kazanma","oran","mac","kullanim","placement","sira","puan","count","death"))return Generic;
        return metric?Generic:null;
    }
}
