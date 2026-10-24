using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using Microsoft.Extensions.DependencyInjection;
namespace MyTCG.Mobile;
public sealed class App(MainPage page) : Microsoft.Maui.Controls.Application
{
    protected override Window CreateWindow(IActivationState? activationState) => new(page);
}
