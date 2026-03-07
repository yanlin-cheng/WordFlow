# WordFlow 软件更新功能设计文档

> 文档版本：1.0  
> 创建日期：2026-03-07  
> 状态：设计稿

---

## 目录

1. [更新检查机制](#1-更新检查机制)
2. [更新包管理](#2-更新包管理)
3. [更新流程设计](#3-更新流程设计)
4. [版本兼容性检查](#4-版本兼容性检查)
5. [更新日志展示](#5-更新日志展示)

---

## 1. 更新检查机制

### 1.1 检查策略

| 检查类型 | 说明 | 频率 |
|---------|------|------|
| **自动检查** | 启动时后台检查 | 每次启动 |
| **手动检查** | 用户点击检查 | 随时 |
| **强制检查** | 重大版本更新 | 立即 |

### 1.2 更新源配置

```json
{
  "update": {
    "endpoint": "https://api.wordflow.com/v1/update",
    "cdn": "https://cdn.wordflow.com/releases",
    "checkInterval": 86400,
    "autoDownload": true,
    "autoInstall": false
  }
}
```

### 1.3 版本信息格式

```json
{
  "version": "2.1.0",
  "buildNumber": 20260307,
  "releaseDate": "2026-03-07T00:00:00Z",
  "minVersion": "2.0.0",
  "channels": {
    "stable": {
      "available": true,
      "downloadUrl": "https://cdn.wordflow.com/releases/2.1.0/WordFlow_Setup_2.1.0.exe",
      "sha256": "abc123...",
      "size": 65000000
    },
    "beta": {
      "available": true,
      "downloadUrl": "https://cdn.wordflow.com/releases/2.1.0-beta/WordFlow_Setup_2.1.0-beta.exe",
      "sha256": "def456...",
      "size": 66000000
    }
  },
  "changes": [
    {
      "type": "feature",
      "title": "新增快捷短语功能",
      "description": "支持语音触发快捷短语，一键展开常用文本"
    },
    {
      "type": "improvement",
      "title": "优化识别速度",
      "description": "识别延迟降低 30%"
    },
    {
      "type": "fix",
      "title": "修复已知问题",
      "description": "修复了部分场景下识别失败的问题"
    }
  ],
  "urgent": false,
  "releaseNotes": "https://wordflow.com/releases/2.1.0/notes"
}
```

### 1.4 更新检查服务

```csharp
public class UpdateService
{
    private readonly HttpClient _httpClient;
    private readonly string _currentVersion;
    private readonly string _updateEndpoint;
    private DateTime _lastCheckTime;
    
    public UpdateService()
    {
        _currentVersion = GetAssemblyVersion();
        _updateEndpoint = ConfigurationManager.AppSettings["UpdateEndpoint"];
    }
    
    /// <summary>
    /// 检查更新
    /// </summary>
    public async Task<UpdateInfo> CheckForUpdateAsync(bool force = false)
    {
        // 检查是否需要检查（避免频繁检查）
        if (!force && DateTime.Now - _lastCheckTime < TimeSpan.FromSeconds(3600))
        {
            return null; // 1 小时内不重复检查
        }
        
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_updateEndpoint}?version={_currentVersion}&platform=win");
            
            response.EnsureSuccessStatusCode();
            
            var updateInfo = await response.Content.ReadFromJsonAsync<UpdateInfo>();
            
            _lastCheckTime = DateTime.Now;
            
            // 判断是否有新版本
            if (IsNewVersion(updateInfo.Version))
            {
                return updateInfo;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error($"检查更新失败：{ex}");
            return null;
        }
    }
    
    /// <summary>
    /// 判断是否为新版本
    /// </summary>
    private bool IsNewVersion(string newVersion)
    {
        return Version.Parse(newVersion) > Version.Parse(_currentVersion);
    }
    
    /// <summary>
    /// 获取当前版本号
    /// </summary>
    private string GetAssemblyVersion()
    {
        return Assembly.GetEntryAssembly()
            .GetName()
            .Version
            .ToString(3);
    }
}

public class UpdateInfo
{
    public string Version { get; set; }
    public int BuildNumber { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string MinVersion { get; set; }
    public ChannelInfo Channels { get; set; }
    public List<ChangeInfo> Changes { get; set; }
    public bool Urgent { get; set; }
    public string ReleaseNotes { get; set; }
    
    // 本地使用
    public bool HasUpdate { get; set; }
    public DownloadChannel Channel { get; set; }
}

public class ChannelInfo
{
    public ChannelDetail Stable { get; set; }
    public ChannelDetail Beta { get; set; }
}

public class ChannelDetail
{
    public bool Available { get; set; }
    public string DownloadUrl { get; set; }
    public string SHA256 { get; set; }
    public long Size { get; set; }
}

public class ChangeInfo
{
    public string Type { get; set; } // feature, improvement, fix
    public string Title { get; set; }
    public string Description { get; set; }
}

public enum DownloadChannel
{
    Stable,
    Beta
}
```

---

## 2. 更新包管理

### 2.1 更新包类型

| 类型 | 说明 | 大小 | 适用场景 |
|------|------|------|---------|
| **全量包** | 完整安装包 | ~60MB | 首次安装、跨版本升级 |
| **增量包** | 差异更新包 | ~5-20MB | 小版本升级 |
| **热更新** | 代码/资源补丁 | ~1MB | 紧急修复 |

### 2.2 增量更新生成

```python
# 构建服务器脚本
import os
import hashlib
from bsdiff4 import diff

def generate_diff_package(old_version: str, new_version: str) -> dict:
    """生成增量更新包"""
    
    old_dir = f"releases/{old_version}"
    new_dir = f"releases/{new_version}"
    diff_dir = f"releases/{new_version}/diff"
    
    os.makedirs(diff_dir, exist_ok=True)
    
    diff_files = []
    
    # 比较文件
    for root, dirs, files in os.walk(new_dir):
        for file in files:
            if file.endswith('.diff'):
                continue
                
            new_path = os.path.join(root, file)
            rel_path = os.path.relpath(new_path, new_dir)
            old_path = os.path.join(old_dir, rel_path)
            
            if os.path.exists(old_path):
                # 生成差异文件
                diff_path = os.path.join(diff_dir, rel_path + '.diff')
                os.makedirs(os.path.dirname(diff_path), exist_ok=True)
                
                with open(old_path, 'rb') as f1, open(new_path, 'rb') as f2:
                    patch = diff(f1.read(), f2.read())
                    with open(diff_path, 'wb') as f3:
                        f3.write(patch)
                
                diff_files.append({
                    'path': rel_path,
                    'type': 'diff',
                    'size': os.path.getsize(diff_path)
                })
            else:
                # 新文件，直接复制
                new_diff_path = os.path.join(diff_dir, rel_path + '.new')
                os.makedirs(os.path.dirname(new_diff_path), exist_ok=True)
                
                with open(new_path, 'rb') as f1, open(new_diff_path, 'wb') as f2:
                    f2.write(f1.read())
                
                diff_files.append({
                    'path': rel_path,
                    'type': 'new',
                    'size': os.path.getsize(new_diff_path)
                })
    
    # 生成清单
    manifest = {
        'version': new_version,
        'base_version': old_version,
        'files': diff_files,
        'total_size': sum(f['size'] for f in diff_files)
    }
    
    with open(os.path.join(diff_dir, 'manifest.json'), 'w') as f:
        json.dump(manifest, f, indent=2)
    
    return manifest
```

### 2.3 更新包验证

```csharp
public class UpdatePackageValidator
{
    /// <summary>
    /// 验证更新包完整性
    /// </summary>
    public async Task<bool> ValidatePackageAsync(string filePath, string expectedSha256)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();
        
        return hashString == expectedSha256.ToLower();
    }
    
    /// <summary>
    /// 验证数字签名
    /// </summary>
    public bool VerifySignature(string filePath)
    {
        try
        {
            var signer = X509Certificate.CreateFromSignedFile(filePath);
            if (signer == null)
            {
                return false;
            }
            
            // 验证证书链
            var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            
            var valid = chain.Build(new X509Certificate2(signer));
            
            return valid;
        }
        catch
        {
            return false;
        }
    }
}
```

---

## 3. 更新流程设计

### 3.1 完整更新流程

```
┌─────────────────────────────────────────────────────────────┐
│                    软件更新流程                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 检查更新                                                 │
│     ↓                                                       │
│  发现新版本？── 否 ──→ 结束                                 │
│     ↓ 是                                                    │
│  2. 显示更新对话框                                           │
│     ↓                                                       │
│  用户选择？                                                 │
│     ├─ 稍后提醒 ──→ 设置定时提醒，结束                      │
│     ├─ 跳过此版本 ──→ 记录跳过版本，结束                    │
│     └─ 立即更新 ──→ 继续                                    │
│     ↓                                                       │
│  3. 后台下载更新包                                           │
│     ↓                                                       │
│  下载完成？                                                 │
│     ├─ 失败 ──→ 重试（最多 3 次）→ 仍失败则提示错误            │
│     └─ 成功 ──→ 继续                                        │
│     ↓                                                       │
│  4. 验证更新包                                               │
│     ↓                                                       │
│  验证通过？                                                 │
│     ├─ 否 ──→ 删除文件，提示错误                             │
│     └─ 是 ──→ 继续                                          │
│     ↓                                                       │
│  5. 准备安装                                                 │
│     ↓                                                       │
│  关闭主程序？                                               │
│     ├─ 用户取消 ──→ 结束                                    │
│     └─ 确认 ──→ 继续                                        │
│     ↓                                                       │
│  6. 运行安装程序                                             │
│     ↓                                                       │
│  安装完成 ──→ 自动启动新版本                                 │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 更新对话框

```csharp
public class UpdateDialog : Window
{
    public UpdateDialog(UpdateInfo updateInfo)
    {
        InitializeComponent();
        
        VersionText.Text = $"新版本：v{updateInfo.Version}";
        ReleaseDateText.Text = $"发布日期：{updateInfo.ReleaseDate:yyyy-MM-dd}";
        
        // 加载更新日志
        LoadReleaseNotes(updateInfo);
        
        // 加载变更列表
        foreach (var change in updateInfo.Changes)
        {
            var icon = change.Type switch
            {
                "feature" => "✨",
                "improvement" => "🔧",
                "fix" => "🐛"
            };
            
            ChangesList.Items.Add(new
            {
                Icon = icon,
                Title = change.Title,
                Description = change.Description
            });
        }
        
        // 紧急更新禁用跳过
        if (updateInfo.Urgent)
        {
            SkipButton.IsEnabled = false;
            SkipButton.ToolTip = "此更新为重要安全更新，必须安装";
        }
    }
    
    private async void LoadReleaseNotes(UpdateInfo updateInfo)
    {
        try
        {
            var notes = await _httpClient.GetStringAsync(updateInfo.ReleaseNotes);
            NotesWebBrowser.NavigateToString(notes);
        }
        catch
        {
            NotesWebBrowser.NavigateToString("<p>无法加载更新日志</p>");
        }
    }
    
    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
    
    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        // 设置稍后提醒
        _updateService.RemindLater(TimeSpan.FromHours(24));
        DialogResult = false;
        Close();
    }
    
    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        // 记录跳过版本
        _settingsService.SkipVersion(updateInfo.Version);
        DialogResult = false;
        Close();
    }
}
```

### 3.3 下载管理器

```csharp
public class UpdateDownloadManager
{
    private readonly HttpClient _httpClient;
    private CancellationTokenSource _cts;
    
    /// <summary>
    /// 下载更新
    /// </summary>
    public async Task<string> DownloadUpdateAsync(
        UpdateInfo updateInfo, 
        IProgress<DownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        var downloadPath = Path.Combine(
            Path.GetTempPath(), 
            "WordFlow_Updates",
            $"WordFlow_Setup_{updateInfo.Version}.exe");
        
        Directory.CreateDirectory(Path.GetDirectoryName(downloadPath));
        
        // 检查是否存在部分下载的文件
        var downloadedSize = 0L;
        if (File.Exists(downloadPath))
        {
            downloadedSize = new FileInfo(downloadPath).Length;
        }
        
        using var request = new HttpRequestMessage(HttpMethod.Get, updateInfo.DownloadUrl);
        
        if (downloadedSize > 0 && downloadedSize < updateInfo.FileSize)
        {
            request.Headers.Range = new RangeHeaderValue(downloadedSize, null);
        }
        
        using var response = await _httpClient.SendAsync(
            request, 
            HttpCompletionOption.ResponseHeadersRead, 
            _cts.Token);
        
        response.EnsureSuccessStatusCode();
        
        var totalSize = downloadedSize + response.Content.Headers.ContentLength.Value;
        
        using var fileStream = new FileStream(
            downloadPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.None,
            81920);
        
        using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
        
        var buffer = new byte[81920];
        var totalRead = downloadedSize;
        var sw = Stopwatch.StartNew();
        var speedSamples = new Queue<long>();
        
        while (true)
        {
            _cts.Token.ThrowIfCancellationRequested();
            
            var read = await stream.ReadAsync(
                buffer.AsMemory(0, buffer.Length), 
                _cts.Token);
            
            if (read == 0) break;
            
            await fileStream.WriteAsync(
                buffer.AsMemory(0, read), 
                _cts.Token);
            
            totalRead += read;
            
            // 计算速度
            speedSamples.Enqueue(read);
            if (speedSamples.Count > 10)
            {
                speedSamples.Dequeue();
            }
            
            var speed = speedSamples.Average() * 10; // 估算每秒字节数
            
            progress?.Report(new DownloadProgress
            {
                TotalSize = totalSize,
                DownloadedSize = totalRead,
                Percentage = (double)totalRead / totalSize * 100,
                Speed = FormatSpeed(speed),
                RemainingTime = TimeSpan.FromSeconds((totalSize - totalRead) / speed)
            });
        }
        
        sw.Stop();
        
        return downloadPath;
    }
    
    /// <summary>
    /// 取消下载
    /// </summary>
    public void CancelDownload()
    {
        _cts?.Cancel();
    }
    
    private string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond} B/s";
        if (bytesPerSecond < 1024 * 1024)
            return $"{bytesPerSecond / 1024.0:F1} KB/s";
        return $"{bytesPerSecond / (1024.0 * 1024):F1} MB/s";
    }
}
```

### 3.4 安装执行

```csharp
public class UpdateInstaller
{
    /// <summary>
    /// 执行安装
    /// </summary>
    public async Task InstallAsync(string installerPath, string version)
    {
        // 1. 验证签名
        var validator = new UpdatePackageValidator();
        if (!validator.VerifySignature(installerPath))
        {
            throw new SecurityException("安装包签名验证失败");
        }
        
        // 2. 准备安装参数
        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            // 静默安装参数
            Arguments = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=\"{AppDirectory}\"",
            UseShellExecute = true
        };
        
        // 3. 启动安装程序
        using var installer = new Process { StartInfo = startInfo };
        installer.Start();
        
        // 4. 等待安装完成
        await Task.Run(() =>
        {
            installer.WaitForExit();
        });
        
        if (installer.ExitCode != 0)
        {
            throw new Exception($"安装失败，退出码：{installer.ExitCode}");
        }
        
        // 5. 清理临时文件
        try
        {
            File.Delete(installerPath);
        }
        catch { }
    }
}
```

---

## 4. 版本兼容性检查

### 4.1 兼容性规则

```csharp
public class VersionCompatibilityChecker
{
    /// <summary>
    /// 检查系统兼容性
    /// </summary>
    public CompatibilityResult CheckCompatibility()
    {
        var result = new CompatibilityResult();
        
        // 检查 Windows 版本
        var osVersion = Environment.OSVersion.Version;
        if (osVersion.Major < 10)
        {
            result.IsCompatible = false;
            result.Reasons.Add("需要 Windows 10 或更高版本");
        }
        
        // 检查 .NET 版本
        var dotnetVersion = GetDotNetVersion();
        if (dotnetVersion.Major < 8)
        {
            result.IsCompatible = false;
            result.Reasons.Add("需要 .NET 8.0 或更高版本");
        }
        
        // 检查磁盘空间
        var freeSpace = GetFreeDiskSpace();
        if (freeSpace < 500 * 1024 * 1024) // 500MB
        {
            result.IsCompatible = false;
            result.Reasons.Add("磁盘空间不足，需要至少 500MB 可用空间");
        }
        
        // 检查内存
        var totalMemory = GetTotalMemory();
        if (totalMemory < 2 * 1024 * 1024 * 1024) // 2GB
        {
            result.IsCompatible = false;
            result.Reasons.Add("内存不足，需要至少 2GB 内存");
        }
        
        return result;
    }
    
