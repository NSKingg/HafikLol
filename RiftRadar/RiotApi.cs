using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace RiftRadar;

public sealed class RateGate
{
    readonly Queue<DateTimeOffset> calls = new();
    readonly Dictionary<string,DateTimeOffset> blocks = new();
    public DateTimeOffset Cooldown {get; private set;}
    public TimeSpan Delay(string host, string method, DateTimeOffset now)
    {
        while(calls.Count>0 && now-calls.Peek() >= TimeSpan.FromSeconds(120)) calls.Dequeue();
        var wait=Cooldown;
        if(calls.Count>=90) wait=new[]{wait,calls.Peek().AddSeconds(120.1)}.Max();
        var last=calls.Where(t=>now-t<TimeSpan.FromSeconds(1)).ToArray();
        if(last.Length>=15) wait=new[]{wait,last[0].AddMilliseconds(1050)}.Max();
        foreach(var k in new[]{host,host+method}) if(blocks.TryGetValue(k,out var b) && b>wait) wait=b;
        return wait>now ? wait-now : TimeSpan.Zero;
    }
    public void Record(DateTimeOffset now) => calls.Enqueue(now);
    public void Block(TimeSpan time) => Cooldown=DateTimeOffset.UtcNow+time;
    public void Observe(HttpResponseMessage response, string host, string method)
    {
        foreach(var prefix in new[]{"App","Method"})
        {
            if(!response.Headers.TryGetValues($"X-{prefix}-Rate-Limit",out var ls) || !response.Headers.TryGetValues($"X-{prefix}-Rate-Limit-Count",out var cs)) continue;
            var limits=Pairs(string.Join(',',ls)); var counts=Pairs(string.Join(',',cs));
            foreach(var entry in limits)
                if(counts.TryGetValue(entry.Key,out var count) && count>=entry.Value)
                { var key=prefix=="App" ? host : host+method; var until=DateTimeOffset.UtcNow.AddSeconds(entry.Key+0.1); if(!blocks.TryGetValue(key,out var old) || old<until) blocks[key]=until; }
        }
    }
    static Dictionary<double,double> Pairs(string text)
    {
        var result=new Dictionary<double,double>();
        foreach(var v in text.Split(',')) { var a=v.Split(':'); if(a.Length==2 && double.TryParse(a[0],System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out var n) && double.TryParse(a[1],System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out var s)) result[s]=n; }
        return result;
    }
}
public class RiotApi : IDisposable
{
    readonly HttpClient http;
    readonly HttpClient local;
    readonly Store store;
    readonly SemaphoreSlim serial = new(1,1);
    public RateGate Gate {get;}=new();
    public string ApiKey {get;set;}="";
    public virtual bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
    public int Requests {get;protected set;}
    public int CacheHits {get;protected set;}
    public int LastHistoryCount {get;private set;}
    public string HistoryWarning {get;private set;}="";
    public RiotApi(Store store, HttpMessageHandler? handler=null)
    {
        this.store=store;
        http=new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect=false, AutomaticDecompression=DecompressionMethods.All }) {Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=12_000_000};
        // Only this isolated client accepts the game's self-signed loopback certificate.
        local=new HttpClient(new HttpClientHandler {UseProxy=false,AllowAutoRedirect=false,ServerCertificateCustomValidationCallback=(request,_,_,_)=>request.RequestUri?.Host=="127.0.0.1" && request.RequestUri.Port==2999}) {Timeout=TimeSpan.FromSeconds(2),MaxResponseContentBufferSize=4_000_000};
    }
    public static Uri Url(string host, string path)
    {
        var valid=Region.All.Any(x=>x.Platform==host || x.Cluster==host);
        if(!valid || !path.StartsWith('/') || path.Contains("://")) throw new ArgumentException("Invalid route");
        return new Uri("https://"+host+".api.riotgames.com"+path);
    }
    public static (string Name,string Tag) SplitId(string text)
    {
        var i=text.LastIndexOf('#');
        if(i<1 || i==text.Length-1 || text.Length>100) throw new ApiException(400,"Riot ID'yi İsim#Etiket biçiminde gir. Örnek: Oyuncu#TR1");
        return(text[..i].Trim(),text[(i+1)..].Trim());
    }
    public virtual async Task<JsonNode?> Get(string host,string path,string method,TimeSpan ttl,CancellationToken ct, bool optional404=false)
    {
        var uri=Url(host,path); var cacheKey=uri.ToString();
        if(ttl>TimeSpan.Zero && store.Cached(cacheKey,ttl) is {} cached)
        { try { var parsed=J.Parse(cached); CacheHits++; return parsed; } catch(System.Text.Json.JsonException) {} }
        if(string.IsNullOrWhiteSpace(ApiKey)) throw new ApiException(401,"Önce Ayarlar'dan Riot API anahtarını ekle.");
        await serial.WaitAsync(ct);
        try
        {
            for(int attempt=0;attempt<2;attempt++)
            {
                var delay=Gate.Delay(host,method,DateTimeOffset.UtcNow);
                if(delay>TimeSpan.Zero) await Task.Delay(delay,ct);
                ct.ThrowIfCancellationRequested(); Gate.Record(DateTimeOffset.UtcNow);
                using var request=new HttpRequestMessage(HttpMethod.Get,uri);
                request.Headers.Add("X-Riot-Token",ApiKey);
                using var response=await http.SendAsync(request,ct); Requests++;
                Gate.Observe(response,host,method);
                var code=(int)response.StatusCode;
                if(code==404 && optional404) return null;
                if(code==429)
                {
                    var retry=response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date is {} date ? date-DateTimeOffset.UtcNow : TimeSpan.FromSeconds(120));
                    retry=retry<TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : retry;
                    Gate.Block(retry); throw new ApiException(429,$"Riot istek sınırına ulaşıldı. {Math.Ceiling(retry.TotalSeconds):0} saniye sonra tekrar dene.");
                }
                if(code>=500 && attempt==0) { await Task.Delay(800,ct); continue; }
                if(!response.IsSuccessStatusCode) throw new ApiException(code,code switch {401=>"Riot API anahtarı eksik veya geçersiz.",403=>"Riot erişimi reddetti. Anahtarın süresi veya bu moda erişim izni bitmiş olabilir; anahtarı ve uç noktayı kontrol et.",404=>"Oyuncu veya maç bulunamadı. Riot ID ve sunucuyu kontrol et.",>=500=>"Riot servisi şu anda kullanılamıyor. Biraz sonra tekrar dene.",_=>$"Riot isteği tamamlanamadı (HTTP {code})."});
                var text=await response.Content.ReadAsStringAsync(ct);
                JsonNode parsed;
                try { parsed=J.Parse(text); } catch(System.Text.Json.JsonException) { throw new ApiException(502,"Riot beklenmeyen bir veri biçimi döndürdü."); }
                if(ttl>TimeSpan.Zero) store.Cache(cacheKey,text);
                return parsed;
            }
            return null;
        }
        finally { serial.Release(); }
    }
    public Task<JsonNode?> Account(string id, Region region,CancellationToken ct)
    { var (name,tag)=SplitId(id); return Get(region.Cluster=="sea"?"asia":region.Cluster,$"/riot/account/v1/accounts/by-riot-id/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(tag)}","account",TimeSpan.FromHours(6),ct); }
    public Task<JsonNode?> Summoner(string puuid,Region r,CancellationToken ct) => Get(r.Platform,"/lol/summoner/v4/summoners/by-puuid/"+Uri.EscapeDataString(puuid),"summoner",TimeSpan.FromMinutes(10),ct);
    public Task<JsonNode?> Ranks(string puuid,Region r,CancellationToken ct) => Get(r.Platform,"/lol/league/v4/entries/by-puuid/"+Uri.EscapeDataString(puuid),"league",TimeSpan.FromMinutes(5),ct);
    public Task<JsonNode?> Mastery(string puuid,Region r,CancellationToken ct) => Get(r.Platform,"/lol/champion-mastery/v4/champion-masteries/by-puuid/"+Uri.EscapeDataString(puuid)+"/top?count=12","mastery",TimeSpan.FromHours(1),ct);
    public Task<JsonNode?> Spectator(string puuid,Region r,CancellationToken ct) => Get(r.Platform,"/lol/spectator/v5/active-games/by-summoner/"+Uri.EscapeDataString(puuid),"spectator",TimeSpan.Zero,ct,true);
    public Task<JsonNode?> Timeline(string id,Region r,CancellationToken ct) => Get(r.Cluster,"/lol/match/v5/matches/"+Uri.EscapeDataString(id)+"/timeline","timeline",TimeSpan.FromDays(180),ct);
    public Task<JsonNode?> Status(Region r,CancellationToken ct) => Get(r.Platform,"/lol/status/v4/platform-data","status",TimeSpan.FromMinutes(5),ct);
    public virtual Task Configure(string key,bool remember) {store.SaveKey(key,remember);ApiKey=key;return Task.CompletedTask;}
    public async Task<List<JsonNode>> History(Player player,int start,int count,IProgress<string>? progress,CancellationToken ct)
    {
        var r=Region.Find(player.Platform);
        var ids=await Get(r.Cluster,$"/lol/match/v5/matches/by-puuid/{Uri.EscapeDataString(player.Puuid)}/ids?start={start}&count={Math.Clamp(count,1,100)}","matchids",TimeSpan.FromSeconds(60),ct);
        LastHistoryCount=0;HistoryWarning="";
        var result=new List<JsonNode>(); var warnings=new List<string>();
        foreach(var id in ids.Rows().Select(x=>x.ToString()))
        {
            if(ct.IsCancellationRequested && result.Count>0){HistoryWarning="İşlem iptal edildi; tamamlanan maçlar korundu.";break;}
            ct.ThrowIfCancellationRequested(); progress?.Report($"Maçlar okunuyor · {LastHistoryCount+1}/{ids.Rows().Count()}");
            JsonNode? match=null;
            if(store.Cached("match:"+id,TimeSpan.FromDays(180)) is {} cached) { try { match=J.Parse(cached); CacheHits++; } catch(System.Text.Json.JsonException) {} }
            try { match ??= await Get(r.Cluster,"/lol/match/v5/matches/"+Uri.EscapeDataString(id),"match",TimeSpan.Zero,ct); }
            catch(ApiException ex) when(ex.Code==404) { warnings.Add(id);LastHistoryCount++;continue; }
            catch(ApiException ex) when(result.Count>0){HistoryWarning=ex.Message+" Tamamlanan maçlar korundu.";break;}
            catch(OperationCanceledException) when(result.Count>0){HistoryWarning="İşlem durduruldu; tamamlanan maçlar korundu.";break;}
            if(match!=null && Modes.PublicMatch(match)) { store.SaveMatch(match); result.Add(match); }
            LastHistoryCount++;
        }
        if(warnings.Count>0)HistoryWarning+=$" {warnings.Count} eski maç Riot tarafından bulunamadı.";
        return result;
    }
    public async Task<JsonNode?> Live(string endpoint,CancellationToken ct)
    {
        if(endpoint is not ("playerlist" or "gamestats" or "eventdata")) throw new ArgumentException("Endpoint not allowed");
        try
        {
            using var response=await local.GetAsync("https://127.0.0.1:2999/liveclientdata/"+endpoint,ct);
            return response.IsSuccessStatusCode ? J.Parse(await response.Content.ReadAsStringAsync(ct)) : null;
        }
        catch(HttpRequestException) {return null;}
        catch(TaskCanceledException) when(!ct.IsCancellationRequested) {return null;}
    }
    public void Dispose() {http.Dispose();local.Dispose();}
}
