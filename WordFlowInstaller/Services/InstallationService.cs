using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using WordFlowInstaller.Models;

namespace WordFlowInstaller.Services
{
    /// <summary>
    /// 安装服务 - 协调整个安装流程
    /// 包含：.NET 环境检测与安装、主程序解压、模型下载与解压
    /// </summary>
    public class InstallationService
    {
        private readonly InstallConfig config;
        private readonly HttpClient httpClient;
        
        public event EventHandler<InstallProgressEventArgs>? ProgressChanged;
        public event EventHandler<string>? StatusChanged;

        public InstallationService(InstallConfig config)
        {
            this.config = config;
            httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        }

        /// <summary>
        /// 执行简化安装流程
        /// 注意：模型下载移至主程序首次启动时进行
        /// </summary>
        public async Task<bool> InstallAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. 创建安装目录
                StatusChanged?.Invoke(this, "正在准备安装...");
                ProgressChanged?.Invoke(this, new InstallProgressEventArgs
                {
                    CurrentStep = 1,
                    TotalSteps = 3,
                    ProgressPercentage = 10,
                    Status = "正在创建安装目录..."
                });

                if (!Directory.Exists(config.InstallPath))
                {
                    Directory.CreateDirectory(config.InstallPath);
                }

                ProgressChanged?.Invoke(this, new InstallProgressEventArgs
                {
                    CurrentStep = 1,
                    TotalSteps = 3,
                    ProgressPercentage = 20,
                    Status = "安装目录创建完成"
                });

                // 2. 从嵌入资源解压主程序
                StatusChanged?.Invoke(this, "正在安装主程序...");
                ProgressChanged?.Invoke(this, new InstallProgressEventArgs
                {
                    CurrentStep = 2,
                    TotalSteps = 3,
                    ProgressPercentage = 30,
                    Status = "正在解压 WordFlow 主程序..."
                });

                await ExtractMainProgramAsync(cancellationToken);

                ProgressChanged?.Invoke(this, new InstallProgressEventArgs
                {
                    CurrentStep = 2,
                    TotalSteps = 3,
                    ProgressPercentage = 70,
                    Status = "主程序安装完成"
                });

                // 3. 创建快捷方式和设置
                StatusChanged?.Invoke(this, "正在完成安装...");
                ProgressChanged?.Invoke(this, new InstallProgressEventArgs
                {
                    CurrentStep = 3,
                    TotalSteps = 3,
                    ProgressPercentage = 80,
                    Status = "正在创建快捷方式..."
                });

                if (config.CreateDesktopShortcut)
                {
                    CreateDesktopShortcut();
                }

                // 设置开机自启动
                if (config.AutoStart)
                {
                    SetAutoStart(true);
                }

                // 完成
                ProgressChanged?.Invoke(this, new InstallProgressEventArgs
                {
                    CurrentStep = 3,
                    TotalSteps = 3,
                    ProgressPercentage = 100,
                    Status = "安装完成！"
                });

                config.InstallationCompleted = true;
                return true;
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke(this, "安装已取消");
                return false;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"安装失败：{ex.Message}");
                throw;
            }
        }

        #region 主程序解压

        /// <summary>
        /// 从嵌入资源解压主程序
        /// </summary>
        private async Task ExtractMainProgramAsync(CancellationToken cancellationToken)
        {
            try
            {
                // 从嵌入资源读取 WordFlow.zip
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "WordFlowInstaller.Resources.WordFlow.zip";
                
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new Exception("找不到嵌入资源：WordFlow.zip");
                    }

                    // 先保存到临时文件
                    var tempZipPath = Path.Combine(Path.GetTempPath(), $"WordFlow_{Guid.NewGuid()}.zip");
                    
                    using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write))
                    {
                        await stream.CopyToAsync(fileStream);
                    }

                    // 解压到安装目录
                    ZipFile.ExtractToDirectory(tempZipPath, config.InstallPath);
                    
                    // 清理临时文件
                    File.Delete(tempZipPath);
                }

                ProgressChanged?.Invoke(this, new InstallProgressEventArgs
                {
                    CurrentStep = 3,
                    TotalSteps = 5,
                    ProgressPercentage = 45,
                    Status = "主程序解压完成"
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"解压主程序失败：{ex.Message}");
            }
        }

        #endregion

        #region 模型下载与安装（已移至主程序）
        /*
         * 注意：模型下载功能已移至主程序的 ModelDownloadDialog
         * 首次启动时会自动检测并提示用户下载模型
         * 保留此区域注释以便了解历史实现
         */

        #endregion

        #region 辅助方法

        /// <summary>
        /// 创建桌面快捷方式
        /// </summary>
        private void CreateDesktopShortcut()
        {
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var shortcutPath = Path.Combine(desktopPath, "WordFlow.lnk");

                var wshType = Type.GetTypeFromProgID("WScript.Shell");
                if (wshType != null)
                {
                    dynamic wsh = Activator.CreateInstance(wshType);
                    var shortcut = wsh.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = Path.Combine(config.InstallPath, "WordFlow.exe");
                    shortcut.WorkingDirectory = config.InstallPath;
                    shortcut.Description = "WordFlow 语音输入工具";
                    shortcut.Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建快捷方式失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 设置开机自启动
        /// </summary>
        private void SetAutoStart(bool enable)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            var exePath = Path.Combine(config.InstallPath, "WordFlow.exe");
                            key.SetValue("WordFlow", $"\"{exePath}\"");
                        }
                        else
                        {
                            key.DeleteValue("WordFlow", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置开机自启动失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 取消安装
        /// </summary>
        public void CancelInstallation()
        {
            try
            {
                if (Directory.Exists(config.InstallPath))
                {
                    var filesToDelete = new[]
                    {
                        "WordFlow.zip",
                        "*.tar.bz2",
                        "model_parts"
                    };

                    foreach (var filePattern in filesToDelete)
                    {
                        try
                        {
                            var files = Directory.GetFiles(config.InstallPath, filePattern);
                            foreach (var file in files)
                            {
                                File.Delete(file);
                            }
                            
                            var dirs = Directory.GetDirectories(config.InstallPath, filePattern);
                            foreach (var dir in dirs)
                            {
                                Directory.Delete(dir, true);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        #endregion
    }

    /// <summary>
    /// 安装进度事件参数
    /// </summary>
    public class InstallProgressEventArgs : EventArgs
    {
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public int ProgressPercentage { get; set; }
        public string Status { get; set; } = "";
        public double SpeedKBps { get; set; }
        public TimeSpan RemainingTime { get; set; }
    }
}
