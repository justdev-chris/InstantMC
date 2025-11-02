using System.Windows;

namespace PawCraft
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        public void UpdateStatus(string message)
        {
            LoadingText.Text = message;
        }
    }
}
