using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using RiftRadar;

if(args.Length!=3 || !args[0].StartsWith("RiftRadar-",StringComparison.Ordinal) || !int.TryParse(args[1],out var parentId)) return;
using var lifetime=new CancellationTokenSource();
try
{
    var parent=Process.GetProcessById(parentId);
    _=Task.Run(async()=>{try{await parent.WaitForExitAsync();}catch{}finally{lifetime.Cancel();}});
    var store=new Store(args[2]);
    using var api=new RiotApi(store){ApiKey=store.LoadKey()};
    while(!lifetime.IsCancellationRequested)
    {
        using var pipe=new NamedPipeServerStream(args[0],PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.WaitForConnectionAsync(lifetime.Token);
            using var reader=new StreamReader(pipe,Encoding.UTF8,false,4096,true);
            using var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true};
            // Bound every connection and reject oversized requests before parsing.
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); deadline.CancelAfter(TimeSpan.FromMinutes(4));
            var buffer=new char[16384]; int length=0; bool finished=false;
            while(length<buffer.Length)
            { var read=await reader.ReadAsync(buffer.AsMemory(length,1),deadline.Token); if(read==0)break; if(buffer[length]=='\n'){finished=true;break;}length++; }
            if(!finished) continue;
            _=Task.Run(async()=>
            {
                try {var probe=new byte[1];if(await pipe.ReadAsync(probe,deadline.Token)==0)deadline.Cancel();}
                catch(Exception e) when(e is IOException or OperationCanceledException or ObjectDisposedException){}
            });
            var request=J.Parse(new string(buffer,0,length)); JsonNode? data=null; int code=0; string error="";
            try
            {
                switch(request.S("op"))
                {
                    case "status": break;
                    case "configure":
                        var key=request.S("key").Trim();
                        if(key.Length>200 || (key.Length>0 && (!key.StartsWith("RGAPI-") || key.Any(char.IsWhiteSpace)))) throw new ApiException(400,"API anahtarı RGAPI- ile başlamalı ve boşluk içermemeli.");
                        await api.Configure(key,request.B("remember")); break;
                    case "get":
                        var path=request.S("path"); var method=request.S("method");
                        var prefixes=new Dictionary<string,string>{{"account","/riot/account/v1/accounts/by-riot-id/"},{"summoner","/lol/summoner/v4/summoners/by-puuid/"},{"league","/lol/league/v4/entries/by-puuid/"},{"mastery","/lol/champion-mastery/v4/champion-masteries/by-puuid/"},{"spectator","/lol/spectator/v5/active-games/by-summoner/"},{"matchids","/lol/match/v5/matches/by-puuid/"},{"match","/lol/match/v5/matches/"},{"timeline","/lol/match/v5/matches/"},{"status","/lol/status/v4/platform-data"}};
                        if(!prefixes.TryGetValue(method,out var prefix) || !path.StartsWith(prefix,StringComparison.Ordinal) || path.Contains("..") || path.Contains('\\') || path.Length>2000) throw new ApiException(400,"İzin verilmeyen istek.");
                        data=await api.Get(request.S("host"),path,method,TimeSpan.FromSeconds(Math.Clamp(request.N("ttl"),0,86400*180)),deadline.Token,request.B("optional")); break;
                    case "shutdown": lifetime.Cancel(); break;
                    default: throw new ApiException(400,"Bilinmeyen işlem.");
                }
            }
            catch(Exception e) {code=e is ApiException a ? a.Code : 500;error=SafeError.Message(e);}
            var response=new JsonObject{["ok"]=code==0,["code"]=code,["error"]=error,["hasKey"]=api.HasKey,["requests"]=api.Requests,["hits"]=api.CacheHits,["data"]=data};
            await writer.WriteLineAsync(response.ToJsonString().AsMemory(),lifetime.Token);
            deadline.Cancel();
        }
        catch(Exception e) when(e is OperationCanceledException or IOException or System.Text.Json.JsonException) {}
    }
}
catch { /* The client reports startup failure without leaking credentials. */ }
