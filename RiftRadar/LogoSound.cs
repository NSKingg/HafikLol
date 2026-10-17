using System.IO;
using System.Media;
using System.Windows;
namespace RiftRadar;
public partial class MainWindow
{
    SoundPlayer? logoSound;
    Stream? logoSoundStream;
    void CatLogoClick(object sender,RoutedEventArgs e)
    {
        if(closing)return;
        try
        {
            if(logoSound==null)
            {
                logoSoundStream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Sounds/meow.wav"))!.Stream;
                logoSound=new SoundPlayer(logoSoundStream);logoSound.Load();
                Closed+=(_,_)=>{logoSound?.Stop();logoSound?.Dispose();logoSoundStream?.Dispose();};
            }
            logoSound.Play();
        }
        catch { /* An unavailable audio device should not interrupt navigation. */ }
    }
}
