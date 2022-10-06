using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using DesktopUI2.Views;
using DesktopUI2.ViewModels;
using Objects.Structural.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Speckle.ConnectorDLUBAL
{
    internal class DesktopUI
    {
        public DesktopUI()
        {
            CreateOrFocusSpeckle();
        }
        public static Window? MainWindow { get; private set; }

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<DesktopUI2.App>()
          .UsePlatformDetect()
          .With(new SkiaOptions { MaxGpuResourceSizeBytes = 8096000 })
          .With(new Win32PlatformOptions { AllowEglInitialization = true, EnableMultitouch = false })
          .LogToTrace()
          .UseReactiveUI();

        public static void CreateOrFocusSpeckle()
        {
            if (MainWindow == null)
            {
                BuildAvaloniaApp().Start(AppMain, null);
            }

            MainWindow.Show();
        }

        private static void AppMain(Application app, string[] args)
        {
            ConnectorBindingsDlubal binding = new ConnectorBindingsDlubal();
            MainViewModel viewModel = new MainViewModel(binding);

            MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            app.Run(MainWindow);
        }
    }
}
