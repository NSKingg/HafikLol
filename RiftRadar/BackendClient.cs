using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;

namespace RiftRadar;

public sealed class BackendClient : RiotApi
{
    readonly string pipeName="RiftRadar-"+Guid.NewGuid().ToString("N");
    readonly Store store;
    readonly SemaphoreSlim channel=new(1,1);
    Process? process;
    bool hasKey;
    RemoteApi? remote;
    public bool ServerMode=>store.Preferences.UseServer;
    public string ServerAddress=>remote?.Address??"";
    public bool HasSavedServer=>!string.IsNullOrEmpty(remote?.Address);
    public override bool HasKey=>hasKey;
    public int BackendPid=>process?.Id??0;
    public BackendClient(Store store):base(store) {this.store=store;if(ServerMode)remote=new RemoteApi(store);}
    public async Task Start()
    {
        if(ServerMode){remote??=new RemoteApi(store);hasKey=await remote.Check(CancellationToken.None);return;}
        var dir=Path.Combine(store.Root,"runtime","1.0.0");Directory.CreateDirectory(dir);
        var file=Path.Combine(dir,"RiftRadar.Backend.exe");
        using var hashResource=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/backend.sha256"))!.Stream;
        using var hashReader=new StreamReader(hashResource);
        var resourceHash=Convert.FromHexString((await hashReader.ReadToEndAsync()).Trim());
        bool extract=true;
        if(File.Exists(file)){using var existing=File.OpenRead(file);extract=!(await SHA256.HashDataAsync(existing)).SequenceEqual(resourceHash);}
        if(extract)
        {
            var temp=file+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                using var resource=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/backend.bin.gz"))!.Stream;
                using var compressed=new GZipStream(resource,CompressionMode.Decompress);
                using(var output=File.Create(temp))await compressed.CopyToAsync(output);
                using(var verify=File.OpenRead(temp))if(!(await SHA256.HashDataAsync(verify)).SequenceEqual(resourceHash))throw new IOException("Backend integrity check failed");
                File.Move(temp,file,true);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        var start=new ProcessStartInfo(file){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=dir};
        start.ArgumentList.Add(pipeName);start.ArgumentList.Add(Environment.ProcessId.ToString());start.ArgumentList.Add(store.Root);
        process=Process.Start(start) ?? throw new IOException("Backend could not start");
        await Send(new JsonObject{["op"]="status"},CancellationToken.None);
    }
    async Task<JsonNode?> Send(JsonObject request,CancellationToken ct)
    {
        await channel.WaitAsync(ct);
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(4));
            using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(12000,timeout.Token);
            using var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true};
            using var reader=new StreamReader(pipe,Encoding.UTF8,false,4096,true);
            await writer.WriteLineAsync(request.ToJsonString().AsMemory(),timeout.Token);
            var text=await reader.ReadLineAsync(timeout.Token) ?? throw new IOException("Backend disconnected");
            if(text.Length>16_000_000)throw new IOException("Response too large");
            var response=J.Parse(text);hasKey=response.B("hasKey");Requests=response.I("requests");CacheHits=response.I("hits");
            if(!response.B("ok"))throw new ApiException(response.I("code"),response.S("error"));
            return response["data"]?.DeepClone();
        }
        finally {channel.Release();}
    }
    public override async Task<JsonNode?> Get(string host,string path,string method,TimeSpan ttl,CancellationToken ct,bool optional404=false)
    {
        if(ServerMode){remote??=new RemoteApi(store);try{return await remote.Get(host,path,method,ttl,ct,optional404);}finally{Requests=remote.Requests;CacheHits=remote.CacheHits;hasKey=remote.HasKey;}}
        return await Send(new JsonObject{["op"]="get",["host"]=host,["path"]=path,["method"]=method,["ttl"]=ttl.TotalSeconds,["optional"]=optional404},ct);
    }
    public async Task ConnectServer(string address,string token,CancellationToken ct)
    {
        if(string.IsNullOrEmpty(token)&&HasSavedServer&&string.Equals(address.TrimEnd('/'),ServerAddress.TrimEnd('/'),StringComparison.OrdinalIgnoreCase))
        {hasKey=await remote!.Check(ct);if(!hasKey)throw new ApiException(503,"Kayıtlı bağlantın korunuyor; sunucu şu anda hazır değil.");return;}
        var candidate=new RemoteApi(store,address,token);
        try{if(!await candidate.Check(ct))throw new ApiException(503,"Sunucuda Riot bağlantısı henüz hazır değil.");candidate.Save();}
        catch{candidate.Dispose();throw;}
        remote?.Dispose();remote=candidate;store.Preferences.UseServer=true;store.Save();hasKey=true;
    }
    public void DisconnectServer(){remote?.Forget();remote?.Dispose();remote=new RemoteApi(store);hasKey=false;}
    public override async Task Configure(string key,bool remember) => await Send(new JsonObject{["op"]="configure",["key"]=key,["remember"]=remember},CancellationToken.None);
    public async Task Stop()
    {
        if(ServerMode){remote?.Dispose();Dispose();return;}
        try {using var ct=new CancellationTokenSource(1500);await Send(new JsonObject{["op"]="shutdown"},ct.Token);}catch{}
        if(process!=null) {try {await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));}catch{try{process.Kill();}catch{}} process.Dispose();}
        Dispose();
    }
}
