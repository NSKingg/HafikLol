using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace RiftRadar;

public static partial class Ui
{
    public static Brush Outcome(MatchRow row)=>row.Remake?Muted:row.Win?WinBlue:Red;
    public static FrameworkElement Badge(string label,Brush color)
    {
        var solid=color as SolidColorBrush;var bg=new SolidColorBrush(Color.FromArgb(24,solid?.Color.R??120,solid?.Color.G??120,solid?.Color.B??120));
        return new Border{CornerRadius=new CornerRadius(5),Padding=new Thickness(8,4,8,4),Background=bg,Child=Text(label,11,color,true),HorizontalAlignment=HorizontalAlignment.Left};
    }
    public static StackPanel Inventory(StaticData data,IEnumerable<int> ids,int size=25)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var list=ids.Take(7).ToArray();
        for(int i=0;i<7;i++){var item=ItemIcon(data,(i<list.Length?list[i]:0).ToString(),size);item.Margin=new Thickness(i==6?4:0,0,3,0);row.Children.Add(item);}return row;
    }
    public static TextBlock KdaText(MatchRow row,int size=18)
    {
        var text=new TextBlock{FontSize=size,VerticalAlignment=VerticalAlignment.Center};
        text.Inlines.Add(new Run(row.P.I("kills").ToString()){Foreground=Brush("#ECEFF5"),FontWeight=FontWeights.SemiBold});
        text.Inlines.Add(new Run(" / "){Foreground=Muted});text.Inlines.Add(new Run(row.P.I("deaths").ToString()){Foreground=Red});
        text.Inlines.Add(new Run(" / "){Foreground=Muted});text.Inlines.Add(new Run(row.P.I("assists").ToString()){Foreground=Brush("#B6C6DF")});return text;
    }
    public static FrameworkElement MatchCard(MatchRow row,StaticData data,Action<MatchRow> open)
    {
        var grid=new Grid{Margin=new Thickness(13,18,13,18),MinHeight=62};
        foreach(var width in new[]{68d,70,0,118,120,274,20})grid.ColumnDefinitions.Add(new ColumnDefinition{Width=width==0?new GridLength(1,GridUnitType.Star):new GridLength(width)});
        void Put(UIElement el,int col){if(el is FrameworkElement element)element.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(el,col);grid.Children.Add(el);}
        Put(Stack(Badge(row.Result,Outcome(row)),Space(8),Text(row.Duration,11,Muted)),0);Put(Portrait(data,row.Champion,56),1);
        var identity=Stack(Text(data.Champion(row.Champion).S("name",row.Champion),16,null,true),Space(5),Text(row.Mode,12,Muted),Text(row.Date,11,Muted));identity.MinWidth=106;Put(identity,2);
        Put(Stack(KdaText(row,20),Space(6),Text($"{row.KdaValue:0.00} KDA",12,StatTheme.Kills.Color)),3);
        Put(Stack(Text($"{row.CsValue:0} CS · {row.CsValue/row.Minutes:0.0}/dk",12,StatTheme.Gold.Color),Space(5),Text($"{row.P.N("totalDamageDealtToChampions")/1000:0.0}k hasar",12,StatTheme.Damage.Color),Text($"%{row.Kp:0} katılım",11,Muted)),4);
        Put(Inventory(data,row.Items,34),5);Put(Text("›",23,Muted),6);
        var shell=new Grid();shell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(4)});shell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        shell.Children.Add(new Border{Background=Outcome(row),CornerRadius=new CornerRadius(9,0,0,9)});Grid.SetColumn(grid,1);shell.Children.Add(grid);
        var content=new Border{Background=Brush(row.Remake?"#1D2027":row.Win?"#1B2432":"#2A2028"),BorderBrush=Brush(row.Win?"#2A3D58":"#443039"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Child=shell};
        var button=Button("",()=>open(row));button.Content=content;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0);button.ToolTip="İki takımın skor tablosunu ve eşyalarını aç";return button;
    }
    public static ListBox MatchList(IEnumerable<MatchRow> rows,StaticData data,Action<MatchRow> open,double height=570,FrameworkElement? footer=null)
    {
        var list=new ListBox{ItemsSource=footer==null?rows.Cast<object>().ToList():rows.Cast<object>().Append(footer).ToList(),Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,MaxHeight=height,MinHeight=120};
        ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(list,ScrollBarVisibility.Auto);ScrollViewer.SetCanContentScroll(list,true);
        VirtualizingPanel.SetIsVirtualizing(list,true);VirtualizingPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(list,ScrollUnit.Pixel);
        VirtualizingPanel.SetCacheLength(list,new VirtualizationCacheLength(1));
        var container=new Style(typeof(ListBoxItem));container.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));container.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));container.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,0,0,8)));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));container.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=presenter}));list.ItemContainerStyle=container;
        var element=new FrameworkElementFactory(typeof(ContentControl));element.SetValue(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch);element.SetBinding(ContentControl.ContentProperty,new Binding{Converter=new MatchCardConverter(data,open)});list.ItemTemplate=new DataTemplate{VisualTree=element};return list;
    }
    public static void ChampionColumn(DataGrid table,StaticData data,string namePath,int index,int iconSize=29)
    {
        var old=table.Columns[index];var panel=new FrameworkElementFactory(typeof(StackPanel));panel.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
        var image=new FrameworkElementFactory(typeof(Image));image.SetValue(FrameworkElement.WidthProperty,(double)iconSize);image.SetValue(FrameworkElement.HeightProperty,(double)iconSize);image.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,8,0));image.SetBinding(Image.SourceProperty,new Binding(namePath){Converter=new ChampionImageConverter(data)});panel.AppendChild(image);
        var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(namePath));text.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);text.SetValue(FrameworkElement.MaxWidthProperty,140.0);panel.AppendChild(text);
        table.Columns.RemoveAt(index);table.Columns.Insert(index,new DataGridTemplateColumn{Header=old.Header,Width=old.Width,MinWidth=115,SortMemberPath=namePath,CellTemplate=new DataTemplate{VisualTree=panel}});
    }
    public static void InventoryColumn(DataGrid table,StaticData data,string path,int width=225)
    {
        var content=new FrameworkElementFactory(typeof(ContentControl));content.SetBinding(ContentControl.ContentProperty,new Binding(path){Converter=new InventoryConverter(data)});
        table.Columns.Add(new DataGridTemplateColumn{Header="EŞYA DİZİLİMİ",MinWidth=width,Width=new DataGridLength(width),CellTemplate=new DataTemplate{VisualTree=content}});
    }
}
public sealed class ChampionImageConverter(StaticData data):IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>data.ChampionImage(value?.ToString()??"");
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}
public sealed class InventoryConverter(StaticData data):IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>value is IEnumerable<int> ids?Ui.Inventory(data,ids,25):null;
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}
public sealed class MatchCardConverter(StaticData data,Action<MatchRow> open):IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>value is MatchRow row?Ui.MatchCard(row,data,open):value as FrameworkElement;
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Binding.DoNothing;
}

