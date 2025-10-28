using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Net;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

namespace InstantMC
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();
        private string profilesFile = "profiles.txt";
        private string configFile = "config.txt";
        private string selectedVersionUrl;
        private VersionDetails selectedVersionDetails;

        public MainWindow()
        {
            InitializeComponent();
            ShowEULAWarning();
            
            if (!CheckJava())
            {
                ShowJavaSetup();
            }
            
            LoadConfig();
            LoadProfiles();
            LoadVersions();
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

        private bool CheckJava()
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = "java";
                process.StartInfo.Arguments = "-version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ShowJavaSetup()
        {
            MessageBoxResult result = MessageBox.Show(
                "Java is required to run Minecraft.\n\n" +
                "Click OK to open Java download page, or Cancel to manually install later.",
                "Java Not Found",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.OK)
            {
                Process.Start("https://adoptium.net/temurin/releases/");
            }
        }

        private async void RefreshVersionsBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadVersions();
        }

        private async Task LoadVersions()
        {
            try
            {
                VersionDropdown.IsEnabled = false;
                RefreshVersionsBtn.Content = "Loading...";

                using (WebClient client = new WebClient())
                {
                    string manifestJson = await client.DownloadStringTaskAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
                    VersionManifest manifest = JsonConvert.DeserializeObject<VersionManifest>(manifestJson);

                    var releaseVersions = manifest.versions.Where(v => v.type == "release").Take(20).ToList();

                    VersionDropdown.Items.Clear();
                    foreach (var version in releaseVersions)
                    {
                        VersionDropdown.Items.Add(version.id);
                    }

                    if (VersionDropdown.Items.Count > 0)
                        VersionDropdown.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load versions: {ex.Message}");
            }
            finally
            {
                VersionDropdown.IsEnabled = true;
                RefreshVersionsBtn.Content = "Refresh Versions";
            }
        }

        private async void VersionDropdown_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (VersionDropdown.SelectedItem == null) return;

            string versionId = VersionDropdown.SelectedItem.ToString();
            
            try
            {
                using (WebClient client = new WebClient())
                {
                    string manifestJson = await client.DownloadStringTaskAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
                    VersionManifest manifest = JsonConvert.DeserializeObject<VersionManifest>(manifestJson);
                    
                    var selectedVersion = manifest.versions.FirstOrDefault(v => v.id == versionId);
                    if (selectedVersion != null)
                    {
                        selectedVersionUrl = selectedVersion.url;
                        string versionJson = await client.DownloadStringTaskAsync(selectedVersionUrl);
                        selectedVersionDetails = JsonConvert.DeserializeObject<VersionDetails>(versionJson);
                        
                        MessageBox.Show($"Version {versionId} loaded!\nMain Class: {selectedVersionDetails.mainClass}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load version details: {ex.Message}");
            }
        }

        private void BrowseDirBtn_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new Forms.FolderBrowserDialog();
            folderDialog.Description = "Select .minecraft folder";
            folderDialog.SelectedPath = Path.GetFullPath(@".\minecraft");
            
            if (folderDialog.ShowDialog() == Forms.DialogResult.OK)
            {
                MinecraftDirBox.Text = folderDialog.SelectedPath;
                SaveConfig();
            }
        }

        private void LoadConfig()
        {
            if (File.Exists(configFile))
            {
                var lines = File.ReadAllLines(configFile);
                if (lines.Length > 0) MinecraftDirBox.Text = lines[0];
            }
        }

        private void SaveConfig()
        {
            File.WriteAllText(configFile, MinecraftDirBox.Text);
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

        private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (selectedVersionDetails == null)
            {
                MessageBox.Show("Select a Minecraft version first!");
                return;
            }

            try
            {
                DownloadBtn.IsEnabled = false;
                DownloadBtn.Content = "Downloading...";

                string minecraftDir = Path.GetFullPath(MinecraftDirBox.Text);
                Directory.CreateDirectory(minecraftDir);
                Directory.CreateDirectory(Path.Combine(minecraftDir, "versions"));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "libraries"));

                // Download client JAR
                string versionDir = Path.Combine(minecraftDir, "versions", VersionDropdown.SelectedItem.ToString());
                Directory.CreateDirectory(versionDir);
                
                string clientJarPath = Path.Combine(versionDir, $"{VersionDropdown.SelectedItem.ToString()}.jar");
                
                using (WebClient client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(selectedVersionDetails.downloads.client.url, clientJarPath);
                }

                MessageBox.Show($"Downloaded Minecraft {VersionDropdown.SelectedItem.ToString()}!\nFile: {clientJarPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}");
            }
            finally
            {
                DownloadBtn.IsEnabled = true;
                DownloadBtn.Content = "Download Game";
            }
        }

        private void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (selectedVersionDetails == null)
            {
                MessageBox.Show("Select and download a Minecraft version first!");
                return;
            }

            string minecraftDir = Path.GetFullPath(MinecraftDirBox.Text);
            string versionId = VersionDropdown.SelectedItem.ToString();
            string clientJarPath = Path.Combine(minecraftDir, "versions", versionId, $"{versionId}.jar");

            if (!File.Exists(clientJarPath))
            {
                MessageBox.Show("Minecraft client not found! Download it first.");
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

                Process.Start("java", 
                    $"-jar \"{clientJarPath}\" " +
                    $"--username {UsernameBox.Text} " +
                    $"--version {versionId} " +
                    $"--gameDir \"{minecraftDir}\" " +
                    $"--assetsDir \"{minecraftDir}\\assets\" " +
                    $"--accessToken 0 " +
                    $"--userType legacy " +
                    $"--online-mode false" +
                    serverArgs);
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

    public class VersionManifest
    {
        public List<Version> versions { get; set; }
    }

    public class Version
    {
        public string id { get; set; }
        public string type { get; set; }
        public string url { get; set; }
        public string time { get; set; }
        public string releaseTime { get; set; }
    }

    public class VersionDetails
    {
        public Downloads downloads { get; set; }
        public string assets { get; set; }
        public List<Library> libraries { get; set; }
        public string mainClass { get; set; }
    }

    public class Downloads
    {
        public DownloadItem client { get; set; }
        public DownloadItem server { get; set; }
    }

    public class DownloadItem
    {
        public string sha1 { get; set; }
        public int size { get; set; }
        public string url { get; set; }
    }

    public class Library
    {
        public string name { get; set; }
        public Downloads downloads { get; set; }
    }
}