using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Data;
namespace RiftRadar;
public static class MatchIdentity
{
    public static string Name(JsonNode participant)
    {
        var name=participant.S("riotIdGameName");if(name.Length==0)name=participant.S("summonerName","Gizli");
        var tag=participant.S("riotIdTagline");return name+(tag.Length>0?"#"+tag:"");
    }
    public static Player Player(JsonNode participant,string platform)=>new(){GameName=participant.S("riotIdGameName",participant.S("summonerName")),TagLine=participant.S("riotIdTagline"),Puuid=participant.S("puuid"),Icon=participant["profileIcon"]==null?-1:participant.I("profileIcon"),Level=participant.I("summonerLevel"),Platform=platform};
}
public sealed class MatchPlayerConverter(Func<JsonNode,FrameworkElement> render):IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>value is JsonNode participant?render(participant):null;
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}
