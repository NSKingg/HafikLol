using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RiftRadar;

public sealed partial class StaticData
{
    static readonly HttpClient iconHttp=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(8),MaxResponseContentBufferSize=1_000_000};
    readonly Dictionary<string,BitmapSource> profileImages=[];
    readonly Dictionary<string,Task<BitmapSource?>> profilePending=[];
    readonly Dictionary<string,DateTimeOffset> profileRetry=[];
    string ProfileKey(int id)=>Version+":"+id;
    string ProfilePath(int id)=>Path.Combine(store.Root,"assets",Version,"profileicons",id+".png");
    public BitmapSource? ProfileImage(int id)
    {
        if(id<0)return null;var key=ProfileKey(id);if(profileImages.TryGetValue(key,out var image))return image;
        try
        {
            var path=ProfilePath(id);if(!File.Exists(path))return null;
            var b=new BitmapImage();b.BeginInit();b.DecodePixelWidth=80;b.CacheOption=BitmapCacheOption.OnLoad;b.UriSource=new Uri(path);b.EndInit();b.Freeze();profileImages[key]=b;return b;
        }
        catch(Exception e)when(e is IOException or ArgumentException or NotSupportedException){return null;}
    }
    public Task<BitmapSource?> ProfileImageAsync(int id)
    {
        var image=ProfileImage(id);if(image!=null)return Task.FromResult<BitmapSource?>(image);
        if(id<0)return Task.FromResult<BitmapSource?>(null);
        var key=ProfileKey(id);
        if(profilePending.TryGetValue(key,out var pending))return pending;
        if(profileRetry.TryGetValue(key,out var retry)&&retry>DateTimeOffset.UtcNow)return Task.FromResult<BitmapSource?>(null);
        return profilePending[key]=DownloadProfile(id,key,Version,ProfilePath(id));
    }
    async Task<BitmapSource?> DownloadProfile(int id,string key,string version,string path)
    {
        try
        {
            var bytes=await iconHttp.GetByteArrayAsync($"https://ddragon.leagueoflegends.com/cdn/{version}/img/profileicon/{id}.png");
            if(bytes.Length<8||bytes[0]!=137||bytes[1]!=80||bytes[2]!=78||bytes[3]!=71)return null;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);Store.Atomic(path,bytes);return ProfileImage(id);
        }
        catch(Exception e)when(e is HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException)
        {profileRetry[key]=DateTimeOffset.UtcNow.AddMinutes(5);return null;}
        finally{profilePending.Remove(key);}
    }
}

public static partial class Ui
{
    public static FrameworkElement ProfilePortrait(StaticData data,Player player,int size=40)
    {
        var fallback=Text(string.IsNullOrEmpty(player.GameName)?"?":player.GameName[..1].ToUpperInvariant(),size*.4,Accent,true);
        fallback.HorizontalAlignment=HorizontalAlignment.Center;fallback.VerticalAlignment=VerticalAlignment.Center;
        var frame=new Border{Width=size,Height=size,CornerRadius=new CornerRadius(8),BorderBrush=Line,BorderThickness=new Thickness(1),Background=Panel,ClipToBounds=true,Child=fallback,Margin=new Thickness(0,0,11,0),ToolTip=player.RiotId+" · LoL profil simgesi"};
        void Show(BitmapSource? source){if(source!=null)frame.Child=new Image{Source=source,Stretch=Stretch.UniformToFill};}
        Show(data.ProfileImage(player.Icon));
        frame.Loaded+=async(_,_)=>{var source=await data.ProfileImageAsync(player.Icon);if(frame.IsLoaded)Show(source);};
        return frame;
    }
    public static DataTemplate PlayerTemplate(StaticData data)
    {
        var content=new FrameworkElementFactory(typeof(ContentControl));
        content.SetBinding(ContentControl.ContentProperty,new Binding{Converter=new PlayerPortraitConverter(data)});
        return new DataTemplate{VisualTree=content};
    }
}
public sealed class PlayerPortraitConverter(StaticData data):IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)
    {
        if(value is not Player p)return null;
        var name=Ui.Text(p.RiotId,12);name.VerticalAlignment=VerticalAlignment.Center;
        return Ui.Row(Ui.ProfilePortrait(data,p,25),name);
    }
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}
