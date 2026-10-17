using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.Windows.Data;

namespace RiftRadar;

public static class GameVisuals
{
    static readonly Dictionary<int,BitmapSource> masteries=[];
    static readonly Dictionary<string,ImageSource> roles=[];
    public static int MasteryAssetLevel(int level)=>level<4?0:Math.Min(level,10);
    public static BitmapSource Mastery(int level)
    {
        var key=MasteryAssetLevel(level);if(masteries.TryGetValue(key,out var cached))return cached;
        var bitmap=new BitmapImage(new Uri($"pack://application:,,,/Assets/Mastery/level-{key}.png"));bitmap.Freeze();masteries[key]=bitmap;return bitmap;
    }
    public static string RoleName(string key)=>key switch{"TOP"=>"Üst koridor","JUNGLE"=>"Orman","MIDDLE"=>"Orta koridor","BOTTOM"=>"Alt koridor","UTILITY"=>"Destek","ARAM"=>"ARAM · rol yok","ARENA"=>"Arena · rol yok",_=>"Rol belirtilmemiş"};
    public static ImageSource RoleIcon(string key)
    {
        var name=key.ToLowerInvariant();if(name is not ("top" or "jungle" or "middle" or "bottom" or "utility"))return StatTheme.Generic.Image;
        if(roles.TryGetValue(name,out var cached))return cached;
        using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Roles/"+name+".svg"))!.Stream;
        var svg=XDocument.Load(stream);var group=new DrawingGroup();
        foreach(var node in svg.Root!.Elements())
        {
            Geometry? geometry=null;
            if(node.Name.LocalName=="path")geometry=Geometry.Parse(node.Attribute("d")!.Value);
            else if(node.Name.LocalName=="polygon")
            {var numbers=node.Attribute("points")!.Value.Split(new[]{' ',','},StringSplitOptions.RemoveEmptyEntries).Select(n=>double.Parse(n,CultureInfo.InvariantCulture)).ToArray();geometry=Geometry.Parse("M "+string.Join(" ",numbers.Select(n=>n.ToString(CultureInfo.InvariantCulture)))+" Z");}
            if(geometry==null)continue;
            var brush=Ui.Brush(node.Attribute("fill")?.Value??"#edeeee");
            if(node.Attribute("opacity") is {} opacity)brush.Opacity=double.Parse(opacity.Value,CultureInfo.InvariantCulture);
            group.Children.Add(new GeometryDrawing(brush,null,geometry));
        }
        group.ClipGeometry=new RectangleGeometry(new Rect(0,0,34,34));var image=new DrawingImage(group);image.Freeze();roles[name]=image;return image;
    }
}

public static partial class Ui
{
    public static FrameworkElement? MasteryIcon(JsonNode? mastery,int size=30)
    {
        if(mastery==null||mastery.I("championLevel")<=0)return null;
        return new Image{Source=GameVisuals.Mastery(mastery.I("championLevel")),Width=size,Height=size,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,ToolTip=$"Ustalık {mastery.I("championLevel")} · {mastery.N("championPoints"):N0} puan"};
    }
    public static FrameworkElement MasteryBadge(JsonNode? mastery,bool points=true,int size=34)
    {
        var icon=MasteryIcon(mastery,size);if(icon==null)return new Border{Visibility=Visibility.Collapsed};
        var text=Text($"Ustalık {mastery!.I("championLevel")}"+(points?$" · {mastery.N("championPoints"):N0} puan":""),12,Brush("#FFFFFF"),true);text.VerticalAlignment=VerticalAlignment.Center;
        return Row(icon,text);
    }
    public static FrameworkElement RoleLabel(string role)=>Row(new Image{Source=GameVisuals.RoleIcon(role),Width=26,Height=26,VerticalAlignment=VerticalAlignment.Center},Text(GameVisuals.RoleName(role),13));
    public static void MasteryLevelColumn(DataGrid table,string path,int index)
    {
        var old=table.Columns[index];var row=new FrameworkElementFactory(typeof(StackPanel));row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
        var image=new FrameworkElementFactory(typeof(Image));image.SetValue(FrameworkElement.WidthProperty,60d);image.SetValue(FrameworkElement.HeightProperty,60d);image.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,10,0));image.SetBinding(Image.SourceProperty,new Binding(path){Converter=new MasteryImageConverter()});row.AppendChild(image);
        var label=new FrameworkElementFactory(typeof(TextBlock));label.SetBinding(TextBlock.TextProperty,new Binding(path){StringFormat="Ustalık {0}"});label.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);label.SetValue(TextBlock.ForegroundProperty,Brush("#FFFFFF"));row.AppendChild(label);
        table.Columns[index]=new DataGridTemplateColumn{Header=old.Header,Width=old.Width,MinWidth=150,SortMemberPath=path,CellTemplate=new DataTemplate{VisualTree=row}};
    }
    public static void RoleColumn(DataGrid table,string path,int index=0)
    {
        var old=table.Columns[index];var content=new FrameworkElementFactory(typeof(ContentControl));content.SetBinding(ContentControl.ContentProperty,new Binding(path){Converter=new RoleCellConverter()});
        table.Columns[index]=new DataGridTemplateColumn{Header="Rol",Width=old.Width,MinWidth=180,SortMemberPath=path,CellTemplate=new DataTemplate{VisualTree=content}};
    }
}

public sealed class MasteryImageConverter:IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is int level&&level>0?GameVisuals.Mastery(level):null;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class RoleCellConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>Ui.RoleLabel(value?.ToString()??"");
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