    /// <summary>
    /// 检查版本升级路径
    /// </summary>
    public bool CanUpgrade(string currentVersion, string targetVersion)
    {
        var current = Version.Parse(currentVersion);
        var target = Version.Parse(targetVersion);
        
        // 不支持跨大版本直接升级
        if (target.Major - current.Major > 1)
        {
            return false; // 需要逐步升级
        }
        
        return true;
    }
}

public class CompatibilityResult
{
    public bool IsCompatible { get; set; }
    public List<string> Reasons { get; set; } = new();
}
```

---

## 5. 更新日志展示

### 5.1 更新日志格式

```markdown
# WordFlow v2.1.0 更新日志

## 🎉 新功能

### 快捷短语功能
- 支持语音触发快捷短语
- 支持自定义触发词和展开内容
- 支持快捷短语分类管理

### 热词管理
- 新增热词管理界面
- 支持批量导入导出

## 🔧 改进

### 性能优化
- 识别延迟降低 30%
- 启动速度提升 20%

### 用户体验
- 优化设置界面布局
- 改进错误提示

## 🐛 问题修复

- 修复了部分场景下识别失败的问题
- 修复了历史记录同步错误
- 修复了模型下载中断问题
```

### 5.2 更新日志 UI

```
┌─────────────────────────────────────────────────────────────┐
│  WordFlow 新版本可用                             [×]        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  📦 WordFlow v2.1.0                                         │
│  发布日期：2026-03-07                                       │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  更新内容                                            │   │
│  │                                                      │   │
│  │  ✨ 新功能                                           │   │
│  │    • 快捷短语功能                                    │   │
│  │    • 热词管理                                        │   │
│  │                                                      │   │
│  │  🔧 改进                                             │   │
│  │    • 识别延迟降低 30%                                 │   │
│  │    • 启动速度提升 20%                                 │   │
│  │                                                      │   │
│  │  🐛 问题修复                                         │   │
│  │    • 修复了识别失败问题                              │   │
│  │    • 修复了同步错误                                  │   │
│  │                                                      │   │
│  │  [查看完整更新日志 →]                                │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  下载进度：████████████████░░░░  80%                │   │
│  │               45.2MB / 56.5MB   12.3MB/s  剩 1 秒    │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│         [稍后提醒]    [跳过此版本]    [立即安装]            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 6. 实施计划

### 6.1 第一阶段：基础功能

- [ ] 更新检查服务
- [ ] 更新对话框
- [ ] 下载管理器

### 6.2 第二阶段：高级功能

- [ ] 增量更新支持
- [ ] 断点续传
- [ ] 后台下载

### 6.3 第三阶段：优化

- [ ] 签名验证
- [ ] 兼容性检查
- [ ] 更新日志展示

---

*本文档为 WordFlow 软件更新功能设计，将根据开发进度持续更新。*

*最后更新：2026-03-07*
