using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RiftRadar;

public static class ScrollAssist
{
    static bool enabled;
    sealed class MotionState { public double Position,Target; public long Last; public bool Active; }
    static readonly ConditionalWeakTable<ScrollViewer,MotionState> motions=new();
    static readonly HashSet<ScrollViewer> active=new();
    public static void Enable()
    {
        if(enabled)return;enabled=true;
        EventManager.RegisterClassHandler(typeof(ScrollViewer),UIElement.PreviewMouseWheelEvent,new MouseWheelEventHandler(OnWheel),true);
        EventManager.RegisterClassHandler(typeof(ScrollViewer),UIElement.PreviewMouseDownEvent,new MouseButtonEventHandler((s,e)=>Stop((ScrollViewer)s)),true);
        EventManager.RegisterClassHandler(typeof(ScrollViewer),UIElement.PreviewKeyDownEvent,new KeyEventHandler((s,e)=>Stop((ScrollViewer)s)),true);
        EventManager.RegisterClassHandler(typeof(ScrollViewer),FrameworkElement.UnloadedEvent,new RoutedEventHandler((s,e)=>Stop((ScrollViewer)s)),true);
    }
    public static void Stop(ScrollViewer view)
    {
        if(motions.TryGetValue(view,out var state))state.Active=false;
        active.Remove(view);
        if(active.Count==0)CompositionTarget.Rendering-=OnFrame;
    }
    public static double WheelTarget(double offset,double maximum,double viewport,int delta,int lines)
    {
        var distance=lines<0?viewport:Math.Min(120,lines*24d);
        return Math.Clamp(offset-delta/120d*distance,0,maximum);
    }
    internal static void Move(ScrollViewer view,int delta)
    {
        var state=motions.GetOrCreateValue(view);
        bool matchList=view.TemplatedParent is ListBox list&&list.Items.OfType<MatchRow>().Any();
        bool logical=view.CanContentScroll&&view.ExtentHeight<view.ActualHeight&&view.ViewportHeight<view.ActualHeight/2;
        if(logical)
        {
            Stop(view);
            var amount=SystemParameters.WheelScrollLines<0?Math.Max(1,view.ViewportHeight):SystemParameters.WheelScrollLines;
            view.ScrollToVerticalOffset(Math.Clamp(view.VerticalOffset-delta/120d*amount,0,view.ScrollableHeight));return;
        }
        if(!state.Active){state.Position=view.VerticalOffset;state.Target=state.Position;}
        // Continue from the current position when the wheel reverses direction.
        if((state.Target-state.Position)*delta>0)state.Target=state.Position;
        state.Target=WheelTarget(state.Target,view.ScrollableHeight,view.ViewportHeight,delta,matchList?2:SystemParameters.WheelScrollLines);
        if(!SystemParameters.ClientAreaAnimation){Stop(view);view.ScrollToVerticalOffset(state.Target);return;}
        state.Last=Stopwatch.GetTimestamp();state.Active=true;
        if(active.Add(view)&&active.Count==1)CompositionTarget.Rendering+=OnFrame;
    }
    internal static bool Advance(ScrollViewer view,double seconds)
    {
        if(!motions.TryGetValue(view,out var state)||!state.Active)return false;
        state.Target=Math.Clamp(state.Target,0,view.ScrollableHeight);
        state.Position+=(state.Target-state.Position)*(1-Math.Exp(-Math.Clamp(seconds,0,.05)/.13));
        if(Math.Abs(state.Target-state.Position)<.15)
        {view.ScrollToVerticalOffset(state.Target);Stop(view);return false;}
        view.ScrollToVerticalOffset(state.Position);return true;
    }
    static void OnFrame(object? sender,EventArgs args)
    {
        var now=Stopwatch.GetTimestamp();
        foreach(var view in active.ToArray())
        {
            if(!view.IsLoaded){Stop(view);continue;}
            var state=motions.GetOrCreateValue(view);var elapsed=(now-state.Last)/(double)Stopwatch.Frequency;state.Last=now;
            Advance(view,elapsed);
        }
    }
    static DependencyObject? Parent(DependencyObject node)=>node is Visual or Visual3D?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);
    public static bool CanMove(ScrollViewer view,int delta)=>delta>0?view.VerticalOffset>0.01:view.VerticalOffset<view.ScrollableHeight-0.01;
    public static ScrollViewer? OuterTarget(ScrollViewer inner,int delta)
    {
        for(var parent=Parent(inner);parent!=null;parent=Parent(parent))
            if(parent is ScrollViewer view&&view.VerticalScrollBarVisibility!=ScrollBarVisibility.Disabled&&CanMove(view,delta))return view;
        return null;
    }
    static void OnWheel(object sender,MouseWheelEventArgs e)
    {
        if(e.Handled||e.Delta==0||Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))return;
        DependencyObject? current=e.OriginalSource as DependencyObject;
        while(current!=null&&current is not ScrollViewer)current=Parent(current);
        if(current is not ScrollViewer inner||!ReferenceEquals(inner,sender))return;
        var target=inner.VerticalScrollBarVisibility!=ScrollBarVisibility.Disabled&&CanMove(inner,e.Delta)?inner:OuterTarget(inner,e.Delta);if(target==null)return;
        if(SystemParameters.WheelScrollLines==0)return;
        Move(target,e.Delta);
        e.Handled=true;
    }
}
