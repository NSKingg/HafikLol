using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RiftRadar;
public static class RankVisuals
{
    static readonly string[] tiers="IRON BRONZE SILVER GOLD PLATINUM EMERALD DIAMOND MASTER GRANDMASTER CHALLENGER".Split(' ');
    static readonly string[] names={"Demir","Bronz","Gümüş","Altın","Platin","Zümrüt","Elmas","Ustalık","Üstatlık","Şampiyonluk"};
    static readonly string[] colors={"#9A8D86","#B9855D","#C6D3DC","#E4BE63","#63D3C8","#59D49C","#91B6FF","#C590ED","#F07A76","#EBD38B"};
    static readonly Dictionary<string,BitmapSource> images=[];
    public static JsonNode? Entry(Player player,string queue="RANKED_SOLO_5x5")=>player.Ranks.Rows().FirstOrDefault(x=>x.S("queueType")==queue);
    public static string Tier(JsonNode? rank)=>tiers.Contains(rank.S("tier"))?rank.S("tier"):"UNRANKED";
    public static Brush Color(JsonNode? rank){var i=Array.IndexOf(tiers,Tier(rank));var brush=Ui.Brush(i<0?"#9AA3B1":colors[i]);return brush;}
    public static string Label(JsonNode? rank){var i=Array.IndexOf(tiers,Tier(rank));return i<0?"Derecesiz":names[i]+(i<7?" "+rank.S("rank"):"")+" · "+rank.I("leaguePoints")+" LP";}
    public static string QueueName(string queue)=>queue switch{"RANKED_SOLO_5x5"=>"Tek / Çift","RANKED_FLEX_SR"=>"Esnek",_=>queue};
    public static BitmapSource Image(JsonNode? rank)
    {
        var key=Tier(rank).ToLowerInvariant();if(images.TryGetValue(key,out var cached))return cached;
        var bitmap=new BitmapImage(new Uri($"pack://application:,,,/Assets/Ranks/{key}.png"));
        var rgba=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);int width=rgba.PixelWidth,height=rgba.PixelHeight,stride=width*4;var pixels=new byte[stride*height];rgba.CopyPixels(pixels,stride,0);
        int left=width,top=height,right=0,bottom=0;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[y*stride+x*4+3]>4){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
        BitmapSource result=bitmap;
        if(left<=right&&top<=bottom){var cropped=new CroppedBitmap(bitmap,new Int32Rect(left,top,right-left+1,bottom-top+1));var scale=Math.Min(1,128d/Math.Max(cropped.PixelWidth,cropped.PixelHeight));result=new TransformedBitmap(cropped,new ScaleTransform(scale,scale));}
        result.Freeze();images[key]=result;return result;
    }
}
public static partial class Ui
{
    public static FrameworkElement RankBadge(Player player,string queue="RANKED_SOLO_5x5",int size=36)=>RankBadge(RankVisuals.Entry(player,queue),size);
    public static FrameworkElement RankBadge(JsonNode? rank,int size=36)
    {
        var label=Text(RankVisuals.Label(rank),12,RankVisuals.Color(rank),true);label.VerticalAlignment=VerticalAlignment.Center;
        return Row(new Image{Source=RankVisuals.Image(rank),Width=size,Height=size,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,ToolTip=RankVisuals.Label(rank)},label);
    }
}
