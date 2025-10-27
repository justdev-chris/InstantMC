using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace InstantMC
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();
        private string profilesFile = "profiles.txt";
        private string configFile = "config.txt";

        public MainWindow()
        {
            InitializeComponent();
            ShowEULAWarning();
            LoadConfig();
            LoadProfiles();
        }

        private void ShowEULAWarning()
        {
            MessageBox.Show(
                "NOTICE: This launcher bypasses Minecraft authentication.\n\n" +
                "This violates Mojang's EULA and is intended for:\n" +
                "- Testing purposes\n" + 
                "- Offline/cracked servers\n" +
                "- Educational use\n\n" +
                "By continuing, you acknowledge this is unauthorized.",
                "EULA Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "JAR files (*.jar)|*.jar|All files (*.*)|*.*";
            openFileDialog.Title = "Select Minecraft JAR file";
            
            if (openFileDialog.ShowDialog() == true)
            {
                JarPathBox.Text = openFileDialog.FileName;
                SaveConfig();
            }
        }

        private void LoadConfig()
        {
            if (File.Exists(configFile))
            {
                var lines = File.ReadAllLines(configFile);
                if (lines.Length > 0)
                {
                    JarPathBox.Text = lines[0];
                }
            }
        }

        private void SaveConfig()
        {
            File.WriteAllText(configFile, JarPathBox.Text);
        }

        private void SaveProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
            {
                MessageBox.Show("Enter a username!");
                return;
            }

            string profileName = UsernameBox.Text;
            profiles[profileName] = new Profile
            {
                Username = UsernameBox.Text,
                ServerIP = ServerIPBox.Text,
                ServerPort = ServerPortBox.Text
            };

            SaveProfiles();
            UpdateProfileDropdown();
            ProfileDropdown.Text = profileName;
            MessageBox.Show($"Profile '{profileName}' saved!");
        }

        private void ProfileDropdown_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileDropdown.SelectedItem != null && profiles.ContainsKey(ProfileDropdown.SelectedItem.ToString()))
            {
                var profile = profiles[ProfileDropdown.SelectedItem.ToString()];
                UsernameBox.Text = profile.Username;
                ServerIPBox.Text = profile.ServerIP;
                ServerPortBox.Text = profile.ServerPort;
            }
        }

        private void UpdateProfileDropdown()
        {
            ProfileDropdown.Items.Clear();
            foreach (var profile in profiles.Keys)
            {
                ProfileDropdown.Items.Add(profile);
            }
        }

        private void LoadProfiles()
        {
            if (File.Exists(profilesFile))
            {
                foreach (var line in File.ReadAllLines(profilesFile))
                {
                    var parts = line.Split('|');
                    if (parts.Length == 3)
                    {
                        profiles[parts[0]] = new Profile { Username = parts[0], ServerIP = parts[1], ServerPort = parts[2] };
                    }
                }
                UpdateProfileDropdown();
            }
        }

        private void SaveProfiles()
        {
            using (var writer = new StreamWriter(profilesFile))
            {
                foreach (var profile in profiles.Values)
                {
                    writer.WriteLine($"{profile.Username}|{profile.ServerIP}|{profile.ServerPort}");
                }
            }
        }

        private void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(JarPathBox.Text))
            {
                MessageBox.Show("Minecraft JAR not found! Please browse and select your JAR file.");
                return;
            }

            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
            {
                MessageBox.Show("Enter a username!");
                return;
            }

            try
            {
                string serverArgs = "";
                if (!string.IsNullOrWhiteSpace(ServerIPBox.Text))
                {
                    string port = string.IsNullOrWhiteSpace(ServerPortBox.Text) ? "25565" : ServerPortBox.Text;
                    serverArgs = $" --server {ServerIPBox.Text} --port {port}";
                }

                // Save JAR path for next time
                SaveConfig();

                Process.Start("java", $"-jar \"{JarPathBox.Text}\" --username {UsernameBox.Text} --version InstantMC --accessToken 0 --userType legacy --online-mode false{serverArgs}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}");
            }
        }
    }

    public class Profile
    {
        public string Username { get; set; }
        public string ServerIP { get; set; }
        public string ServerPort { get; set; }
    }
}
