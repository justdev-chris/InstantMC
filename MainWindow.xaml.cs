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
        private VersionDetails selectedVersionDetails;
        private AssetsIndex assetsIndex;

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
            MessageBox.Show("EULA warning...");
        }

        private bool CheckJava()
        {
            try
            {
                var process = new Process();
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
            // Java setup code
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
                        string versionJson = await client.DownloadStringTaskAsync(selectedVersion.url);
                        selectedVersionDetails = JsonConvert.DeserializeObject<VersionDetails>(versionJson);
                        
                        DownloadStatus.Text = $"Version {versionId} loaded - Ready to download";
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
                DownloadProgress.Visibility = Visibility.Visible;
                DownloadStatus.Text = "Starting download...";

                string minecraftDir = Path.GetFullPath(MinecraftDirBox.Text);
                
                // Create directories
                Directory.CreateDirectory(minecraftDir);
                Directory.CreateDirectory(Path.Combine(minecraftDir, "versions", selectedVersionDetails.id));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "libraries"));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "assets", "objects"));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "assets", "indexes"));

                // Download everything
                await DownloadLibraries(minecraftDir);
                await DownloadAssets(minecraftDir);
                await DownloadClient(minecraftDir);

                DownloadStatus.Text = "Download complete!";
                MessageBox.Show("Game downloaded successfully!");
            }
            catch (Exception ex)
            {
                DownloadStatus.Text = "Download failed!";
                MessageBox.Show($"Download failed: {ex.Message}");
            }
            finally
            {
                DownloadBtn.IsEnabled = true;
                DownloadProgress.Visibility = Visibility.Collapsed;
            }
        }

        private async Task DownloadLibraries(string minecraftDir)
        {
            DownloadStatus.Text = "Downloading libraries...";
            
            using (WebClient client = new WebClient())
            {
                foreach (var library in selectedVersionDetails.libraries)
                {
                    if (!IsLibraryAllowed(library)) continue;

                    string libPath = Path.Combine(minecraftDir, "libraries", library.downloads.artifact.path);
                    string libDir = Path.GetDirectoryName(libPath);
                    
                    Directory.CreateDirectory(libDir);
                    
                    if (!File.Exists(libPath))
                    {
                        await client.DownloadFileTaskAsync(new Uri(library.downloads.artifact.url), libPath);
                    }
                }
            }
        }

        private async Task DownloadAssets(string minecraftDir)
        {
            if (selectedVersionDetails.assetIndex == null) return;

            DownloadStatus.Text = "Downloading assets...";
            
            using (WebClient client = new WebClient())
            {
                // Download assets index
                string assetsIndexUrl = selectedVersionDetails.assetIndex.url;
                string assetsIndexFile = Path.Combine(minecraftDir, "assets", "indexes", selectedVersionDetails.assetIndex.id + ".json");
                
                await client.DownloadFileTaskAsync(assetsIndexUrl, assetsIndexFile);
                
                string indexJson = File.ReadAllText(assetsIndexFile);
                assetsIndex = JsonConvert.DeserializeObject<AssetsIndex>(indexJson);
                
                // Download assets
                foreach (var asset in assetsIndex.objects)
                {
                    string hash = asset.Value.hash;
                    string hashPath = Path.Combine(hash.Substring(0, 2), hash);
                    string localPath = Path.Combine(minecraftDir, "assets", "objects", hashPath);
                    
                    if (!File.Exists(localPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                        await client.DownloadFileTaskAsync(
                            $"https://resources.download.minecraft.net/{hashPath}", 
                            localPath
                        );
                    }
                }
            }
        }

        private async Task DownloadClient(string minecraftDir)
        {
            DownloadStatus.Text = "Downloading client...";
            
            string clientJarPath = Path.Combine(minecraftDir, "versions", selectedVersionDetails.id, $"{selectedVersionDetails.id}.jar");
            
            using (WebClient client = new WebClient())
            {
                client.DownloadProgressChanged += (s, args) =>
                {
                    DownloadProgress.Value = args.ProgressPercentage;
                };

                await client.DownloadFileTaskAsync(new Uri(selectedVersionDetails.downloads.client.url), clientJarPath);
            }
        }

        private bool IsLibraryAllowed(Library library)
        {
            if (library.rules == null || library.rules.Count == 0)
                return true;

            string currentOS = Environment.OSVersion.Platform.ToString().ToLower();
            bool isWindows = currentOS.Contains("win");
            bool isLinux = currentOS.Contains("unix") || currentOS.Contains("linux");
            bool isMac = currentOS.Contains("mac");

            foreach (var rule in library.rules)
            {
                if (rule.action == "allow")
                {
                    if (rule.os == null) return true;
                    if (rule.os.name == "windows" && isWindows) return true;
                    if (rule.os.name == "linux" && isLinux) return true;
                    if (rule.os.name == "osx" && isMac) return true;
                }
                else if (rule.action == "disallow")
                {
                    if (rule.os == null) return false;
                    if (rule.os.name == "windows" && isWindows) return false;
                    if (rule.os.name == "linux" && isLinux) return false;
                    if (rule.os.name == "osx" && isMac) return false;
                }
            }

            return library.rules[0].action == "allow";
        }

        private string BuildClassPath(string minecraftDir)
        {
            var paths = new List<string>();
            
            foreach (var library in selectedVersionDetails.libraries)
            {
                if (IsLibraryAllowed(library))
                {
                    string libPath = Path.Combine(minecraftDir, "libraries", library.downloads.artifact.path);
                    paths.Add(libPath);
                }
            }
            
            // Add client jar
            string clientJar = Path.Combine(minecraftDir, "versions", selectedVersionDetails.id, $"{selectedVersionDetails.id}.jar");
            paths.Add(clientJar);
            
            return string.Join(";", paths);
        }

        private void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (selectedVersionDetails == null)
            {
                MessageBox.Show("Select and download a Minecraft version first!");
                return;
            }

            string minecraftDir = Path.GetFullPath(MinecraftDirBox.Text);
            string clientJarPath = Path.Combine(minecraftDir, "versions", selectedVersionDetails.id, $"{selectedVersionDetails.id}.jar");

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

                string classpath = BuildClassPath(minecraftDir);
                
                Process.Start("java", 
                    $"-cp \"{classpath}\" " +
                    $"{selectedVersionDetails.mainClass} " +
                    $"--username {UsernameBox.Text} " +
                    $"--version {selectedVersionDetails.id} " +
                    $"--gameDir \"{minecraftDir}\" " +
                    $"--assetsDir \"{Path.Combine(minecraftDir, "assets")}\" " +
                    $"--assetIndex {selectedVersionDetails.assetIndex?.id} " +
                    $"--accessToken 0 " +
                    $"--userType legacy " +
                    $"--versionType release " +
                    $"--online-mode false" +
                    serverArgs);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}");
            }
        }
    }

    // Your existing class definitions here...
    public class Profile { public string Username { get; set; } public string ServerIP { get; set; } public string ServerPort { get; set; } }
    public class VersionManifest { public List<Version> versions { get; set; } }
    public class Version { public string id { get; set; } public string type { get; set; } public string url { get; set; } }
    public class VersionDetails { 
        public string id { get; set; }
        public string mainClass { get; set; }
        public string assets { get; set; }
        public AssetIndex assetIndex { get; set; }
        public Downloads downloads { get; set; }
        public List<Library> libraries { get; set; }
    }

public class AssetIndex { public string id { get; set; } public string url { get; set; } }

public class Downloads { 
    public DownloadItem client { get; set; }
    public Artifact artifact { get; set; } // ADD THIS LINE!
}

public class DownloadItem { public string url { get; set; } }

// ADD THIS NEW CLASS:
public class Artifact {
    public string path { get; set; }
    public string sha1 { get; set; }
    public int size { get; set; }
    public string url { get; set; }
}

public class Library { 
    public string name { get; set; } 
    public Downloads downloads { get; set; } 
    public List<Rule> rules { get; set; }
}
    public class Rule { public string action { get; set; } public OS os { get; set; } }
    public class OS { public string name { get; set; } }
    public class AssetsIndex { public Dictionary<string, AssetObject> objects { get; set; } }
    public class AssetObject { public string hash { get; set; } public int size { get; set; } }
}
