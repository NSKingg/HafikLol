using System.Windows;
using System.Windows.Media;
namespace RiftRadar;
public static class ThemePalette
{
    public static bool Light {get;private set;}
    static readonly List<(WeakReference<SolidColorBrush> Brush,string Hex)> brushes=[];
    static readonly Dictionary<string,string> light=new(StringComparer.OrdinalIgnoreCase)
    {
        ["#13243D"]="#FFFFFF",["#1B2821"]="#FFFFFF",["#121418"]="#F3F5F9",["#17191E"]="#FFFFFF",["#1B1E24"]="#FFFFFF",["#171719"]="#F7F8FB",["#141416"]="#F2F4F8",
        ["#1C2028"]="#FFFFFF",["#1D2027"]="#F5F7FA",["#212124"]="#F3F5F8",["#242428"]="#E9EDF3",["#252528"]="#FFFFFF",["#26262B"]="#E8EDF5",["#27272B"]="#F0F3F8",["#2B2B30"]="#E7EBF2",
        ["#2C313A"]="#D9DFE9",["#2E2E33"]="#E0E5EE",["#39393F"]="#D4DAE5",["#39393E"]="#D4DAE5",["#45454D"]="#CAD2DF",["#3B3B42"]="#E3E9F2",
        ["#253249"]="#DCE8FD",["#253246"]="#E1EBFC",["#293B56"]="#D7E5FC",["#202C3D"]="#ECF3FF",["#1B2432"]="#EAF1FB",["#2A3D58"]="#DAE8FC",["#233B60"]="#DCE9FF",["#3A5276"]="#BFD4F5",
        ["#2A2028"]="#FFF0F3",["#392A30"]="#FCE6EB",["#443039"]="#F4DCE3",["#512C39"]="#F4DCE3",["#514A3C"]="#F2E8CE",
        ["#ECECEF"]="#202838",["#ECEFF5"]="#202838",["#DEDEE3"]="#202838",["#FFFFFF"]="#202838",["#edeeee"]="#202838",["#F0F3F0"]="#202838",["#E6F3EA"]="#203047",
        ["#969FAC"]="#626F83",["#777781"]="#6B778B",["#BFBFC7"]="#59677D",["#C1C1CA"]="#536279",["#A6B6AB"]="#576B63",["#B6C6DF"]="#536988",["#505057"]="#B8C3D3",["#73737D"]="#8D9CB2",
        ["#84B5FF"]="#3878D5",["#70ADFF"]="#2367C2",["#F27685"]="#BF3650",["#F58256"]="#BF4A20",["#F1A04B"]="#A56011",["#E9D27D"]="#806717",["#EACB69"]="#876B12",["#E9B76C"]="#91601C",
        ["#E4BE63"]="#896516",["#EBD38B"]="#806217",["#C6D3DC"]="#63768B",["#91B6FF"]="#426FC4",["#59D49C"]="#16815B",["#63D3C8"]="#167F7A",["#C590ED"]="#8240AC",["#F07A76"]="#BD4645"
    };
    public static Color Color(string hex)
    {
        if(Light&&light.TryGetValue(hex,out var mapped))return (Color)ColorConverter.ConvertFromString(mapped);
        var color=(Color)ColorConverter.ConvertFromString(hex);
        if(Light&&color.A==255&&Math.Max(color.R,Math.Max(color.G,color.B))>165){color.R=(byte)(color.R*.58);color.G=(byte)(color.G*.58);color.B=(byte)(color.B*.58);}return color;
    }
    public static SolidColorBrush Brush(string hex){var brush=new SolidColorBrush(Color(hex));brushes.Add((new(brush),hex));if(brushes.Count%512==0)brushes.RemoveAll(item=>!item.Brush.TryGetTarget(out _));return brush;}
    public static object Resource(string hex)
    {
        var key="Theme_"+hex.TrimStart('#').ToUpperInvariant();var resources=Application.Current.Resources;if(!resources.Contains(key))resources[key]=Brush(hex);return new DynamicResourceExtension(key);
    }
    public static void Apply(bool isLight)
    {
        Light=isLight;
        if(Application.Current is {} app)
        {
            foreach(var key in app.Resources.Keys.Cast<object>().OfType<string>().Where(k=>k.StartsWith("Theme_")).ToList())app.Resources[key]=Brush("#"+key[6..]);
            foreach(var (key,hex) in new[]{("Bg","#121418"),("Panel","#1B1E24"),("Line","#2C313A"),("Text","#ECECEF"),("Muted","#969FAC"),("Accent","#84B5FF")})app.Resources[key]=Brush(hex);
        }
        for(int i=brushes.Count-1;i>=0;i--){var item=brushes[i];if(!item.Brush.TryGetTarget(out var brush)){brushes.RemoveAt(i);continue;}if(!brush.IsFrozen)brush.Color=Color(item.Hex);}
    }
}
