using System.IO;
using System.Windows;
using System.Windows.Media;

namespace RiftRadar;

public partial class App : Application
{
    Mutex? instance;
    EventWaitHandle? restoreEvent;
    RegisteredWaitHandle? restoreWait;
    internal bool IsExiting {get;set;}
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ScrollAssist.Enable();
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        if (e.Args.Contains("--self-test"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var result = await SelfTests.Run();
            var i = Array.IndexOf(e.Args, "--report");
            File.WriteAllText(i >= 0 && i + 1 < e.Args.Length ? e.Args[i + 1] : Path.Combine(AppContext.BaseDirectory, "test-results.json"), result);
            Shutdown(result.Contains("\"failed\": 0") ? 0 : 1);
            return;
        }
        DispatcherUnhandledException += (_, a) =>
        {
            a.Handled = true;
            if(IsExiting||Dispatcher.HasShutdownStarted)return;
            MessageBox.Show("İşlem tamamlanamadı. Verileriniz korunuyor.\n\n" + SafeError.Message(a.Exception), "HafikLol", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        var dirIndex = Array.IndexOf(e.Args, "--data-dir");
        var dir = dirIndex >= 0 && dirIndex + 1 < e.Args.Length ? e.Args[dirIndex + 1] : null;
        var store=new Store(dir);
        var id=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(store.Root+Environment.UserName)))[..24];
        restoreEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\RiftRadar-Restore-"+id);
        instance=new Mutex(true,"Local\\RiftRadar-"+id,out var created);
        if(!created){restoreEvent.Set();Shutdown();return;}
        var window = new MainWindow(store, e.Args.Contains("--demo"));
        restoreWait=ThreadPool.RegisterWaitForSingleObject(restoreEvent,(_,_)=>Dispatcher.BeginInvoke(()=>{if(!IsExiting)window.RestoreFromTray();}),null,Timeout.Infinite,false);
        MainWindow = window;
        window.Show();
    }
    protected override void OnExit(ExitEventArgs e){restoreWait?.Unregister(null);restoreEvent?.Dispose();instance?.Dispose();base.OnExit(e);}
}
