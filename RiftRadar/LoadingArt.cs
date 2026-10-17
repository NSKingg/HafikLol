using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace RiftRadar;

public sealed partial class StaticData
{
    static readonly HttpClient artHttp=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(8),MaxResponseContentBufferSize=2*1024*1024};
    readonly Dictionary<string,BitmapSource> loadingArt=[];
    readonly Dictionary<string,Task<BitmapSource?>> loadingTasks=[];
    public Task<BitmapSource?> LoadingArt(string champion)
    {
        var code=Champion(champion).S("id");
        if(!Regex.IsMatch(code,"^[A-Za-z0-9]+$"))return Task.FromResult<BitmapSource?>(null);
        if(loadingArt.TryGetValue(code,out var image))return Task.FromResult<BitmapSource?>(image);
        if(loadingTasks.TryGetValue(code,out var task))return task;
        var pending=ReadLoadingArt(code);loadingTasks[code]=pending;return pending;
    }
    async Task<BitmapSource?> ReadLoadingArt(string code)
    {
        try
        {
            var path=Path.Combine(store.Root,"assets",Version,"loading",code+".jpg");byte[] bytes;
            if(File.Exists(path))bytes=await File.ReadAllBytesAsync(path);
            else{bytes=await artHttp.GetByteArrayAsync("https://ddragon.leagueoflegends.com/cdn/img/champion/loading/"+code+"_0.jpg");Directory.CreateDirectory(Path.GetDirectoryName(path)!);Store.Atomic(path,bytes);}
            using var stream=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.DecodePixelWidth=240;bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
            if(loadingArt.Count>=24)loadingArt.Remove(loadingArt.Keys.First());loadingArt[code]=bitmap;return bitmap;
        }
        catch(Exception ex)when(ex is HttpRequestException or IOException or OperationCanceledException or NotSupportedException or FormatException or ArgumentException){return null;}
        finally{loadingTasks.Remove(code);}
    }
}
