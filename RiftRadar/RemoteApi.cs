using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace RiftRadar;

public sealed class RemoteApi : IDisposable
{
    readonly Store store;
    readonly HttpClient http;
    readonly string token;
    public string Address {get;}
    public bool HasKey {get;private set;}
    public int Requests {get;private set;}
    public int CacheHits {get;private set;}
    string FilePath=>Path.Combine(store.Root,"server.dpapi");
    public RemoteApi(Store store,string? address=null,string? invite=null,HttpMessageHandler? handler=null)
    {
        this.store=store;
        var restoring=address==null;
        if(address==null)
        {
            try{var saved=J.Parse(Encoding.UTF8.GetString(Secret.Unprotect(File.ReadAllBytes(FilePath))));address=saved.S("server");invite=saved.S("invite");}
            catch(Exception e)when(e is IOException or CryptographicException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException){address="";invite="";}
        }
        Address=address??"";token=invite??"";
        if(Address.Length>0)
        {try{Validate(Address,token);}catch(ApiException)when(restoring){Address="";token="";}}
        http=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(45),MaxResponseContentBufferSize=12_000_000};
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RiftRadar/2.0");
    }
    public static void Validate(string address,string invite)
    {
        if(!Uri.TryCreate(address,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Port!=443||uri.AbsolutePath!="/"||uri.Query.Length>0||uri.Fragment.Length>0||uri.UserInfo.Length>0||uri.HostNameType!=UriHostNameType.Dns||uri.IsLoopback)
            throw new ApiException(400,"Sunucu adresi https://sunucun.workers.dev biçiminde olmalı; yol veya sorgu içermemeli.");
        if(invite.Length<32||invite.Length>128||invite.StartsWith("RGAPI-",StringComparison.OrdinalIgnoreCase)||invite.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'&&c!='_'))
            throw new ApiException(400,"Riot anahtarı yerine sana verilen kişisel davet kodunu gir.");
    }
    public void Save()=>Store.Atomic(FilePath,Secret.Protect(Encoding.UTF8.GetBytes(new JsonObject{["server"]=Address,["invite"]=token}.ToJsonString())));
    public void Forget(){if(File.Exists(FilePath))File.Delete(FilePath);HasKey=false;}
    async Task<JsonNode?> Send(string route,object? body,CancellationToken ct)
    {
        if(Address.Length==0)throw new ApiException(401,"Ayarlar'dan sunucu bağlantı dosyanı aç veya adres ve davet kodunu gir.");
        using var request=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,new Uri(new Uri(Address),route));
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        if(body!=null)request.Content=JsonContent.Create(body);
        using var response=await http.SendAsync(request,ct);Requests++;
        if(response.StatusCode==HttpStatusCode.Unauthorized||response.StatusCode==HttpStatusCode.Forbidden)HasKey=false;
        if(!response.IsSuccessStatusCode)
        {
            var retry=response.Headers.RetryAfter?.Delta?.TotalSeconds;
            throw new ApiException((int)response.StatusCode,(int)response.StatusCode switch{401 or 403=>"Davet kodu geçersiz veya erişimin kaldırılmış. Sunucu sahibinden yeni bağlantı iste.",429=>$"Ortak istek sınırı doldu. {Math.Ceiling(retry??120):0} saniye sonra tekrar dene.",503=>"Sunucunun Riot anahtarı eksik veya hizmet geçici olarak hazır değil.",404=>"Oyuncu veya maç bulunamadı.",400=>"Bu sorgu sunucu tarafından desteklenmiyor.",_=>"Sunucuya bağlanılamadı. Biraz sonra tekrar dene."});
        }
        try{return J.Parse(await response.Content.ReadAsStringAsync(ct));}catch(System.Text.Json.JsonException){throw new ApiException(502,"Sunucudan beklenen yanıt alınamadı.");}
    }
    public async Task<bool> Check(CancellationToken ct)
    {
        if(Address.Length==0)return HasKey=false;
        var result=await Send("v1/status",null,ct);
        if(result.I("protocol")!=1)throw new ApiException(502,"Sunucu sürümü bu uygulamayla uyumlu değil.");
        return HasKey=result.B("ready");
    }
    public async Task<JsonNode?> Get(string host,string path,string method,TimeSpan ttl,CancellationToken ct,bool optional404=false)
    {
        RiotApi.Url(host,path);
        var cache="remote:"+Address+":"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))+":"+host+path;
        if(ttl>TimeSpan.Zero&&store.Cached(cache,ttl) is {} saved){try{CacheHits++;return J.Parse(saved);}catch(System.Text.Json.JsonException){}}
        JsonNode? result;
        try{result=await Send("v1/query",new{host,path},ct);}
        catch(ApiException e)when(e.Code==404&&optional404){return null;}
        HasKey=true;if(ttl>TimeSpan.Zero&&result!=null)store.Cache(cache,result.ToJsonString());return result;
    }
    public void Dispose()=>http.Dispose();
}
