using System.Text.Json.Nodes;

namespace RiftRadar;

public sealed class LiveSessionTracker
{
    bool active;
    int missed;
    double lastTime;
    public void Reset(){active=false;missed=0;lastTime=0;}
    public bool Observe(JsonNode? game)
    {
        if(game==null){if(++missed>=3)Reset();return false;}
        var time=game.N("gameTime");
        bool started=!active||time+60<lastTime;
        active=true;missed=0;lastTime=time;
        return started;
    }
}
