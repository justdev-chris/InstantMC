using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;
using System.Windows.Media;

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

        // === THEME SYSTEM ===
        private void LoadTheme()
        {
            try
            {
                if (File.Exists(themeFile))
                {
                    string theme = File.ReadAllText(themeFile).Trim();
                    ApplyTheme(theme);
                    
                    // Set combobox selection
                    foreach (ComboBoxItem item in ThemeSelector.Items)
                    {
                        if (item.Content.ToString().StartsWith(theme, StringComparison.OrdinalIgnoreCase))
                        {
                            ThemeSelector.SelectedItem = item;
                            break;
                        }
                    }
                }
                else
                {
                    ApplyTheme("Dark");
                    ThemeSelector.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load theme: {ex.Message}");
                ApplyTheme("Dark");
            }
        }

        private void ApplyTheme(string theme)
        {
            if (theme.StartsWith("Dark"))
            {
                this.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                this.Foreground = Brushes.White;
            }
            else if (theme.StartsWith("Light"))
            {
                this.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                this.Foreground = Brushes.Black;
            }
            // Custom themes can be added later
        }

        private void ThemeSelector_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSelector.SelectedItem is ComboBoxItem item)
            {
                string theme = item.Content.ToString();
                ApplyTheme(theme);
                File.WriteAllText(themeFile, theme);
            }
        }

        // === MODS SUPPORT ===
        private void OpenModsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string minecraftDir = Path.GetFullPath(MinecraftDirBox.Text);
                string modsDir = Path.Combine(minecraftDir, "mods");
                
                Directory.CreateDirectory(modsDir);
                Process.Start("explorer.exe", modsDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open mods folder: {ex.Message}");
            }
        }

        // === EXISTING CODE (updated for PawCraft) ===
        private void ShowEULAWarning()
        {
            MessageBox.Show("By using PawCraft, you agree to Minecraft's EULA.\nMake sure you own the game!");
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
            MessageBox.Show("Java not found! Please install Java from https://java.com");
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
                    string manifestJson = await client.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
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
                RefreshVersionsBtn.Content = "Refresh Versions";
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
                    string manifestJson = await client.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
                    VersionManifest manifest = JsonConvert.DeserializeObject<VersionManifest>(manifestJson);
                    
                    var selectedVersion = manifest.versions.FirstOrDefault(v => v.id == versionId);
                    if (selectedVersion != null)
                    {
                        string versionJson = await client.GetStringAsync(selectedVersion.url);
                        selectedVersionDetails = JsonConvert.DeserializeObject<VersionDetails>(versionJson);
                        
                        DownloadStatus.Text = $"Version {versionId} loaded - Ready to download";
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
            MessageBox.Show($"Profile '{profileName}' saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                
                // Create directories (INCLUDING MODS)
                Directory.CreateDirectory(minecraftDir);
                Directory.CreateDirectory(Path.Combine(minecraftDir, "versions", selectedVersionDetails.id));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "libraries"));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "assets", "objects"));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "assets", "indexes"));
                Directory.CreateDirectory(Path.Combine(minecraftDir, "mods")); // MODS FOLDER!

                // Download everything
                await DownloadLibraries(minecraftDir);
                await DownloadAssets(minecraftDir);
                await DownloadClient(minecraftDir);

                DownloadStatus.Text = "Download complete!";
                MessageBox.Show("Game downloaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DownloadStatus.Text = "Download failed!";
                MessageBox.Show($"Download failed: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                int errorCount = 0;
                
                using (HttpClient client = new HttpClient())
                {
                    foreach (var library in selectedVersionDetails.libraries)
                    {
                        try
                        {
                            if (!IsLibraryAllowed(library)) 
                            {
                                continue;
                            }

                            if (library.downloads == null || library.downloads.artifact == null)
                            {
                                errorCount++;
                                continue;
                            }

                            string libPath = Path.Combine(minecraftDir, "libraries", library.downloads.artifact.path);
                            string libDir = Path.GetDirectoryName(libPath);
                            
                            Directory.CreateDirectory(libDir);
                            
                            if (!File.Exists(libPath))
                            {
                                DownloadStatus.Text = $"Downloading {library.name}...";
                                
                                byte[] fileData = await client.GetByteArrayAsync(library.downloads.artifact.url);
                                File.WriteAllBytes(libPath, fileData);
                                
                                downloadedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                        }
                    }
                }

                if (errorCount > 0)
                {
                    MessageBox.Show($"Libraries download complete!\nDownloaded: {downloadedCount}\nErrors: {errorCount}", "Libraries Status", MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
                if (selectedVersionDetails.assetIndex == null) 
                {
                    MessageBox.Show("No assets index found for this version!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DownloadStatus.Text = "Downloading assets...";
                
                using (HttpClient client = new HttpClient())
                {
                    // Download assets index
                    string assetsIndexUrl = selectedVersionDetails.assetIndex.url;
                    string assetsIndexFile = Path.Combine(minecraftDir, "assets", "indexes", selectedVersionDetails.assetIndex.id + ".json");
                    
                    byte[] indexData = await client.GetByteArrayAsync(assetsIndexUrl);
                    File.WriteAllBytes(assetsIndexFile, indexData);
                    
                    string indexJson = File.ReadAllText(assetsIndexFile);
                    assetsIndex = JsonConvert.DeserializeObject<AssetsIndex>(indexJson);
                    
                    if (assetsIndex?.objects == null)
                    {
                        MessageBox.Show("No assets found in assets index!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    int assetCount = 0;
                    int assetErrors = 0;
                    
                    // Download assets
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
                                
                                byte[] assetData = await client.GetByteArrayAsync(
                                    $"https://resources.download.minecraft.net/{hashPath}"
                                );
                                File.WriteAllBytes(localPath, assetData);
                                
                                assetCount++;
                                
                                if (assetCount % 50 == 0)
                                {
                                    DownloadStatus.Text = $"Downloaded {assetCount} assets...";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            assetErrors++;
                        }
                    }

                    if (assetErrors > 0)
                    {
                        MessageBox.Show($"Assets download complete!\nDownloaded: {assetCount}\nErrors: {assetErrors}", "Assets Status", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
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
                    
                    MessageBox.Show("Client jar downloaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
            
            // Add client jar
            string clientJar = Path.Combine(minecraftDir, "versions", selectedVersionDetails.id, $"{selectedVersionDetails.id}.jar");
            paths.Add(clientJar);
            
            // ADD MODS TO CLASSPATH
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
                
                Process.Start(JavaPathBox.Text, 
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
                    
                MessageBox.Show("Minecraft launched!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // === DATA CLASSES ===
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
