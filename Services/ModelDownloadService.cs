using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// 模型下载服务 - 负责从远程下载并安装语音识别模型
    /// </summary>
    public class ModelDownloadService
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelsDir;
        private readonly string _configPath;
        private ModelsConfig? _config;

        public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
        public event EventHandler<string>? StatusChanged;

        public ModelDownloadService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            
            // 获取应用程序基础目录
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // 尝试多个可能的路径来找到模型目录
            var possibleModelPaths = new[]
            {
                // 标准发布结构
                Path.Combine(exeDir, "PythonASR", "models"),
                // 单文件发布解压后的结构
                Path.Combine(exeDir, "..", "PythonASR", "models"),
                // 开发环境结构
                Path.Combine(exeDir, "..", "..", "..", "PythonASR", "models"),
            };
            
            _modelsDir = possibleModelPaths.FirstOrDefault(Directory.Exists) ?? possibleModelPaths[0];
            _modelsDir = Path.GetFullPath(_modelsDir);
            
            // 确保模型目录存在
            try
            {
                Directory.CreateDirectory(_modelsDir);
                Logger.Log($"ModelDownloadService: 确保模型目录存在: {_modelsDir}");
            }
            catch (Exception ex)
            {
                Logger.Log($"ModelDownloadService: 创建模型目录失败 - {ex.Message}");
            }
            
            // 同样处理配置文件路径
            var possibleConfigPaths = new[]
            {
                Path.Combine(exeDir, "Data", "models.json"),
                Path.Combine(exeDir, "..", "Data", "models.json"),
                Path.Combine(exeDir, "..", "..", "..", "Data", "models.json"),
            };
            
            _configPath = possibleConfigPaths.FirstOrDefault(File.Exists) ?? possibleConfigPaths[0];
            _configPath = Path.GetFullPath(_configPath);
            
            Logger.Log($"ModelDownloadService: 模型目录 = {_modelsDir}");
            Logger.Log($"ModelDownloadService: 配置文件 = {_configPath}");
        }

        /// <summary>
        /// 获取模型目录路径（公开访问）
        /// </summary>
        public string GetModelsDir() => _modelsDir;

        #region 首次设置检测

        /// <summary>
        /// 检查是否需要首次运行设置（没有任何已安装的模型）
        /// </summary>
        public async Task<bool> NeedsFirstRunSetupAsync()
        {
            try
            {
                // 确保目录存在
                Directory.CreateDirectory(_modelsDir);
                
                // 检查是否有任何模型目录
                var modelDirs = Directory.GetDirectories(_modelsDir);
                var hasValidModel = modelDirs.Any(dir => IsValidModel(dir));
                
                Logger.Log($"首次设置检测：模型目录数={modelDirs.Length}, 有效模型={hasValidModel}");
                
                // 如果没有有效模型，检查是否有已下载的压缩包
                if (!hasValidModel)
                {
                    var pendingArchives = CheckPendingArchives();
                    if (pendingArchives.Count > 0)
                    {
                        Logger.Log($"发现 {pendingArchives.Count} 个待解压的模型压缩包");
                        // 自动解压已下载的模型
                        foreach (var archive in pendingArchives)
                        {
                            await ExtractDownloadedArchiveAsync(archive);
                        }
                        // 重新检查
                        modelDirs = Directory.GetDirectories(_modelsDir);
                        hasValidModel = modelDirs.Any(dir => IsValidModel(dir));
                    }
                }
                
                Logger.Log($"首次设置检测完成：需要设置={hasValidModel}");
                return !hasValidModel;
            }
            catch (Exception ex)
            {
                Logger.Log($"首次设置检测失败：{ex.Message}");
                return true;
            }
        }
        
        /// <summary>
        /// 检查是否有已下载但未解压的模型压缩包
        /// </summary>
        private List<string> CheckPendingArchives()
        {
            var archives = new List<string>();
            try
            {
                // 检查 .tar.bz2 文件
                var files = Directory.GetFiles(_modelsDir, "*.tar.bz2");
                archives.AddRange(files);
                Logger.Log($"找到 {files.Length} 个压缩包");
                
                // 同时检查 .tar 文件（可能是其他格式）
                var tarFiles = Directory.GetFiles(_modelsDir, "*.tar");
                foreach (var tarFile in tarFiles)
                {
                    if (!archives.Contains(tarFile))
                    {
                        archives.Add(tarFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"检查压缩包失败：{ex.Message}");
            }
            return archives;
        }
        
        /// <summary>
        /// 解压已下载的模型压缩包
        /// </summary>
        private async Task ExtractDownloadedArchiveAsync(string archivePath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(archivePath);
                // 去掉 .tar 后缀（因为文件名是 model-id.tar.bz2）
                if (fileName.EndsWith(".tar"))
                {
                    fileName = fileName.Substring(0, fileName.Length - 4);
                }
                
                Logger.Log($"正在解压：{archivePath}");
                StatusChanged?.Invoke(this, $"正在解压 {fileName}...");
                
                var result = await ExtractModelAsync(archivePath, fileName);
                
                if (result.Success)
                {
                    // 解压成功后删除压缩包
                    try
                    {
                        File.Delete(archivePath);
                        Logger.Log($"解压完成并已删除压缩包：{archivePath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"删除压缩包失败：{ex.Message}");
                    }
                }
                else
                {
                    Logger.Log($"解压失败：{result.Error}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"解压异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 检查目录是否包含有效的模型文件
        /// </summary>
        private bool IsValidModel(string modelDir)
        {
            // 检查是否存在 model.onnx 或 model.int8.onnx
            var hasModel = File.Exists(Path.Combine(modelDir, "model.onnx")) ||
                          File.Exists(Path.Combine(modelDir, "model.int8.onnx"));
            
            // 检查是否存在 tokens.txt
            var hasTokens = File.Exists(Path.Combine(modelDir, "tokens.txt"));
            
            return hasModel && hasTokens;
        }

        #endregion

        #region 模型配置

        /// <summary>
        /// 获取可用模型列表
        /// </summary>
        public async Task<List<ModelInfo>> GetAvailableModelsAsync()
        {
            try
            {
                if (_config == null)
                {
                    await LoadConfigAsync();
                }
                
                return _config?.Models ?? new List<ModelInfo>();
            }
            catch (Exception ex)
            {
                Logger.Log($"获取模型列表失败: {ex.Message}");
                return new List<ModelInfo>();
            }
        }

        /// <summary>
        /// 加载模型配置文件
        /// </summary>
        private async Task LoadConfigAsync()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    Logger.Log($"配置文件不存在: {_configPath}");
                    _config = new ModelsConfig { Models = new List<ModelInfo>() };
                    return;
                }
                
                var json = await File.ReadAllTextAsync(_configPath);
                _config = JsonSerializer.Deserialize<ModelsConfig>(json);
                Logger.Log($"已加载 {_config?.Models?.Count ?? 0} 个模型配置");
            }
            catch (Exception ex)
            {
                Logger.Log($"加载模型配置失败: {ex.Message}");
                _config = new ModelsConfig { Models = new List<ModelInfo>() };
            }
        }

        /// <summary>
        /// 获取默认模型
        /// </summary>
        public async Task<ModelInfo?> GetDefaultModelAsync()
        {
            var models = await GetAvailableModelsAsync();
            return models.FirstOrDefault(m => m.Default) ?? models.FirstOrDefault();
        }

        #endregion

        #region 下载模型

        /// <summary>
        /// 从 Gitee Release 下载模型（支持分包）
        /// </summary>
        public async Task<DownloadResult> DownloadModelFromGiteeAsync(
            string modelId,
            string giteeUser,
            string repo,
            string version,
            string[] partFiles,
            long totalSize,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.Log($"开始从 Gitee 下载模型: {modelId}");
                StatusChanged?.Invoke(this, $"准备下载 {modelId}...");
                
                // 确保模型目录存在
                Directory.CreateDirectory(_modelsDir);
                
                // 下载所有分包
                var partPaths = new List<string>();
                for (int i = 0; i < partFiles.Length; i++)
                {
                    var partFile = partFiles[i];
                    var partUrl = $"https://gitee.com/{giteeUser}/{repo}/releases/download/{version}/{partFile}";
                    var partPath = Path.Combine(_modelsDir, partFile);
                    
                    Logger.Log($"下载分包 {i + 1}/{partFiles.Length}: {partFile}");
                    StatusChanged?.Invoke(this, $"正在下载模型 ({i + 1}/{partFiles.Length})...");
                    
                    var downloadResult = await DownloadFileWithResumeAsync(
                        partUrl, 
                        partPath, 
                        totalSize / partFiles.Length,  // 估算每个分包大小
                        cancellationToken,
                        (progress) =>
                        {
                            // 计算整体进度
                            var overallProgress = ((i + progress / 100.0) / partFiles.Length) * 100;
                            ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                            {
                                BytesReceived = (long)(totalSize * overallProgress / 100),
                                TotalBytes = totalSize,
                                ProgressPercentage = overallProgress
                            });
                        });
                    
                    if (!downloadResult)
                    {
                        return new DownloadResult { Success = false, Error = $"分包 {partFile} 下载失败" };
                    }
                    
                    partPaths.Add(partPath);
                }
                
                // 合并分包
                StatusChanged?.Invoke(this, "正在合并模型文件...");
                var mergedPath = Path.Combine(_modelsDir, $"{modelId}.tar.bz2");
                await MergePartsAsync(partPaths, mergedPath);
                
                // 删除分包文件
                foreach (var partPath in partPaths)
                {
                    try { File.Delete(partPath); } catch { }
                }
                
                // 解压
                StatusChanged?.Invoke(this, "正在解压模型...");
                var extractResult = await ExtractModelAsync(mergedPath, modelId);
                
                // 删除合并后的文件
                try { File.Delete(mergedPath); } catch { }
                
                if (!extractResult.Success)
                {
                    return new DownloadResult { Success = false, Error = extractResult.Error ?? "解压失败" };
                }
                
                StatusChanged?.Invoke(this, "模型安装完成！");
                Logger.Log($"模型安装成功: {modelId}");
                
                return new DownloadResult { Success = true, ModelPath = Path.Combine(_modelsDir, modelId) };
            }
            catch (OperationCanceledException)
            {
                Logger.Log("下载被取消");
                return new DownloadResult { Success = false, Error = "用户取消下载" };
            }
            catch (Exception ex)
            {
                Logger.Log($"下载模型失败: {ex.Message}");
                return new DownloadResult { Success = false, Error = ex.Message };
            }
        }
        
        /// <summary>
        /// 合并分包文件
        /// </summary>
        private async Task MergePartsAsync(List<string> partPaths, string outputPath)
        {
            using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                foreach (var partPath in partPaths.OrderBy(p => p))
                {
                    using (var inputStream = new FileStream(partPath, FileMode.Open, FileAccess.Read))
                    {
                        await inputStream.CopyToAsync(outputStream);
                    }
                }
            }
        }

        /// <summary>
        /// 下载并安装模型（标准方式）
        /// </summary>
        public async Task<DownloadResult> DownloadModelAsync(
            ModelInfo model, 
            bool useMirror = true, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.Log($"开始下载模型: {model.Id} ({model.Name})");
                StatusChanged?.Invoke(this, $"准备下载 {model.Name}...");
                
                // 确保模型目录存在
                Directory.CreateDirectory(_modelsDir);
                
                // 构建下载 URL
                var baseUrl = useMirror && !string.IsNullOrEmpty(_config?.MirrorUrl) 
                    ? _config.MirrorUrl 
                    : _config?.BaseUrl ?? "";
                
                var archiveUrl = $"{baseUrl}/{model.Files.Archive}";
                var archivePath = Path.Combine(_modelsDir, model.Files.Archive);
                
                Logger.Log($"下载地址: {archiveUrl}");
                Logger.Log($"保存路径: {archivePath}");
                
                // 下载
                StatusChanged?.Invoke(this, "正在下载...");
                var downloadResult = await DownloadFileWithResumeAsync(
                    archiveUrl, 
                    archivePath, 
                    model.SizeBytes,
                    cancellationToken);
                
                if (!downloadResult)
                {
                    return new DownloadResult { Success = false, Error = "下载失败或被取消" };
                }
                
                // 解压
                StatusChanged?.Invoke(this, "正在解压...");
                var extractResult = await ExtractModelAsync(archivePath, model.Id);
                
                if (!extractResult.Success)
                {
                    return new DownloadResult { Success = false, Error = extractResult.Error ?? "解压失败" };
                }
                
                // 验证
                StatusChanged?.Invoke(this, "正在验证模型...");
                var modelPath = Path.Combine(_modelsDir, model.Id);
                if (!ValidateModelIntegrity(modelPath, model))
                {
                    return new DownloadResult { Success = false, Error = "模型文件验证失败" };
                }
                
                // 删除压缩包
                try
                {
                    File.Delete(archivePath);
                }
                catch { /* 忽略删除失败 */ }
                
                StatusChanged?.Invoke(this, "模型安装完成！");
                Logger.Log($"模型安装成功: {model.Id}");
                
                return new DownloadResult { Success = true, ModelPath = modelPath };
            }
            catch (OperationCanceledException)
            {
                Logger.Log("下载被取消");
                return new DownloadResult { Success = false, Error = "用户取消下载" };
            }
            catch (Exception ex)
            {
                Logger.Log($"下载模型失败: {ex.Message}");
                return new DownloadResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// 带断点续传的文件下载
        /// </summary>
        private async Task<bool> DownloadFileWithResumeAsync(
            string url, 
            string filePath, 
            long expectedSize,
            CancellationToken cancellationToken,
            Action<double>? progressCallback = null)
        {
            try
            {
                // 检查已下载的部分
                long existingLength = 0;
                if (File.Exists(filePath))
                {
                    existingLength = new FileInfo(filePath).Length;
                    Logger.Log($"发现已下载部分: {existingLength} bytes");
                }
                
                // 检查是否已完成
                if (existingLength >= expectedSize && expectedSize > 0)
                {
                    Logger.Log("文件已完整下载");
                    return true;
                }
                
                // 设置 Range 头（断点续传）
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (existingLength > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
                }
                
                using var response = await _httpClient.SendAsync(
                    request, 
                    HttpCompletionOption.ResponseHeadersRead, 
                    cancellationToken);
                
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? (expectedSize - existingLength);
                var totalToDownload = existingLength + totalBytes;
                
                Logger.Log($"下载: 已有 {existingLength}, 需下载 {totalBytes}, 总计 {totalToDownload}");
                
                // 打开文件流（追加模式）
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(
                    filePath, 
                    existingLength > 0 ? FileMode.Append : FileMode.Create, 
                    FileAccess.Write, 
                    FileShare.None);
                
                var buffer = new byte[8192];
                var totalRead = existingLength;
                var lastReport = DateTime.Now;
                var readSinceLastReport = 0L;
                
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;
                    readSinceLastReport += bytesRead;
                    
                    // 每 200ms 报告一次进度
                    var now = DateTime.Now;
                    if ((now - lastReport).TotalMilliseconds >= 200)
                    {
                        var speed = readSinceLastReport / (now - lastReport).TotalSeconds;
                        var progress = (double)totalRead / totalToDownload * 100;
                        var remainingBytes = totalToDownload - totalRead;
                        var remainingTime = speed > 0 ? TimeSpan.FromSeconds(remainingBytes / speed) : TimeSpan.Zero;
                        
                        // 调用回调（如果提供）
                        progressCallback?.Invoke(progress);
                        
                        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                        {
                            BytesReceived = totalRead,
                            TotalBytes = totalToDownload,
                            ProgressPercentage = progress,
                            Speed = speed,
                            RemainingTime = remainingTime
                        });
                        
                        lastReport = now;
                        readSinceLastReport = 0;
                    }
                }
                
                // 最终进度报告
                ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                {
                    BytesReceived = totalToDownload,
                    TotalBytes = totalToDownload,
                    ProgressPercentage = 100,
                    Speed = 0,
                    RemainingTime = TimeSpan.Zero
                });
                
                Logger.Log($"下载完成: {totalRead} bytes");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log($"下载失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 解压模型

        /// <summary>
        /// 解压模型（使用 SharpZipLib，不依赖 Python）
        /// </summary>
        private async Task<(bool Success, string? Error)> ExtractModelAsync(string archivePath, string modelId)
        {
            try
            {
                var modelsDir = _modelsDir;
                Logger.Log($"开始解压: {archivePath}");
                Logger.Log($"目标目录: {modelsDir}");
                
                // 使用 SharpZipLib 解压 tar.bz2
                await Task.Run(() =>
                {
                    // 创建 BZip2 输入流
                    using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
                    using (var bzip2Stream = new ICSharpCode.SharpZipLib.BZip2.BZip2InputStream(fs))
                    using (var tarArchive = ICSharpCode.SharpZipLib.Tar.TarArchive.CreateInputTarArchive(bzip2Stream, System.Text.Encoding.UTF8))
                    {
                        tarArchive.ExtractContents(modelsDir);
                    }
                });
                
                Logger.Log("解压完成，检查目录结构...");
                
                // 查找解压后的目录并重命名
                var targetDir = Path.Combine(modelsDir, modelId);
                
                // 查找解压出来的目录（通常是 sherpa-onnx-paraformer-zh-... 这样的名字）
                var allDirs = Directory.GetDirectories(modelsDir);
                var extractedDir = allDirs.FirstOrDefault(d => 
                {
                    var name = Path.GetFileName(d);
                    // 排除目标目录本身和标记文件目录
                    if (name == modelId) return false;
                    if (File.Exists(Path.Combine(d, ".first_run_completed"))) return false;
                    // 匹配包含模型关键字的目录
                    return name.Contains("paraformer") || 
                           name.Contains("sherpa") || 
                           name.Contains(modelId.Replace("-", "_"));
                });
                
                if (extractedDir != null)
                {
                    Logger.Log($"找到解压目录: {extractedDir}");
                    
                    // 如果目标目录已存在，先删除
                    if (Directory.Exists(targetDir))
                    {
                        Logger.Log($"删除已存在的目标目录: {targetDir}");
                        Directory.Delete(targetDir, true);
                    }
                    
                    // 确保源目录和目标目录不同
                    if (!extractedDir.Equals(targetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.Move(extractedDir, targetDir);
                        Logger.Log($"重命名: {extractedDir} -> {targetDir}");
                    }
                    else
                    {
                        Logger.Log($"目录已经是目标名称，无需重命名");
                    }
                }
                else
                {
                    Logger.Log($"未找到需要重命名的目录，检查是否已存在目标目录: {targetDir}");
                    // 如果目标目录已存在，说明解压成功
                    if (!Directory.Exists(targetDir))
                    {
                        // 尝试查找任何新创建的目录
                        var newDir = allDirs.FirstOrDefault(d => 
                        {
                            var name = Path.GetFileName(d);
                            return name != modelId && 
                                   !File.Exists(Path.Combine(d, ".first_run_completed")) &&
                                   Directory.GetFiles(d, "*.onnx").Length > 0; // 包含模型文件
                        });
                        
                        if (newDir != null)
                        {
                            Directory.Move(newDir, targetDir);
                            Logger.Log($"重命名: {newDir} -> {targetDir}");
                        }
                    }
                }
                
                Logger.Log("解压流程完成");
                return (true, null);
            }
            catch (Exception ex)
            {
                Logger.Log($"解压异常: {ex.Message}");
                Logger.Log($"堆栈跟踪: {ex.StackTrace}");
                return (false, $"解压失败: {ex.Message}");
            }
        }

        #endregion

        #region 验证模型

        /// <summary>
        /// 验证模型完整性
        /// </summary>
        private bool ValidateModelIntegrity(string modelPath, ModelInfo model)
        {
            try
            {
                if (!Directory.Exists(modelPath))
                {
                    Logger.Log($"模型目录不存在: {modelPath}");
                    return false;
                }
                
                // 检查必需文件
                foreach (var requiredFile in model.RequiredFiles)
                {
                    // 特殊处理 model.onnx（可能是 model.int8.onnx）
                    if (requiredFile == "model.onnx")
                    {
                        var hasModel = File.Exists(Path.Combine(modelPath, "model.onnx")) ||
                                      File.Exists(Path.Combine(modelPath, "model.int8.onnx"));
                        if (!hasModel)
                        {
                            Logger.Log($"缺少模型文件: {requiredFile}");
                            return false;
                        }
                    }
                    else
                    {
                        var filePath = Path.Combine(modelPath, requiredFile);
                        if (!File.Exists(filePath))
                        {
                            Logger.Log($"缺少必需文件: {requiredFile}");
                            return false;
                        }
                    }
                }
                
                Logger.Log($"模型验证通过: {modelPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"验证失败: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 模型配置
    /// </summary>
    public class ModelsConfig
    {
        public string Version { get; set; } = "";
        public string Description { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string? MirrorUrl { get; set; }
        public List<ModelInfo> Models { get; set; } = new();
    }

    /// <summary>
    /// 模型信息
    /// </summary>
    public class ModelInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Size { get; set; } = "";
        public long SizeBytes { get; set; }
        public string Description { get; set; } = "";
        public ModelFiles Files { get; set; } = new();
        public List<string> RequiredFiles { get; set; } = new();
        public bool Default { get; set; }
    }

    /// <summary>
    /// 模型文件信息
    /// </summary>
    public class ModelFiles
    {
        public string Archive { get; set; } = "";
    }

    /// <summary>
    /// 下载进度事件参数
    /// </summary>
    public class DownloadProgressEventArgs : EventArgs
    {
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public double ProgressPercentage { get; set; }
        public double Speed { get; set; } // bytes/sec
        public TimeSpan RemainingTime { get; set; }
    }

    /// <summary>
    /// 下载结果
    /// </summary>
    public class DownloadResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? ModelPath { get; set; }
    }

    #endregion
}
