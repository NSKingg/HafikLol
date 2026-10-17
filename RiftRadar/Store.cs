using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RiftRadar;

public sealed class Store
{
    public string Root { get; }
    public string CacheDir => Path.Combine(Root, "cache");
    public Preferences Preferences { get; }
    public List<Player> Players { get; }
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    readonly object sync = new();
    public Store(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiftRadar");
        Directory.CreateDirectory(Root); Directory.CreateDirectory(CacheDir);
        Preferences = Read<Preferences>("settings.json") ?? new();
        Players = Read<List<Player>>("players.json") ?? [];
        if (!Region.All.Any(x => x.Platform == Preferences.Platform)) Preferences.Platform = "tr1";
        Preferences.MatchCount = Math.Clamp(Preferences.MatchCount, 10, 100);
        Preferences.PollSeconds = Math.Clamp(Preferences.PollSeconds, 3, 30);
        Prune();
    }
    T? Read<T>(string file)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Root,file))); }
        catch (Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { return default; }
    }
    public void Save()
    {
        lock(sync)
        {
            Atomic(Path.Combine(Root,"settings.json"), Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Preferences, Json)));
            Atomic(Path.Combine(Root,"players.json"), Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Players, Json)));
        }
    }
    public JsonArray LoadBuilds() => Read<JsonArray>("builds.json") ?? [];
    public void RememberPlayer(Player player)
    {
        var existing=Players.FindIndex(p=>p.Key==player.Key);
        if(existing<0)Players.Add(player);
        else if(!ReferenceEquals(Players[existing],player))
        {
            player.Note=Players[existing].Note;
            player.MatchIds=player.MatchIds.Concat(Players[existing].MatchIds).Distinct().Take(500).ToList();
            Players[existing]=player;
        }
        player.LastViewed=DateTimeOffset.Now;Preferences.Selected=player.Key;Save();
    }
    public void SaveBuilds(JsonArray builds) => Atomic(Path.Combine(Root,"builds.json"),Encoding.UTF8.GetBytes(builds.ToJsonString()));
    public static void Atomic(string path, byte[] bytes)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try { File.WriteAllBytes(temp, bytes); File.Move(temp,path,true); }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    public string? Cached(string key, TimeSpan ttl)
    {
        var path = Path.Combine(CacheDir, Hash(key)+".gz");
        try
        {
            if(!File.Exists(path) || DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > ttl) return null;
            using var f = File.OpenRead(path); using var z = new GZipStream(f,CompressionMode.Decompress); using var r = new StreamReader(z);
            var s=r.ReadToEnd(); return s.Length<12_000_000 ? s : null;
        }
        catch(Exception e) when(e is IOException or InvalidDataException) { return null; }
    }
    public void Cache(string key, string json)
    {
        using var data = new MemoryStream();
        using(var z=new GZipStream(data,CompressionLevel.Fastest,true)) { var bytes=Encoding.UTF8.GetBytes(json); z.Write(bytes); }
        lock(sync) Atomic(Path.Combine(CacheDir,Hash(key)+".gz"),data.ToArray());
    }
    public List<JsonNode> Matches(Player player) => player.MatchIds.Select(id=>Cached("match:"+id,TimeSpan.FromDays(365))).Where(s=>s!=null).Select(s=> { try { return J.Parse(s!); } catch(JsonException) { return null; } }).OfType<JsonNode>().ToList();
    public void SaveMatch(JsonNode match)
    {
        // Custom games are deliberately excluded from stored/shared history.
        if(Modes.PublicMatch(match)) Cache("match:"+match["metadata"].S("matchId"),match.ToJsonString());
    }
    public long CacheBytes => Directory.EnumerateFiles(CacheDir,"*.gz").Sum(x=>new FileInfo(x).Length);
    public void ClearCache() { lock(sync) foreach(var f in Directory.EnumerateFiles(CacheDir)) File.Delete(f); }
    public void Prune()
    {
        long bytes=0;
        foreach(var f in new DirectoryInfo(CacheDir).GetFiles("*.gz").OrderByDescending(f=>f.LastWriteTimeUtc))
        { bytes+=f.Length; if(bytes>128L*1024*1024 || DateTime.UtcNow-f.LastWriteTimeUtc>TimeSpan.FromDays(180)) f.Delete(); }
    }
    public string LoadKey()
    {
        try { return Encoding.UTF8.GetString(Secret.Unprotect(File.ReadAllBytes(Path.Combine(Root,"key.dpapi")))); }
        catch(Exception e) when(e is IOException or System.ComponentModel.Win32Exception or CryptographicException) { return ""; }
    }
    public void SaveKey(string key, bool remember)
    {
        var path=Path.Combine(Root,"key.dpapi");
        if(!remember || key.Length==0) { if(File.Exists(path)) File.Delete(path); return; }
        Atomic(path,Secret.Protect(Encoding.UTF8.GetBytes(key)));
    }
}
public static class Secret
{
    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern bool CryptProtectData(ref Blob data, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob result);
    [DllImport("crypt32.dll", SetLastError=true)] static extern bool CryptUnprotectData(ref Blob data, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob result);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr ptr);
    static byte[] Transform(byte[] bytes, bool encrypt)
    {
        var input = new Blob {Size=bytes.Length, Data=Marshal.AllocHGlobal(bytes.Length)};
        var output = new Blob();
        try
        {
            Marshal.Copy(bytes,0,input.Data,bytes.Length);
            var ok=encrypt ? CryptProtectData(ref input,"Rift Radar API key",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output) : CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var result=new byte[output.Size]; Marshal.Copy(output.Data,result,0,result.Length); return result;
        }
        finally { for(int i=0;i<input.Size;i++) Marshal.WriteByte(input.Data,i,0); Marshal.FreeHGlobal(input.Data); if(output.Data!=IntPtr.Zero) LocalFree(output.Data); }
    }
    public static byte[] Protect(byte[] b) => Transform(b,true);
    public static byte[] Unprotect(byte[] b) => Transform(b,false);
}
