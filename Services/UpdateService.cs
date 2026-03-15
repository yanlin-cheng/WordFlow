using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// 软件更新服务
    /// 负责检查更新、下载更新包和执行安装
    /// </summary>
    public class UpdateService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _currentVersion;
        private DateTime _lastCheckTime;
        private UpdateInfo? _cachedUpdateInfo;
        private bool _disposed;

        // 更新检查间隔（秒）
        private const int CHECK_INTERVAL_SECONDS = 3600; // 1 小时

        // GitHub 仓库配置
        private const string GITHUB_OWNER = "yanlin-cheng";
        private const string GITHUB_REPO = "WordFlow";
        private const string GITHUB_API_URL = "https://api.github.com/repos/yanlin-cheng/WordFlow/releases/latest";
        private const string GITHUB_DOWNLOAD_BASE = "https://github.com/yanlin-cheng/WordFlow/releases";

        // Gitee 仓库配置（国内用户优先）
        private const string GITEE_OWNER = "yanlin-cheng";
        private const string GITEE_REPO = "wordflow";
        private const string GITEE_API_URL = "https://gitee.com/api/v5/repos/yanlin-cheng/wordflow/releases/latest";
        private const string GITEE_DOWNLOAD_BASE = "https://gitee.com/yanlin-cheng/wordflow/releases";

        public event EventHandler<UpdateInfo>? UpdateAvailable;
        public event EventHandler<string>? UpdateCheckFailed;

        public UpdateService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            // GitHub API 需要 User-Agent
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WordFlow/1.0.0");

            _currentVersion = GetAssemblyVersion();
            
            Logger.Log($"UpdateService 初始化完成，当前版本：{_currentVersion}");
            Logger.Log($"GitHub 仓库：{GITHUB_OWNER}/{GITHUB_REPO}");
            Logger.Log($"Gitee 仓库：{GITEE_OWNER}/{GITEE_REPO}");
        }

        /// <summary>
        /// 获取当前版本号
        /// </summary>
        private string GetAssemblyVersion()
        {
            try
            {
                var version = Assembly.GetEntryAssembly()?.GetName().Version;
                return version?.ToString(3) ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        /// <summary>
        /// 检查更新（启动时自动检查）
        /// 优先使用 Gitee（国内用户），失败时尝试 GitHub（海外用户）
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdateAsync(bool force = false)
        {
            // 检查是否需要检查（避免频繁检查）
            if (!force && DateTime.Now - _lastCheckTime < TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS))
            {
                Logger.Log($"距离上次检查不足{CHECK_INTERVAL_SECONDS}秒，跳过检查");
                return null;
            }

            // 如果有缓存的更新信息且不是强制检查，直接返回
            if (!force && _cachedUpdateInfo != null)
            {
                Logger.Log("使用缓存的更新信息");
                return _cachedUpdateInfo.HasUpdate ? _cachedUpdateInfo : null;
            }

            try
            {
                Logger.Log($"开始检查更新，当前版本：{_currentVersion}");

                // 优先尝试 Gitee（国内用户）
                var updateInfo = await CheckFromGiteeAsync();
                
                if (updateInfo == null)
                {
                    // Gitee 失败或无更新，尝试 GitHub
                    Logger.Log("Gitee 检查失败或无更新，尝试 GitHub...");
                    updateInfo = await CheckFromGitHubAsync();
                }

                if (updateInfo == null)
                {
                    Logger.Log("检查更新：无更新信息返回");
                    return null;
                }

                _lastCheckTime = DateTime.Now;

                // 判断是否有新版本
                if (IsNewVersion(updateInfo.Version))
                {
                    Logger.Log($"发现新版本：{updateInfo.Version}");
                    updateInfo.HasUpdate = true;
                    _cachedUpdateInfo = updateInfo;
                    UpdateAvailable?.Invoke(this, updateInfo);
                    return updateInfo;
                }
                else
                {
                    Logger.Log("当前已是最新版本");
                    updateInfo.HasUpdate = false;
                    _cachedUpdateInfo = updateInfo;
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"检查更新失败：{ex.Message}");
                UpdateCheckFailed?.Invoke(this, $"检查更新失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从 Gitee 检查更新
        /// </summary>
        private async Task<UpdateInfo?> CheckFromGiteeAsync()
        {
            try
            {
                Logger.Log($"从 Gitee 检查更新：{GITEE_API_URL}");
                
                var response = await _httpClient.GetAsync(GITEE_API_URL);
                
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log($"Gitee 检查更新失败：HTTP {response.StatusCode}");
                    return null;
                }

                var release = await response.Content.ReadFromJsonAsync<GiteeRelease>();
                
                if (release == null)
                {
                    Logger.Log("Gitee 无更新信息返回");
                    return null;
                }

                return ConvertGiteeReleaseToUpdateInfo(release, "gitee");
            }
            catch (Exception ex)
            {
                Logger.Log($"从 Gitee 检查更新失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从 GitHub 检查更新
        /// </summary>
        private async Task<UpdateInfo?> CheckFromGitHubAsync()
        {
            try
            {
                Logger.Log($"从 GitHub 检查更新：{GITHUB_API_URL}");
                
                var response = await _httpClient.GetAsync(GITHUB_API_URL);
                
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log($"GitHub 检查更新失败：HTTP {response.StatusCode}");
                    return null;
                }

                var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
                
                if (release == null)
                {
                    Logger.Log("GitHub 无更新信息返回");
                    return null;
                }

                return ConvertGitHubReleaseToUpdateInfo(release, "github");
            }
            catch (Exception ex)
            {
                Logger.Log($"从 GitHub 检查更新失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将 GitHub Release 转换为 UpdateInfo
        /// </summary>
        private UpdateInfo ConvertGitHubReleaseToUpdateInfo(GitHubRelease release, string source)
        {
            var updateInfo = new UpdateInfo
            {
                Version = release.tag_name.TrimStart('v', 'V'),
                ReleaseDate = ParseDateTime(release.published_at),
                ReleaseNotes = release.body ?? ""
            };

            // 查找安装包（.exe 文件）
            var installerAsset = release.assets?.FirstOrDefault(a => a.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            
            if (installerAsset != null)
            {
                updateInfo.Channels = new ChannelInfo
                {
                    Stable = new ChannelDetail
                    {
                        Available = true,
                        DownloadUrl = installerAsset.browser_download_url,
                        Size = installerAsset.size,
                        SHA256 = "" // GitHub 不提供 SHA256，跳过验证
                    }
                };
            }

            return updateInfo;
        }

        /// <summary>
        /// 将 Gitee Release 转换为 UpdateInfo
        /// </summary>
        private UpdateInfo ConvertGiteeReleaseToUpdateInfo(GiteeRelease release, string source)
        {
            var updateInfo = new UpdateInfo
            {
                Version = release.tag_name.TrimStart('v', 'V'),
                ReleaseDate = ParseDateTime(release.published_at),
                ReleaseNotes = release.body ?? ""
            };

            // 查找安装包（.exe 文件）
            var installerAsset = release.assets?.FirstOrDefault(a => a.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            
            if (installerAsset != null)
            {
                updateInfo.Channels = new ChannelInfo
                {
                    Stable = new ChannelDetail
                    {
                        Available = true,
                        DownloadUrl = installerAsset.browser_download_url,
                        Size = installerAsset.size,
                        SHA256 = "" // Gitee 不提供 SHA256，跳过验证
                    }
                };
            }

            return updateInfo;
        }

        /// <summary>
        /// 解析日期时间字符串
        /// </summary>
        private DateTime ParseDateTime(string? dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return DateTime.Now;

            if (DateTime.TryParse(dateString, out var result))
                return result;

            return DateTime.Now;
        }

        /// <summary>
        /// 判断是否为新版本
        /// </summary>
        private bool IsNewVersion(string newVersion)
        {
            try
            {
                var current = Version.Parse(_currentVersion);
                var newVer = Version.Parse(newVersion);
                return newVer > current;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 下载更新包
        /// </summary>
        public async Task<string> DownloadUpdateAsync(
            UpdateInfo updateInfo, 
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (updateInfo.Channels?.Stable == null || string.IsNullOrEmpty(updateInfo.Channels.Stable.DownloadUrl))
            {
                throw new Exception("未找到可用的下载链接");
            }

            var downloadUrl = updateInfo.Channels.Stable.DownloadUrl;
            var downloadPath = Path.Combine(
                Path.GetTempPath(), 
                "WordFlow_Updates",
                $"WordFlow_Setup_{updateInfo.Version}.exe");

            Directory.CreateDirectory(Path.GetDirectoryName(downloadPath)!);

            Logger.Log($"开始下载更新包：{downloadUrl}");
            Logger.Log($"下载路径：{downloadPath}");

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            
            using var response = await _httpClient.SendAsync(
                request, 
                HttpCompletionOption.ResponseHeadersRead, 
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalSize = response.Content.Headers.ContentLength ?? 0;
            
            using var fileStream = new FileStream(
                downloadPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[81920];
            var totalRead = 0L;
            var sw = Stopwatch.StartNew();
            var speedSamples = new Queue<long>();
            var lastProgressTime = DateTime.Now;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read = await stream.ReadAsync(
                    buffer.AsMemory(0, buffer.Length), 
                    cancellationToken);

                if (read == 0) break;

                await fileStream.WriteAsync(
                    buffer.AsMemory(0, read), 
                    cancellationToken);

                totalRead += read;

                // 计算速度
                speedSamples.Enqueue(read);
                if (speedSamples.Count > 10)
                {
                    speedSamples.Dequeue();
                }

                // 限制进度更新频率（每 200ms 更新一次）
                var now = DateTime.Now;
                if ((now - lastProgressTime).TotalMilliseconds >= 200)
                {
                    var speed = speedSamples.Count > 0 ? speedSamples.Average() * 10 : 0;
                    var remainingTime = speed > 0 ? TimeSpan.FromSeconds((totalSize - totalRead) / speed) : TimeSpan.Zero;

                    progress?.Report(new DownloadProgress
                    {
                        TotalSize = totalSize,
                        DownloadedSize = totalRead,
                        Percentage = totalSize > 0 ? (double)totalRead / totalSize * 100 : 0,
                        Speed = FormatSpeed(speed),
                        RemainingTime = remainingTime
                    });

                    lastProgressTime = now;
                }
            }

            sw.Stop();
            Logger.Log($"下载完成：{downloadPath}，总大小：{FormatSize(totalRead)}，耗时：{sw.Elapsed}");

            return downloadPath;
        }

        /// <summary>
        /// 验证更新包完整性
        /// </summary>
        public async Task<bool> ValidatePackageAsync(string filePath, string? expectedSha256)
        {
            if (string.IsNullOrEmpty(expectedSha256))
            {
                Logger.Log("未提供 SHA256，跳过验证");
                return true; // 没有期望的哈希值，跳过验证
            }

            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = await sha256.ComputeHashAsync(stream);
                var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();

                var isValid = hashString == expectedSha256.ToLower();
                Logger.Log($"SHA256 验证：{(isValid ? "通过" : "失败")}");
                Logger.Log($"期望：{expectedSha256}");
                Logger.Log($"实际：{hashString}");

                return isValid;
            }
            catch (Exception ex)
            {
                Logger.Log($"验证更新包失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行安装
        /// </summary>
        public async Task<bool> InstallAsync(string installerPath)
        {
            try
            {
                Logger.Log($"开始安装更新：{installerPath}");

                // 准备安装参数（静默安装）
                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                    UseShellExecute = true
                };

                // 启动安装程序
                using var installer = new Process { StartInfo = startInfo };
                installer.Start();

                // 等待安装完成
                await Task.Run(() =>
                {
                    installer.WaitForExit();
                });

                if (installer.ExitCode != 0)
                {
                    Logger.Log($"安装失败，退出码：{installer.ExitCode}");
                    return false;
                }

                Logger.Log("安装成功");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"安装失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 跳过此版本
        /// </summary>
        public void SkipVersion(string version)
        {
            var settingsService = new SettingsService();
            settingsService.Settings.SkippedVersion = version;
            settingsService.Save();
            Logger.Log($"跳过版本：{version}");
            _cachedUpdateInfo = null; // 清除缓存
        }

        /// <summary>
        /// 稍后提醒
        /// </summary>
        public void RemindLater(TimeSpan delay)
        {
            Logger.Log($"设置稍后提醒：{delay}");
            Task.Run(async () =>
            {
                await Task.Delay(delay);
                // 重新检查更新
                await CheckForUpdateAsync(true);
            });
        }

        /// <summary>
        /// 格式化速度显示
        /// </summary>
        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond} B/s";
            if (bytesPerSecond < 1024 * 1024)
                return $"{bytesPerSecond / 1024.0:F1} KB/s";
            return $"{bytesPerSecond / (1024.0 * 1024):F1} MB/s";
        }

        /// <summary>
        /// 格式化大小显示
        /// </summary>
        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 更新信息
    /// </summary>
    public class UpdateInfo
    {
        public string Version { get; set; } = "0.0.0";
        public int BuildNumber { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string MinVersion { get; set; } = "";
        public ChannelInfo? Channels { get; set; }
        public List<ChangeInfo> Changes { get; set; } = new();
        public bool Urgent { get; set; }
        public string ReleaseNotes { get; set; } = "";
        
        // 本地使用
        public bool HasUpdate { get; set; }
        
        /// <summary>
        /// 获取下载链接
        /// </summary>
        public string DownloadUrl => Channels?.Stable?.DownloadUrl ?? "";
        
        /// <summary>
        /// 获取文件大小
        /// </summary>
        public long FileSize => Channels?.Stable?.Size ?? 0;
        
        /// <summary>
        /// 获取 SHA256
        /// </summary>
        public string SHA256 => Channels?.Stable?.SHA256 ?? "";
    }

    /// <summary>
    /// 频道信息
    /// </summary>
    public class ChannelInfo
    {
        public ChannelDetail? Stable { get; set; }
        public ChannelDetail? Beta { get; set; }
    }

    /// <summary>
    /// 频道详情
    /// </summary>
    public class ChannelDetail
    {
        public bool Available { get; set; }
        public string DownloadUrl { get; set; } = "";
        public string SHA256 { get; set; } = "";
        public long Size { get; set; }
    }

    /// <summary>
    /// 变更信息
    /// </summary>
    public class ChangeInfo
    {
        public string Type { get; set; } = ""; // feature, improvement, fix
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// 下载进度
    /// </summary>
    public class DownloadProgress
    {
        public long TotalSize { get; set; }
        public long DownloadedSize { get; set; }
        public double Percentage { get; set; }
        public string Speed { get; set; } = "";
        public TimeSpan RemainingTime { get; set; }
    }

    /// <summary>
    /// GitHub Release API 响应格式
    /// </summary>
    public class GitHubRelease
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public string body { get; set; }
        public string published_at { get; set; }
        public List<GitHubAsset> assets { get; set; }
    }

    /// <summary>
    /// GitHub Asset API 响应格式
    /// </summary>
    public class GitHubAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
    }

    /// <summary>
    /// Gitee Release API 响应格式
    /// </summary>
    public class GiteeRelease
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public string body { get; set; }
        public string published_at { get; set; }
        public List<GiteeAsset> assets { get; set; }
    }

    /// <summary>
    /// Gitee Asset API 响应格式
    /// </summary>
    public class GiteeAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
    }
}
