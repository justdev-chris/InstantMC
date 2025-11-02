using System;
using System.Threading.Tasks;
using System.Windows;

namespace PawCraft
{
    public partial class App : Application
    {
        private SplashWindow splash;
        private MainWindow mainWindow;

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // Show splash screen
            splash = new SplashWindow();
            splash.Show();
            
            // Simulate loading steps
            await LoadApplication();
            
            // Show main window and close splash
            mainWindow = new MainWindow();
            mainWindow.Show();
            splash.Close();
        }

        private async Task LoadApplication()
        {
            splash.UpdateStatus("Loading themes...");
            await Task.Delay(500);
            
            splash.UpdateStatus("Initializing mods system...");
            await Task.Delay(500);
            
            splash.UpdateStatus("Checking for updates...");
            await Task.Delay(500);
            
            splash.UpdateStatus("Ready to launch!");
            await Task.Delay(300);
        }
    }
}
