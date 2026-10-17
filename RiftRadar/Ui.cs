using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RiftRadar;

public static partial class Ui
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    public static void DarkTitle(Window window)=>window.SourceInitialized+=(_,_)=>UpdateTitleTheme(window);
    public static void UpdateTitleTheme(Window window){try{var enabled=ThemePalette.Light?0:1;var handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;if(handle!=IntPtr.Zero)DwmSetWindowAttribute(handle,20,ref enabled,4);}catch{}}
    public static readonly Brush Accent=Brush("#84B5FF"),Muted=Brush("#969FAC"),Panel=Brush("#1B1E24"),Line=Brush("#2C313A"),Red=Brush("#F27685"),WinBlue=Brush("#70ADFF");
    public static Brush Brush(string hex) => ThemePalette.Brush(hex);
    public static TextBlock Text(string text,double size=13,Brush? color=null,bool bold=false) => new() {Text=text,FontSize=size,Foreground=color??Brush("#ECECEF"),TextWrapping=TextWrapping.Wrap,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal};
    public static Border Card(UIElement content,int padding=18) => new() {Background=Panel,CornerRadius=new CornerRadius(12),BorderBrush=Line,BorderThickness=new Thickness(1),Padding=new Thickness(padding),Child=content,Margin=new Thickness(0,0,0,12)};
    public static Button Button(string label,Action action,bool primary=false)
    {
        var b=new Button {Content=label,Margin=new Thickness(0,0,8,0)};
        if(primary) { b.Background=Accent;b.Foreground=Brush("#1B2821"); }
        b.Click+=(_,_)=>action(); return b;
    }
    public static StackPanel Stack(params UIElement[] children) {var p=new StackPanel();foreach(var c in children)p.Children.Add(c);return p;}
    public static WrapPanel Row(params UIElement[] children) {var p=new WrapPanel {Orientation=Orientation.Horizontal};for(int i=0;i<children.Length;i++){var c=children[i];if(i<children.Length-1&&c is FrameworkElement element&&element.Margin==default)element.Margin=new Thickness(0,0,10,0);p.Children.Add(c);}return p;}
    public static TextBlock Caption(string text) => new() {Text=text,FontSize=11,Foreground=Muted,Margin=new Thickness(0,0,0,7)};
    public static FrameworkElement Space(double height=14) => new Border {Height=height};
    public static Border Stat(string title,string value,string sub,Brush? color=null)
    {
        var visual=StatTheme.Resolve(title);
        var header=StatLabel(title,visual,11);header.Margin=new Thickness(0,0,0,7);
        var c=Card(Stack(header,Text(value,27,color??visual?.Color??Accent,true),new TextBlock{Text=sub,Foreground=Muted,FontSize=11,Margin=new Thickness(0,6,0,0)}),15);
        c.MinWidth=138;c.Margin=new Thickness(0,0,10,14); return c;
    }
    public static Grid Columns(UIElement left,UIElement right,double ratio=1)
    {
        var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(ratio,GridUnitType.Star)});g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});g.Children.Add(left);Grid.SetColumn(right,2);g.Children.Add(right);return g;
    }
    public static DataGrid Table(System.Collections.IEnumerable rows,params (string Header,string Path,double Width)[] columns)
    {
        var t=new DataGrid {ItemsSource=rows,MaxHeight=560,MinHeight=100,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        bool metrics=columns.Any(x=>x.Path=="Alan")&&columns.Any(x=>x.Path=="Değer");
        foreach(var col in columns)
        {
            var visual=StatTheme.Resolve(col.Path+" "+col.Header);
            if(metrics&&(col.Path=="Alan"||col.Path=="Değer"||col.Path=="Dakika"))
            {
                var stack=new FrameworkElementFactory(typeof(StackPanel));stack.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
                if(col.Path=="Alan"){var icon=new FrameworkElementFactory(typeof(Image));icon.SetValue(FrameworkElement.WidthProperty,17.0);icon.SetValue(FrameworkElement.HeightProperty,17.0);icon.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,8,0));icon.SetBinding(Image.SourceProperty,new Binding("Alan"){Converter=new StatIconConverter()});stack.AppendChild(icon);}
                var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(col.Path));text.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);text.SetBinding(TextBlock.ForegroundProperty,new Binding("Alan"){Converter=new StatColorConverter()});stack.AppendChild(text);
                t.Columns.Add(new DataGridTemplateColumn{Header=col.Header,CellTemplate=new DataTemplate{VisualTree=stack},Width=new DataGridLength(col.Width,DataGridLengthUnitType.Star),MinWidth=80});continue;
            }
            var style=new Style(typeof(TextBlock));if(visual!=null)style.Setters.Add(new Setter(TextBlock.ForegroundProperty,ThemePalette.Resource(visual.Hex)));
            t.Columns.Add(new DataGridTextColumn {Header=visual==null?(object)col.Header:StatLabel(col.Header,visual,12),Binding=new Binding(col.Path),ElementStyle=style,Width=new DataGridLength(col.Width,DataGridLengthUnitType.Star),MinWidth=65});
        }
        return t;
    }
    public static FrameworkElement StatLabel(string label,StatVisual? visual,double size=12)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        if(visual!=null)row.Children.Add(new Image{Source=visual.Image,Width=16,Height=16,Margin=new Thickness(0,0,7,0),ToolTip=visual.Name});
        row.Children.Add(Text(label,size,visual?.Color??Muted));return row;
    }
    public static Border StatChip(string key,string value,string? label=null)
    {
        var visual=StatTheme.Resolve(key,true);var panel=new StackPanel{Orientation=Orientation.Horizontal};
        if(visual!=null)panel.Children.Add(new Image{Source=visual.Image,Width=21,Height=21,Margin=new Thickness(0,0,8,0)});
        var text=Text(value+" "+(label??key),13,visual?.Color??Muted,true);text.VerticalAlignment=VerticalAlignment.Center;panel.Children.Add(text);
        return new Border{Child=panel,Background=Brush("#252528"),BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Padding=new Thickness(9,7,11,7),Margin=new Thickness(0,0,8,8),ToolTip=label??key};
    }
    public static FrameworkElement ItemIcon(StaticData data,string id,int size=32)
    {
        var source=data.ItemImage(id);var frame=new Border{Width=size,Height=size,Background=Brush("#141416"),BorderBrush=Brush("#514A3C"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Margin=new Thickness(0,0,8,0)};
        if(source!=null)frame.Child=new Image{Source=source,Stretch=Stretch.Uniform};else frame.Child=new TextBlock{Text=id=="0"?"":"?",Foreground=Muted,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        if(int.TryParse(id,out var numeric)&&numeric>0)
        {
            frame.ToolTip=data.ItemName(numeric);frame.ToolTipOpening+=(_,_)=>
            {if(data.Items[id] is {} raw)frame.ToolTip=new ToolTip{MaxWidth=445,Background=Panel,BorderBrush=Line,Content=new ScrollViewer{MaxHeight=590,Width=395,Content=ItemDetails.View(data,new ItemRow(id,raw))}};};
        }
        return frame;
    }
    public static WrapPanel ItemStrip(StaticData data,IEnumerable<int> ids,bool names=false,int size=34)
    {
        var panel=new WrapPanel();foreach(var id in ids.Where(x=>x>0))
        {
            var tile=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,10,9)};tile.Children.Add(ItemIcon(data,id.ToString(),size));
            if(names){var label=Text(data.ItemName(id),12);label.VerticalAlignment=VerticalAlignment.Center;tile.Children.Add(label);}
            panel.Children.Add(tile);
        }
        return panel;
    }
    public static void ItemColumn(DataGrid table,StaticData data,string idPath,string namePath,int index=0,bool showStats=false)
    {
        var old=table.Columns[index];var row=new FrameworkElementFactory(typeof(StackPanel));row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
        var image=new FrameworkElementFactory(typeof(Image));image.SetValue(FrameworkElement.WidthProperty,showStats?36.0:27.0);image.SetValue(FrameworkElement.HeightProperty,showStats?36.0:27.0);image.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,9,0));image.SetBinding(Image.SourceProperty,new Binding(idPath){Converter=new ItemImageConverter(data)});row.AppendChild(image);
        var names=new FrameworkElementFactory(typeof(StackPanel));names.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(namePath));text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);text.SetValue(FrameworkElement.MaxWidthProperty,250.0);names.AppendChild(text);
        if(showStats){var stats=new FrameworkElementFactory(typeof(TextBlock));stats.SetBinding(TextBlock.TextProperty,new Binding("StatSummary"));stats.SetValue(TextBlock.FontSizeProperty,10.0);stats.SetValue(TextBlock.ForegroundProperty,Muted);stats.SetValue(FrameworkElement.MaxWidthProperty,250.0);stats.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);stats.SetValue(FrameworkElement.MarginProperty,new Thickness(0,4,0,0));names.AppendChild(stats);}row.AppendChild(names);
        table.Columns.RemoveAt(index);table.Columns.Insert(index,new DataGridTemplateColumn{Header=old.Header,Width=old.Width,MinWidth=Math.Max(150,old.MinWidth),CellTemplate=new DataTemplate{VisualTree=row},SortMemberPath=namePath});
    }
    public static FrameworkElement Portrait(StaticData data,string champ,int size=40)
    {
        var image=data.ChampionImage(champ);return new Border{Width=size,Height=size,CornerRadius=new CornerRadius(8),BorderBrush=Line,BorderThickness=new Thickness(1),ClipToBounds=true,Child=image==null?Text("?",16,Muted):new Image{Source=image,Stretch=Stretch.UniformToFill},Margin=new Thickness(0,0,11,0),ToolTip=data.Champion(champ).S("name",champ)};
    }
    public static FrameworkElement Bar(string title,string value,double percentage,Brush? color=null,FrameworkElement? adornment=null)
    {
        var visual=StatTheme.Resolve(title);var head=new DockPanel();var v=Text(value,12,color??visual?.Color??Muted);DockPanel.SetDock(v,Dock.Right);head.Children.Add(v);head.Children.Add(adornment==null?StatLabel(title,visual):Row(StatLabel(title,visual),adornment));
        var progress=new ProgressBar {Minimum=0,Maximum=100,Value=Math.Clamp(percentage,0,100),Height=5,Foreground=color??Accent,Background=Brush("#343439"),BorderThickness=new Thickness(0),Margin=new Thickness(0,7,0,13)};
        return Stack(head,progress);
    }
}
public sealed class ItemImageConverter(StaticData data):IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>data.ItemImage(value?.ToString()??"");
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}
public sealed class StatIconConverter:IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>StatTheme.Resolve(value?.ToString()??"",true)?.Image;
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}
public sealed class StatColorConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>StatTheme.Resolve(value?.ToString()??"",true)?.Color??Ui.Muted;
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}
