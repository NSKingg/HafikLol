using System.Text.Json.Nodes;

namespace RiftRadar;

public sealed class Demo
{
    public List<Player> Players {get;}=[];
    public List<JsonNode> Matches {get;}=[];
    public Demo()
    {
        var names=new[]{"Efe","Kuzey","Deniz","Mert"};
        for(int i=0;i<names.Length;i++) Players.Add(new Player {Puuid="DEMO_"+i,GameName=names[i],TagLine="ÖRNEK",Level=210+i*38,Updated=DateTimeOffset.Now,Ranks=new JsonArray(new JsonObject{["queueType"]="RANKED_SOLO_5x5",["tier"]=i==0 ? "EMERALD" : "PLATINUM",["rank"]="II",["leaguePoints"]=64-i*12,["wins"]=86,["losses"]=62}),Mastery=new JsonArray(new JsonObject{["championId"]=103,["championLevel"]=12,["championPoints"]=145830})});
        var random=new Random(42); string[] champs=["Ahri","Jinx","Yasuo","Lux","Thresh","LeeSin","Kaisa","Garen","Ashe","Ekko"];
        for(int game=0;game<48;game++)
        {
            int q=(game%8) switch {4=>450,5=>2400,6=>1710,7=>440,_=>420};
            var duration=1200+random.Next(900); var parts=new JsonArray();
            for(int i=0;i<10;i++)
            {
                var p=new JsonObject { ["participantId"]=i+1,["puuid"]=i<4?Players[i].Puuid:"DEMO_ENEMY_"+i,["riotIdGameName"]=i<4?Players[i].GameName:"Örnek rakip "+(i-4),["riotIdTagline"]="ÖRNEK",["championName"]=champs[(game+i)%champs.Length],["teamId"]=i<5?100:200,["playerSubteamId"]=i/2+1,["placement"]=game%8+1,["win"]=(game%3!=0)==(i<5),["kills"]=random.Next(3,18),["deaths"]=random.Next(1,10),["assists"]=random.Next(4,24),["totalMinionsKilled"]=random.Next(70,240),["neutralMinionsKilled"]=random.Next(0,25),["totalDamageDealtToChampions"]=random.Next(14000,48000),["physicalDamageDealtToChampions"]=random.Next(1000,13000),["magicDamageDealtToChampions"]=random.Next(6000,20000),["trueDamageDealtToChampions"]=random.Next(200,1000),["totalDamageTaken"]=random.Next(14000,40000),["goldEarned"]=random.Next(10000,19000),["visionScore"]=random.Next(12,65),["wardsPlaced"]=random.Next(4,18),["wardsKilled"]=random.Next(0,8),["visionWardsBoughtInGame"]=random.Next(0,5),["teamPosition"]=new[]{"MIDDLE","BOTTOM","TOP","UTILITY","JUNGLE"}[i%5],["timeCCingOthers"]=random.Next(10,80),["totalHeal"]=random.Next(1000,6000),["totalHealsOnTeammates"]=random.Next(0,4000),["totalDamageShieldedOnTeammates"]=random.Next(0,2500),["damageDealtToBuildings"]=random.Next(500,6000),["damageDealtToObjectives"]=random.Next(1000,12000),["doubleKills"]=random.Next(0,4),["tripleKills"]=random.Next(0,2),["quadraKills"]=0,["pentaKills"]=0,["largestKillingSpree"]=random.Next(2,7),["champLevel"]=random.Next(14,19),["item0"]=3089,["item1"]=3020,["item2"]=random.Next(2)==0?3157:4645,["item3"]=3137,["item4"]=0,["item5"]=0,["item6"]=3363,["perks"]=new JsonObject{["styles"]=new JsonArray(new JsonObject{["style"]=8100,["selections"]=new JsonArray(new JsonObject{["perk"]=8112})})},["challenges"]=new JsonObject{["soloKills"]=random.Next(0,5),["skillshotsDodged"]=random.Next(4,45),["skillshotsHit"]=random.Next(8,60),["goldPerMinute"]=random.Next(330,550)} };
                parts.Add(p);
            }
            var id="DEMO_"+game;
            Matches.Add(new JsonObject { ["metadata"]=new JsonObject{["matchId"]=id},["info"]=new JsonObject{["queueId"]=q,["mapId"]=q is 450 or 2400?12:q==1710?30:11,["gameMode"]=q==1710?"CHERRY":q is 450 or 2400?"ARAM":"CLASSIC",["gameType"]="MATCHED_GAME",["gameVersion"]="16.20.1.1",["gameCreation"]=DateTimeOffset.Now.AddHours(-game*3-1).ToUnixTimeMilliseconds(),["gameDuration"]=duration,["participants"]=parts,["teams"]=new JsonArray(new JsonObject{["teamId"]=100,["objectives"]=new JsonObject{["dragon"]=new JsonObject{["kills"]=3},["baron"]=new JsonObject{["kills"]=1}}},new JsonObject{["teamId"]=200,["objectives"]=new JsonObject{["dragon"]=new JsonObject{["kills"]=1},["baron"]=new JsonObject{["kills"]=0}}})} });
            foreach(var p in Players) p.MatchIds.Add(id);
        }
    }
    public JsonNode LivePlayers() => new JsonArray(Enumerable.Range(0,10).Select(i=>(JsonNode)new JsonObject{["riotId"]=i<4?Players[i].RiotId:"Örnek rakip "+(i-4)+"#ÖRNEK",["championName"]=new[]{"Ahri","Jinx","Yasuo","Lux","Thresh","LeeSin","Kaisa","Garen","Ashe","Ekko"}[i],["team"]=i<5?"ORDER":"CHAOS",["level"]=12+i%3,["scores"]=new JsonObject{["kills"]=3+i,["deaths"]=i%5,["assists"]=8+i,["creepScore"]=140+i*8,["wardScore"]=14+i},["items"]=new JsonArray(new JsonObject{["displayName"]="Örnek eşya"}),["isDead"]=false}).ToArray());
}
