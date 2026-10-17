using System.Windows;
using Forms=System.Windows.Forms;
namespace RiftRadar;
public partial class MainWindow
{
    Forms.NotifyIcon? trayIcon;
    Forms.ContextMenuStrip? trayMenu;
    System.Drawing.Icon? trayImage;
    bool exitRequested;
    WindowState trayRestoreState=WindowState.Normal;
    void InitializeTray()
    {
        try
        {
            using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
            using var original=new System.Drawing.Icon(stream);trayImage=(System.Drawing.Icon)original.Clone();
            trayMenu=new Forms.ContextMenuStrip();
            trayMenu.Items.Add("HafikLol’u aç",null,(_,_)=>Dispatcher.BeginInvoke(RestoreFromTray));trayMenu.Items.Add(new Forms.ToolStripSeparator());
            trayMenu.Items.Add("Çıkış",null,(_,_)=>Dispatcher.BeginInvoke(ExitFromTray));
            trayIcon=new Forms.NotifyIcon{Icon=trayImage,Text="HafikLol",ContextMenuStrip=trayMenu,Visible=true};
            trayIcon.MouseClick+=(_,e)=>{if(e.Button==Forms.MouseButtons.Left)Dispatcher.BeginInvoke(RestoreFromTray);};
            Application.Current.SessionEnding+=SessionEnding;
            Closed+=(_,_)=>{Application.Current.SessionEnding-=SessionEnding;DisposeTray();};
        }
        catch {DisposeTray();} // Without a working tray icon, Close still exits normally.
    }
    void SessionEnding(object sender,SessionEndingCancelEventArgs e)=>exitRequested=true;
    internal void HideToTray()
    {
        if(trayIcon==null||closing)return;
        if(WindowState!=WindowState.Minimized)trayRestoreState=WindowState;
        WindowState=WindowState.Minimized;ShowInTaskbar=false;Hide();
    }
    internal void RestoreFromTray()
    {
        if(closing)return;ShowInTaskbar=true;if(!IsVisible||WindowState==WindowState.Minimized){Show();WindowState=trayRestoreState;}Activate();
    }
    internal void ExitFromTray(){if(closing)return;exitRequested=true;Close();}
    void DisposeTray()
    {
        if(trayIcon!=null){trayIcon.Visible=false;trayIcon.Dispose();trayIcon=null;}
        trayMenu?.Dispose();trayMenu=null;trayImage?.Dispose();trayImage=null;
    }
}
