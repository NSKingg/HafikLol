using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace RiftRadar;
public static class SidebarVisuals
{
    public static FrameworkElement Icon(string key,int size=21)
    {
        var canvas=new Canvas{Width=24,Height=24};var color=Ui.Brush(key=="live"?"#F27685":"#969FAC");
        void Path(string d,bool fill=false){canvas.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(d),Stroke=color,StrokeThickness=1.7,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Fill=fill?color:null});}
        switch(key)
        {
            case "overview":Path("M3 3H10V10H3Z M14 3H21V10H14Z M3 14H10V21H3Z M14 14H21V21H14Z");break;
            case "live":canvas.Children.Add(new Ellipse{Width=10,Height=10,Fill=color,Margin=new Thickness(7)});Path("M4 6C1 9 1 15 4 18 M20 6C23 9 23 15 20 18");break;
            case "history":Path("M4 8A9 9 0 1 1 3 14 M4 3V8H9 M12 7V12L16 15");break;
            case "champions":Path("M12 2L18 5L21 11L19 19L14 22V14L18 10L12 12L6 10L10 14V22L5 19L3 11L6 5Z M12 2V8 M3 11L6 10 M21 11L18 10 M6 5L8 8 M18 5L16 8");break;
            case "friends":Path("M3 6L7 11L12 4L17 11L21 6L19 18H5Z M6 21H18");break;
            case "insights":Path("M3 3V21H21 M6 16L11 11L15 14L21 6 M17 6H21V10");break;
            case "settings":Path("M9 3H15L16 6L19 7L22 10V14L19 17L16 18L15 21H9L8 18L5 17L2 14V10L5 7L8 6Z");canvas.Children.Add(new Ellipse{Width=7,Height=7,Stroke=color,StrokeThickness=1.7,Margin=new Thickness(8.5)});break;
            case "sun":canvas.Children.Add(new Ellipse{Width=9,Height=9,Stroke=color,StrokeThickness=1.7,Margin=new Thickness(7.5)});Path("M12 1V4 M12 20V23 M1 12H4 M20 12H23 M4 4L6 6 M18 18L20 20 M4 20L6 18 M18 6L20 4");break;
            case "moon":Path("M17 3C10 1 4 6 4 12C4 18 10 23 17 20C20 19 22 16 22 13C14 16 9 8 17 3Z");break;
        }
        return new Viewbox{Width=size,Height=size,Child=canvas,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center};
    }
    public static FrameworkElement Label(string key,string label)
    {
        var text=Ui.Text(label,13,null,true);text.VerticalAlignment=VerticalAlignment.Center;return Ui.Row(Icon(key),text);
    }
}
public partial class MainWindow
{
    void DiscordClick(object sender,RoutedEventArgs e)=>OpenUrl("https://discord.gg/6xym2VfUTu");
    Action? updateThemeSwitch;
    internal void SetLightTheme(bool value)
    {
        store.Preferences.LightMode=value;store.Save();ThemePalette.Apply(value);Ui.UpdateTitleTheme(this);updateThemeSwitch?.Invoke();
    }
    FrameworkElement CreateThemeSwitch()
    {
        var move=new TranslateTransform();var rotate=new RotateTransform();
        var thumbIcon=new ContentControl{Content=SidebarVisuals.Icon(ThemePalette.Light?"sun":"moon",14),HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,RenderTransformOrigin=new Point(.5,.5),RenderTransform=rotate};
        var thumb=new Border{Width=20,Height=20,CornerRadius=new CornerRadius(10),Background=Ui.Brush("#1B1E24"),Child=thumbIcon,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(3),RenderTransform=move};
        var track=new Grid{Width=46,Height=26,HorizontalAlignment=HorizontalAlignment.Right};track.Children.Add(new Border{CornerRadius=new CornerRadius(13),Background=Ui.Brush("#253249")});track.Children.Add(thumb);
        move.X=ThemePalette.Light?20:0;
        var caption=Ui.Text(ThemePalette.Light?"Açık tema":"Koyu tema",11,null,true);caption.VerticalAlignment=VerticalAlignment.Center;
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.Children.Add(caption);Grid.SetColumn(track,1);grid.Children.Add(track);
        updateThemeSwitch=()=>
        {
            caption.Text=ThemePalette.Light?"Açık tema":"Koyu tema";thumbIcon.Content=SidebarVisuals.Icon(ThemePalette.Light?"sun":"moon",14);
            var easing=new CubicEase{EasingMode=EasingMode.EaseInOut};move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(ThemePalette.Light?20:0,TimeSpan.FromMilliseconds(320)){EasingFunction=easing});rotate.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(-80,0,TimeSpan.FromMilliseconds(320)){EasingFunction=easing});
        };
        var button=Ui.Button("",()=>SetLightTheme(!ThemePalette.Light));
        button.Content=grid;button.Background=Brushes.Transparent;button.Padding=new Thickness(10,5,8,5);button.Margin=new Thickness(0);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.ToolTip="Açık / koyu tema · seçim kaydedilir";System.Windows.Automation.AutomationProperties.SetName(button,"Açık veya koyu tema değiştir");return button;
    }
}
