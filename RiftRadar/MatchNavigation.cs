using System.Windows;
using System.Windows.Input;

namespace RiftRadar;

public partial class MainWindow
{
    MatchView? matchView;
    double beforeMatchOffset;
    internal void OpenMatch(MatchRow row)
    {
        if(selected==null||closing)return;
        CloseMatch();ScrollAssist.Stop(ContentScroll);beforeMatchOffset=ContentScroll.VerticalOffset;
        matchView=new MatchView(row,data,api,Region.Find(selected.Platform),demo!=null,CloseMatch,OpenProfilePreview);
        MatchHost.Content=matchView;ContentScroll.Visibility=Visibility.Hidden;MatchHost.Visibility=Visibility.Visible;
        Motion.Reveal(MatchHost);
    }
    internal void CloseMatch()
    {
        if(matchView==null)return;
        matchView.Dispose();matchView=null;MatchHost.Content=null;MatchHost.Visibility=Visibility.Collapsed;
        ContentScroll.Visibility=Visibility.Visible;ContentScroll.ScrollToVerticalOffset(beforeMatchOffset);
        Motion.Reveal(Body);
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if(e.Key==Key.Escape&&ProfileOverlay.Visibility==Visibility.Visible){CloseProfilePreview();e.Handled=true;return;}
        if(matchView!=null&&(e.Key==Key.BrowserBack||(e.SystemKey==Key.Left&&Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))))
        {CloseMatch();e.Handled=true;return;}
        base.OnPreviewKeyDown(e);
    }
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        if(matchView!=null&&e.ChangedButton==MouseButton.XButton1){CloseMatch();e.Handled=true;return;}
        base.OnPreviewMouseDown(e);
    }
}
