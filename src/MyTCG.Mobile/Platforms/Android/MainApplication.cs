using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using Microsoft.Extensions.DependencyInjection;
using Android.App;
using Android.Runtime;
namespace MyTCG.Mobile;
[Application]
public class MainApplication(nint handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
