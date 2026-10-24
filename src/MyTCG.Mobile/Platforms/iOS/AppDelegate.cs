using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using Microsoft.Extensions.DependencyInjection;
using Foundation;
namespace MyTCG.Mobile;
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
