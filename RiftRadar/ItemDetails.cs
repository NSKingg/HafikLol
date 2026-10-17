using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RiftRadar;

public static class ItemDetails
{
    public static IReadOnlyList<(string Amount,string Name)> Stats(ItemRow item)
    {
        var block=Regex.Match(item.Raw.S("description"),@"<stats>(.*?)</stats>",RegexOptions.Singleline|RegexOptions.IgnoreCase);
        if(!block.Success)return [];
        return J.Strip(block.Groups[1].Value).Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(line=>
        {var m=Regex.Match(line,@"^(?<number>[%+\-]?[\d.,]+%?)\s*(?<name>.+)$");return m.Success?(m.Groups["number"].Value,m.Groups["name"].Value):("",line);}).ToList();
    }
    public static FrameworkElement View(StaticData data,ItemRow item)
    {
        var title=Ui.Row(Ui.ItemIcon(data,item.Id,48),Ui.Stack(Ui.Text(item.Name,18,null,true),Ui.StatLabel(item.Gold.ToString("N0")+" altın",StatTheme.Gold)));
        var panel=Ui.Stack(title,Ui.Space(12));var html=item.Raw.S("description");
        var block=Regex.Match(html,@"<stats>(.*?)</stats>",RegexOptions.Singleline|RegexOptions.IgnoreCase);
        if(block.Success)
        {
            panel.Children.Add(Ui.Caption("VERDİĞİ STATLAR"));var chips=new WrapPanel();foreach(var stat in Stats(item))
            {
                chips.Children.Add(Ui.StatChip(stat.Name,stat.Amount,stat.Name));
            }
            panel.Children.Add(chips);html=html.Remove(block.Index,block.Length);
        }
        if(!string.IsNullOrWhiteSpace(J.Strip(html)))
        {panel.Children.Add(Ui.Space(5));panel.Children.Add(Ui.Caption("PASİFLER / AKTİF ETKİLER"));panel.Children.Add(Description(html));}
        else panel.Children.Add(Ui.Text("Ayrı bir pasif/aktif açıklaması bulunmuyor.",12,Ui.Muted));
        var components=item.Raw.A("from").Select(x=>int.TryParse(x.ToString(),out var n)?n:0).Where(x=>x>0).ToList();
        if(components.Count>0){panel.Children.Add(Ui.Space(12));panel.Children.Add(Ui.Caption("BİLEŞENLER"));panel.Children.Add(Ui.ItemStrip(data,components,true,28));}
        return panel;
    }
    public static TextBlock Description(string html)
    {
        var text=new TextBlock{TextWrapping=TextWrapping.Wrap,FontSize=13,LineHeight=21,Foreground=Ui.Muted};
        var colors=new Stack<Brush>();colors.Push(Ui.Muted);
        var known=new Dictionary<string,Brush>(StringComparer.OrdinalIgnoreCase){["scaleAP"]=StatTheme.AP.Color,["magicDamage"]=StatTheme.AP.Color,["scaleAD"]=StatTheme.AD.Color,["physicalDamage"]=StatTheme.AD.Color,["trueDamage"]=StatTheme.Crit.Color,["healing"]=StatTheme.HealthRegen.Color,["scaleHealth"]=StatTheme.Health.Color,["health"]=StatTheme.Health.Color,["scaleMana"]=StatTheme.Mana.Color,["mana"]=StatTheme.Mana.Color,["speed"]=StatTheme.Movement.Color,["attackSpeed"]=StatTheme.AttackSpeed.Color,["shield"]=StatTheme.Shield.Color,["passive"]=Ui.Brush("#E1D3AF"),["active"]=Ui.Brush("#E1D3AF"),["attention"]=Ui.Brush("#ECECEF")};
        foreach(Match token in Regex.Matches(html,@"<(?<closing>/)?(?<tag>\w+)[^>]*>|(?<text>[^<]+)"))
        {
            if(token.Groups["text"].Success){text.Inlines.Add(new Run(WebUtility.HtmlDecode(token.Groups["text"].Value)){Foreground=colors.Peek()});continue;}
            var tag=token.Groups["tag"].Value;
            if(tag.Equals("br",StringComparison.OrdinalIgnoreCase)){text.Inlines.Add(new LineBreak());continue;}
            if(token.Groups["closing"].Success){if(colors.Count>1)colors.Pop();}
            else colors.Push(known.GetValueOrDefault(tag,colors.Peek()));
        }
        return text;
    }
}
