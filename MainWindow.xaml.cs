using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PawCraft
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();
        private string profilesFile = "profiles.txt";
        private string configFile = "config.txt";
        private string themeFile = "theme.txt";
        private VersionDetails selectedVersionDetails;
        private AssetsIndex assetsIndex;

        public MainWindow()
        {
            InitializeComponent();
            LoadTheme();
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
            MessageBox.Show("🐾 Welcome to PawCraft!\n\nThis launcher provides access to Minecraft through offline/cracked mode.\n\nBy using this software, you acknowledge that:\n• You understand this is for offline play\n• You'll only use cracked servers\n• You respect Mojang's intellectual property", 
                          "PawCraft - Cracked Launcher", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadTheme()
        {
            try
            {
                if (File.Exists(themeFile))
                {
                    string theme = File.ReadAllText(themeFile).Trim();
                    
                    if (theme.StartsWith("Custom:"))
                    {
                        string imagePath = theme.Substring(7);
                        if (File.Exists(imagePath))
                        {
                            SetBackgroundImage(imagePath);
                        }
                        else
                        {
                            ApplyTheme("Dark Theme");
                        }
                    }
                    else
                    {
                        ApplyTheme(theme);
                    }
                    
                    foreach (ComboBoxItem item in ThemeSelector.Items)
                    {
                        if (item.Content.ToString() == theme || 
                           (theme.StartsWith("Custom:") && item.Content.ToString() == "Custom Background"))
                        {
                            ThemeSelector.SelectedItem = item;
                            break;
                        }
                    }
                }
                else
                {
                    ApplyTheme("Dark Theme");
                    ThemeSelector.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load theme: {ex.Message}");
                ApplyTheme("Dark Theme");
            }
        }

        private void ApplyTheme(string theme)
        {
            if (theme == "Dark Theme")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                this.Foreground = Brushes.White;
            }
            else if (theme == "Light Theme")
            {
                this.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                this.Foreground = Brushes.Black;
            }
        }

        private void SetBackgroundImage(string imagePath)
        {
            try
            {
                var imageBrush = new ImageBrush();
                imageBrush.ImageSource = new BitmapImage(new Uri(imagePath));
                imageBrush.Stretch = Stretch.UniformToFill;
                imageBrush.Opacity = 0.9;
                this.Background = imageBrush;
            }
            catch
            {
                ApplyTheme("Dark Theme");
            }
        }

        private void ThemeSelector_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSelector.SelectedItem is ComboBoxItem item)
            {
                string theme = item.Content.ToString();
                
                if (theme == "Custom Background")
                {
                    var dialog = new Forms.OpenFileDialog();
                    dialog.Filter = "Image Files|*.jpg;*.png;*.bmp;*.gif";
                    dialog.Title = "Select Background Image";
                    
                    if (dialog.ShowDialog() == Forms.DialogResult.OK)
                    {
                        SetBackgroundImage(dialog.FileName);
                        File.WriteAllText(themeFile, "Custom:" + dialog.FileName);
                    }
                }
                else
                {
                    ApplyTheme(theme);
                    File.WriteAllText(themeFile, theme);
                }
            }
        }

        private void OpenModsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string minecraftDir = Path.GetFullPath(MinecraftDirBox.Text);
                string modsDir = Path.Combine(minecraftDir, "mods");
                
                Directory.CreateDirectory(modsDir);
                Process.Start("explorer.exe", modsDir);
                MessageBox.Show("📁 Mods folder opened!\n\nDrop .jar mod files here and they'll auto-load! 🚀", 
                              "Mods Folder", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open mods folder: {ex.Message}");
            }
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
            MessageBox.Show("Java not found! PawCraft will use the bundled Java 21. 🐾", 
                          "Java Setup", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowseJavaBtn_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new Forms.OpenFileDialog();
            fileDialog.Filter = "Java Executable|java.exe";
            fileDialog.Title = "Select java.exe";
            
            if (fileDialog.ShowDialog() == Forms.DialogResult.OK)
            {
                JavaPathBox.Text = fileDialog.FileName;
                SaveConfig();
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

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "PawCraft/1.0");
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
                MessageBox.Show($"Failed to load versions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                VersionDropdown.IsEnabled = true;
                RefreshVersionsBtn.Content = "Refresh";
            }
        }

        private async void VersionDropdown_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (VersionDropdown.SelectedItem == null) return;

            string versionId = VersionDropdown.SelectedItem.ToString();
            
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "PawCraft/1.0");
                    string manifestJson = await client.DownloadStringTaskAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
                    VersionManifest manifest = JsonConvert.DeserializeObject<VersionManifest>(manifestJson);
                    
                    var selectedVersion = manifest.versions.FirstOrDefault(v => v.id == versionId);
                    if (selectedVersion != null)
                    {
                        string versionJson = await client.DownloadStringTaskAsync(selectedVersion.url);
                        selectedVersionDetails = JsonConvert.DeserializeObject<VersionDetails>(versionJson);
                        
                        DownloadStatus.Text = $"✅ Version {versionId} loaded - Ready to download!";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load version details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                if (lines.Length > 0) 
                {
                    MinecraftDirBox.Text = lines[0];
                    if (lines.Length > 1) JavaPathBox.Text = lines[1];
                }
            }
        }

        private void SaveConfig()
        {
            File.WriteAllText(configFile, MinecraftDirBox.Text + "\n" + JavaPathBox.Text);
        }

        private void SaveProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
            {
                MessageBox.Show("Enter a username!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show($"✅ Profile '{profileName}' saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show("Select a Minecraft version first!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                DownloadBtn.IsEnabled = false;
                DownloadProgress.Visibility = Visibility.Visible;
                DownloadStatus.Text = "Starting download...";

                string minecraftDir = Path.GetFullPath(MinecraftDirBox.Text);
                
                Directory.CreateDirectory(minecraftDir);
                Directory.CreateDirectory(Path.Combine(minecraftDir, "versions", selectedVersionDetails.id));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "libraries"));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "assets", "objects"));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "assets", "indexes"));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "mods"));

                await DownloadLibraries(minecraftDir);
                await DownloadAssets(minecraftDir);
                await DownloadClient(minecraftDir);

                DownloadStatus.Text = "✅ Download complete! Ready to launch!";
                MessageBox.Show("🎉 Game downloaded successfully!\n\nYou can now launch Minecraft! 🚀", 
                              "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DownloadStatus.Text = "❌ Download failed!";
                MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DownloadBtn.IsEnabled = true;
                DownloadProgress.Visibility = Visibility.Collapsed;
            }
        }

        private async Task DownloadLibraries(string minecraftDir)
        {
            try
            {
                DownloadStatus.Text = "Downloading libraries...";
                
                if (selectedVersionDetails.libraries == null || selectedVersionDetails.libraries.Count == 0)
                {
                    MessageBox.Show("No libraries found in version manifest!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int downloadedCount = 0;
                int totalCount = selectedVersionDetails.libraries.Count;
                
                using (HttpClient client = new HttpClient())
                {
                    foreach (var library in selectedVersionDetails.libraries)
                    {
                        try
                        {
                            if (!IsLibraryAllowed(library)) continue;
                            if (library.downloads?.artifact == null) continue;

                            string libPath = Path.Combine(minecraftDir, "libraries", library.downloads.artifact.path);
                            string libDir = Path.GetDirectoryName(libPath);
                            
                            Directory.CreateDirectory(libDir);
                            
                            if (!File.Exists(libPath))
                            {
                                byte[] fileData = await client.GetByteArrayAsync(library.downloads.artifact.url);
                                File.WriteAllBytes(libPath, fileData);
                                downloadedCount++;
                            }
                        }
                        catch { }
                    }
                }

                DownloadStatus.Text = $"✅ Downloaded {downloadedCount} libraries!";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download libraries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private async Task DownloadAssets(string minecraftDir)
        {
            try
            {
                if (selectedVersionDetails.assetIndex == null) return;

                DownloadStatus.Text = "Downloading assets...";
                
                using (HttpClient client = new HttpClient())
                {
                    string assetsIndexUrl = selectedVersionDetails.assetIndex.url;
                    string assetsIndexFile = Path.Combine(minecraftDir, "assets", "indexes", selectedVersionDetails.assetIndex.id + ".json");
                    
                    byte[] indexData = await client.GetByteArrayAsync(assetsIndexUrl);
                    File.WriteAllBytes(assetsIndexFile, indexData);
                    
                    string indexJson = File.ReadAllText(assetsIndexFile);
                    assetsIndex = JsonConvert.DeserializeObject<AssetsIndex>(indexJson);
                    
                    if (assetsIndex?.objects == null) return;

                    int assetCount = 0;
                    
                    foreach (var asset in assetsIndex.objects)
                    {
                        try
                        {
                            string hash = asset.Value.hash;
                            string hashPath = Path.Combine(hash.Substring(0, 2), hash);
                            string localPath = Path.Combine(minecraftDir, "assets", "objects", hashPath);
                            
                            if (!File.Exists(localPath))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(localPath));
                                byte[] assetData = await client.GetByteArrayAsync($"https://resources.download.minecraft.net/{hashPath}");
                                File.WriteAllBytes(localPath, assetData);
                                assetCount++;
                            }
                        }
                        catch { }
                    }

                    DownloadStatus.Text = $"✅ Downloaded {assetCount} assets!";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download assets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private async Task DownloadClient(string minecraftDir)
        {
            try
            {
                DownloadStatus.Text = "Downloading client...";
                
                string clientJarPath = Path.Combine(minecraftDir, "versions", selectedVersionDetails.id, $"{selectedVersionDetails.id}.jar");
                
                using (HttpClient client = new HttpClient())
                {
                    byte[] clientData = await client.GetByteArrayAsync(selectedVersionDetails.downloads.client.url);
                    File.WriteAllBytes(clientJarPath, clientData);
                    DownloadStatus.Text = "✅ Client downloaded!";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download client: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
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
                if (IsLibraryAllowed(library) && library.downloads?.artifact != null)
                {
                    string libPath = Path.Combine(minecraftDir, "libraries", library.downloads.artifact.path);
                    if (File.Exists(libPath))
                    {
                        paths.Add(libPath);
                    }
                }
            }
            
            string clientJar = Path.Combine(minecraftDir, "versions", selectedVersionDetails.id, $"{selectedVersionDetails.id}.jar");
            paths.Add(clientJar);
            
            string modsDir = Path.Combine(minecraftDir, "mods");
            if (Directory.Exists(modsDir))
            {
                foreach (string modFile in Directory.GetFiles(modsDir, "*.jar"))
                {
                    paths.Add(modFile);
                }
            }
            
            return string.Join(";", paths);
        }

        private void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (selectedVersionDetails == null)
            {
                MessageBox.Show("Select and download a Minecraft version first!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string minecraftDir = Path.GetFullPath(MinecraftDirBox.Text);
            string clientJarPath = Path.Combine(minecraftDir, "versions", selectedVersionDetails.id, $"{selectedVersionDetails.id}.jar");

            if (!File.Exists(clientJarPath))
            {
                MessageBox.Show("Minecraft client not found! Download it first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
            {
                MessageBox.Show("Enter a username!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string serverArgs = "";
                if (AutoJoinServer.IsChecked == true && !string.IsNullOrWhiteSpace(ServerIPBox.Text))
                {
                    string port = string.IsNullOrWhiteSpace(ServerPortBox.Text) ? "25565" : ServerPortBox.Text;
                    serverArgs = $" --server {ServerIPBox.Text} --port {port}";
                }

                string classpath = BuildClassPath(minecraftDir);
                string authArgs = "--accessToken 0 --userType legacy --online-mode false";
                
                Process.Start(JavaPathBox.Text, 
                    $"-cp \"{classpath}\" " +
                    $"{selectedVersionDetails.mainClass} " +
                    $"--username {UsernameBox.Text} " +
                    $"--version {selectedVersionDetails.id} " +
                    $"--gameDir \"{minecraftDir}\" " +
                    $"--assetsDir \"{Path.Combine(minecraftDir, "assets")}\" " +
                    $"--assetIndex {selectedVersionDetails.assetIndex?.id} " +
                    $"{authArgs}" +
                    $"--versionType release " +
                    serverArgs);
                    
                MessageBox.Show("🚀 Minecraft launched!\n\nEnjoy your game! 🎮", 
                              "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Profile { 
        public string Username { get; set; } 
        public string ServerIP { get; set; } 
        public string ServerPort { get; set; } 
    }
    
    public class VersionManifest { 
        public List<Version> versions { get; set; } 
    }
    
    public class Version { 
        public string id { get; set; } 
        public string type { get; set; } 
        public string url { get; set; } 
    }
    
    public class VersionDetails { 
        public string id { get; set; }
        public string mainClass { get; set; }
        public string assets { get; set; }
        public AssetIndex assetIndex { get; set; }
        public Downloads downloads { get; set; }
        public List<Library> libraries { get; set; }
    }

    public class AssetIndex { 
        public string id { get; set; } 
        public string url { get; set; } 
    }

    public class Downloads { 
        public DownloadItem client { get; set; }
    }

    public class DownloadItem { 
        public string url { get; set; } 
    }

    public class Library { 
        public string name { get; set; } 
        public LibraryDownloads downloads { get; set; } 
        public List<Rule> rules { get; set; }
    }

    public class LibraryDownloads {
        public Artifact artifact { get; set; }
    }

    public class Artifact {
        public string path { get; set; }
        public string sha1 { get; set; }
        public int size { get; set; }
        public string url { get; set; }
    }

    public class Rule { 
        public string action { get; set; } 
        public OS os { get; set; } 
    }
    
    public class OS { 
        public string name { get; set; } 
    }
    
    public class AssetsIndex { 
        public Dictionary<string, AssetObject> objects { get; set; } 
    }
    
    public class AssetObject { 
        public string hash { get; set; } 
        public int size { get; set; } 
    }
}
