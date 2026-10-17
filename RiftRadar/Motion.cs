using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RiftRadar;

public static class Motion
{
    public static void Reveal(FrameworkElement element)
    {
        if(!SystemParameters.ClientAreaAnimation)return;
        var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
        var fade=new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(160)){EasingFunction=ease,FillBehavior=FillBehavior.Stop};
        element.BeginAnimation(UIElement.OpacityProperty,fade,HandoffBehavior.SnapshotAndReplace);
        var shift=new TranslateTransform();element.RenderTransform=shift;
        shift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(5,0,TimeSpan.FromMilliseconds(180)){EasingFunction=ease,FillBehavior=FillBehavior.Stop},HandoffBehavior.SnapshotAndReplace);
    }
}
