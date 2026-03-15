using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using WordFlow.Models.SpeechRecognition;

namespace WordFlow.Services
{
    /// <summary>
    /// 语音识别服务 - Sherpa-ONNX版本
    /// </summary>
    public class SpeechRecognitionService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly AudioRecorder _audioRecorder;
        private bool _isRecording;
        private string _serviceUrl;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<string>? RecognitionCompleted;
        public event EventHandler<bool>? RecordingStateChanged;
        public event EventHandler<bool>? ProcessingStateChanged;

        public bool IsRecording => _isRecording;
        public string CurrentModel { get; private set; } = "";

        public SpeechRecognitionService(string serviceUrl = "http://127.0.0.1:5000")
        {
            _serviceUrl = serviceUrl;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _audioRecorder = new AudioRecorder();
        }

        #region 服务连接

        /// <summary>
        /// 初始化服务并获取模型信息
        /// </summary>
        public async Task<(bool connected, List<ModelInfo> models, string currentModel)> InitializeAsync()
        {
            StatusChanged?.Invoke(this, "正在连接ASR服务...");
            
            try
            {
                // 检查服务健康状态
                var response = await _httpClient.GetAsync($"{_serviceUrl}/health");
                if (!response.IsSuccessStatusCode)
                {
                    return (false, new List<ModelInfo>(), "");
                }
                
                var healthData = await response.Content.ReadAsStringAsync();
                var health = JsonSerializer.Deserialize<HealthResponse>(healthData);
                CurrentModel = health?.current_model ?? "";
                
                // 获取可用模型列表
                var models = await GetAvailableModelsAsync();
                
                StatusChanged?.Invoke(this, $"已连接 - 当前模型: {CurrentModel}");
                return (true, models, CurrentModel);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"连接失败: {ex.Message}");
                return (false, new List<ModelInfo>(), "");
            }
        }

        /// <summary>
        /// 检查服务是否可用
        /// </summary>
        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serviceUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取健康检查信息（包含已安装模型和当前模型）
        /// </summary>
        public async Task<HealthResponse?> GetHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serviceUrl}/health");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<HealthResponse>(json);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 模型管理

        /// <summary>
        /// 获取可用模型列表
        /// </summary>
        public async Task<List<ModelInfo>> GetAvailableModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serviceUrl}/models");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<ModelsResponse>(json);
                
                return data?.models?.Select(m => new ModelInfo
                {
                    Id = m.Key,
                    Name = m.Value.name,
                    Size = m.Value.size,
                    Description = m.Value.description,
                    Installed = m.Value.installed
                }).ToList() ?? new List<ModelInfo>();
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"获取模型列表失败: {ex.Message}");
                return new List<ModelInfo>();
            }
        }

        /// <summary>
        /// 切换模型
        /// </summary>
        public async Task<bool> SwitchModelAsync(string modelId)
        {
            try
            {
                StatusChanged?.Invoke(this, $"正在切换模型: {modelId}...");
                
                var content = new StringContent(
                    JsonSerializer.Serialize(new { model_id = modelId }),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                
                var response = await _httpClient.PostAsync($"{_serviceUrl}/load_model", content);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<LoadModelResponse>(json);
                
                if (result?.success == true)
                {
                    CurrentModel = modelId;
                    StatusChanged?.Invoke(this, $"已切换到: {modelId}");
                    return true;
                }
                else
                {
                    var errorMsg = result?.error ?? "请检查模型是否正确安装";
                    StatusChanged?.Invoke(this, $"切换模型失败: {errorMsg}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"切换模型失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 语音识别

        /// <summary>
        /// 开始录音
        /// </summary>
        public void StartRecording()
        {
            Utils.Logger.Log("SpeechService: StartRecording 被调用");
            if (_isRecording)
            {
                Utils.Logger.Log("SpeechService: 已经在录音中，跳过");
                return;
            }

            try
            {
                Utils.Logger.Log("SpeechService: 调用 _audioRecorder.StartRecording");
                _audioRecorder.StartRecording();
                _isRecording = true;
                Utils.Logger.Log("SpeechService: 录音已启动");
                RecordingStateChanged?.Invoke(this, true);
                StatusChanged?.Invoke(this, "正在录音...");
            }
            catch (Exception ex)
            {
                Utils.Logger.Log($"SpeechService: 录音启动失败 - {ex.Message}");
                StatusChanged?.Invoke(this, $"录音启动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止录音并识别
        /// </summary>
        public async Task StopRecordingAndRecognizeAsync(float gain = 3.0f)
        {
            if (!_isRecording) return;

            try
            {
                ProcessingStateChanged?.Invoke(this, true);
                StatusChanged?.Invoke(this, "正在识别...");
                
                // 停止录音，应用增益
                var wavData = _audioRecorder.StopRecording(gain);
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);

                // 调用ASR服务
                var result = await RecognizeAsync(wavData);
                
                RecognitionCompleted?.Invoke(this, result);
                StatusChanged?.Invoke(this, "识别完成");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"识别失败: {ex.Message}");
            }
            finally
            {
                ProcessingStateChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// 从音频文件识别
        /// </summary>
        public async Task RecognizeFromFileAsync(string filePath)
        {
            try
            {
                byte[] wavData = await File.ReadAllBytesAsync(filePath);
                
                if (wavData.Length < 4 || 
                    wavData[0] != 'R' || wavData[1] != 'I' || 
                    wavData[2] != 'F' || wavData[3] != 'F')
                {
                    StatusChanged?.Invoke(this, "错误: 只支持WAV格式音频文件");
                    return;
                }

                ProcessingStateChanged?.Invoke(this, true);
                StatusChanged?.Invoke(this, "正在识别...");

                var result = await RecognizeAsync(wavData);
                
                RecognitionCompleted?.Invoke(this, result);
                StatusChanged?.Invoke(this, "识别完成");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"识别失败: {ex.Message}");
            }
            finally
            {
                ProcessingStateChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// 识别音频数据
        /// </summary>
        private async Task<string> RecognizeAsync(byte[] wavData)
        {
            var request = new
            {
                audio = Convert.ToBase64String(wavData),
                sample_rate = 16000
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync($"{_serviceUrl}/recognize", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RecognizeResponse>(json);

            return result?.text ?? "";
        }

        #endregion

        #region 自动启动服务

        /// <summary>
        /// 尝试自动启动 ASR 服务
        /// </summary>
        public async Task<bool> TryStartServerAsync()
        {
            try
            {
                // 获取 PythonASR 目录路径
                // 使用 AppContext.BaseDirectory 替代 Assembly.Location（单文件模式下 Location 返回空）
                var exeDir = AppContext.BaseDirectory;
                
                Utils.Logger.Log($"TryStartServerAsync: exeDir={exeDir}");
                
                // 尝试多个可能的路径
                var possiblePaths = new[]
                {
                    // 直接检查当前目录（打包后的场景）
                    Path.Combine(exeDir, "PythonASR"),
                    // 向上查找 1-5 层（调试模式或不同部署结构）
                    Path.Combine(exeDir, "..", "PythonASR"),
                    Path.Combine(exeDir, "..", "..", "PythonASR"),
                    Path.Combine(exeDir, "..", "..", "..", "PythonASR"),
                    Path.Combine(exeDir, "..", "..", "..", "..", "PythonASR"),
                    Path.Combine(exeDir, "..", "..", "..", "..", "..", "PythonASR"),
                };
                
                string? serverDir = null;
                foreach (var path in possiblePaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    Utils.Logger.Log($"TryStartServerAsync: 检查路径={fullPath}, 存在={Directory.Exists(fullPath)}");
                    if (Directory.Exists(fullPath))
                    {
                        // 额外检查：确认目录中包含 asr_server.py 或 start_server.bat
                        if (File.Exists(Path.Combine(fullPath, "asr_server.py")) || 
                            File.Exists(Path.Combine(fullPath, "start_server.bat")))
                        {
                            serverDir = fullPath;
                            Utils.Logger.Log($"TryStartServerAsync: 找到 PythonASR 目录={serverDir}");
                            break;
                        }
                        else
                        {
                            Utils.Logger.Log($"TryStartServerAsync: 路径 {fullPath} 存在但不是有效的 PythonASR 目录");
                        }
                    }
                }
                
                if (serverDir == null)
                {
                    // 尝试使用进程位置作为备选方案
                    try
                    {
                        var processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(processPath))
                        {
                            var processDir = Path.GetDirectoryName(processPath);
                            Utils.Logger.Log($"TryStartServerAsync: 进程路径={processDir}");
                            
                            var fallbackPaths = new[]
                            {
                                Path.Combine(processDir, "PythonASR"),
                                Path.Combine(processDir, "..", "PythonASR"),
                                Path.Combine(processDir, "..", "..", "PythonASR"),
                            };
                            
                            foreach (var path in fallbackPaths)
                            {
                                var fullPath = Path.GetFullPath(path);
                                Utils.Logger.Log($"TryStartServerAsync: 检查备选路径={fullPath}, 存在={Directory.Exists(fullPath)}");
                                if (Directory.Exists(fullPath))
                                {
                                    if (File.Exists(Path.Combine(fullPath, "asr_server.py")) ||
                                        File.Exists(Path.Combine(fullPath, "start_server.bat")))
                                    {
                                        serverDir = fullPath;
                                        Utils.Logger.Log($"TryStartServerAsync: 通过备选路径找到 PythonASR 目录={serverDir}");
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Log($"TryStartServerAsync: 获取进程路径失败：{ex.Message}");
                    }
                }
                
                if (serverDir == null)
                {
                    string errorMsg = "找不到 PythonASR 目录";
                    Utils.Logger.Log($"TryStartServerAsync: {errorMsg}");
                    StatusChanged?.Invoke(this, errorMsg);
                    return false;
                }
                
                Utils.Logger.Log($"TryStartServerAsync: 找到 PythonASR 目录={serverDir}");
                StatusChanged?.Invoke(this, $"正在启动 ASR 服务...");

                // 优先使用 start_server.bat（因为它设置了正确的环境变量和工作目录）
                var batchFile = Path.Combine(serverDir, "start_server.bat");
                if (File.Exists(batchFile))
                {
                    Utils.Logger.Log($"TryStartServerAsync: 使用批处理文件启动：{batchFile}");
                    
                    // 启动批处理文件（隐藏窗口）
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c \"{batchFile}\"",
                            WorkingDirectory = serverDir,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };

                    process.Start();
                    
                    // 记录启动日志
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!process.HasExited)
                            {
                                var output = await process.StandardOutput.ReadLineAsync();
                                if (!string.IsNullOrEmpty(output))
                                {
                                    Utils.Logger.Log($"[ASR] {output}");
                                }
                            }
                        }
                        catch { }
                    });

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!process.HasExited)
                            {
                                var error = await process.StandardError.ReadLineAsync();
                                if (!string.IsNullOrEmpty(error))
                                {
                                    Utils.Logger.Log($"[ASR-ERR] {error}");
                                }
                            }
                        }
                        catch { }
                    });
                }
                else
                {
                    // 备用：直接启动 Python 脚本
                    var pythonScript = Path.Combine(serverDir, "asr_server.py");
                    if (!File.Exists(pythonScript))
                    {
                        StatusChanged?.Invoke(this, $"找不到 asr_server.py: {pythonScript}");
                        return false;
                    }

                    // 使用嵌入的 Python 解释器
                    var pythonExe = Path.Combine(serverDir, "python", "python.exe");
                    if (!File.Exists(pythonExe))
                    {
                        StatusChanged?.Invoke(this, $"找不到嵌入的 Python: {pythonExe}");
                        return false;
                    }

                    Utils.Logger.Log($"TryStartServerAsync: 使用 Python 脚本启动：{pythonExe} {pythonScript}");
                    StatusChanged?.Invoke(this, "正在启动 ASR 服务...");

                    // 启动 Python 脚本（隐藏窗口）
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = pythonExe,
                            Arguments = $"-u \"{pythonScript}\" --port 5000",  // -u 禁用输出缓冲
                            WorkingDirectory = serverDir,
                            UseShellExecute = false,  // 不使用 Shell，以便隐藏窗口
                            CreateNoWindow = true,    // 不创建窗口
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                            RedirectStandardOutput = true,  // 重定向输出以便日志记录
                            RedirectStandardError = true
                        }
                    };

                    process.Start();
                    
                    // 记录启动日志
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!process.HasExited)
                            {
                                var output = await process.StandardOutput.ReadLineAsync();
                                if (!string.IsNullOrEmpty(output))
                                {
                                    Utils.Logger.Log($"[ASR] {output}");
                                }
                            }
                        }
                        catch { }
                    });

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!process.HasExited)
                        {
                                var error = await process.StandardError.ReadLineAsync();
                                if (!string.IsNullOrEmpty(error))
                                {
                                    Utils.Logger.Log($"[ASR-ERR] {error}");
                                }
                            }
                        }
                        catch { }
                    });
                }
                
                // 等待服务器启动（最多等 15 秒）
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);
                    
                    if (await CheckConnectionAsync())
                    {
                        StatusChanged?.Invoke(this, "ASR 服务已启动");
                        return true;
                    }
                }

                StatusChanged?.Invoke(this, "服务启动超时，请检查手动启动");
                return false;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"启动服务失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 自动加载第一个已安装的模型
        /// </summary>
        public async Task<bool> AutoLoadModelAsync()
        {
            try
            {
                var models = await GetAvailableModelsAsync();
                var installedModel = models.FirstOrDefault(m => m.Installed);
                
                if (installedModel != null)
                {
                    StatusChanged?.Invoke(this, $"正在自动加载模型: {installedModel.Name}...");
                    return await SwitchModelAsync(installedModel.Id);
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        public void Dispose()
        {
            try
            {
                _audioRecorder?.Dispose();
            }
            catch { }
            
            try
            {
                _httpClient?.Dispose();
            }
            catch { }
        }

        #region 数据模型

        /// <summary>
        /// 模型信息
        /// </summary>
        public class ModelInfo
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Size { get; set; } = "";
            public string Description { get; set; } = "";
            public bool Installed { get; set; }
        }

        /// <summary>
        /// 修正建议（用于错误检测）
        /// </summary>
        public class CorrectionSuggestion
        {
            public string WrongWord { get; set; } = "";
            public string SuggestedCorrectWord { get; set; } = "";
            public string Context { get; set; } = "";
            public double Confidence { get; set; }
            public bool IsNewVocabulary { get; set; }
        }

        public class HealthResponse
        {
            public string status { get; set; } = "";
            public string current_model { get; set; } = "";
            public List<string> installed_models { get; set; } = new();
        }

        private class ModelsResponse
        {
            public Dictionary<string, ModelDetail> models { get; set; } = new();
        }

        private class ModelDetail
        {
            public string name { get; set; } = "";
            public string size { get; set; } = "";
            public string description { get; set; } = "";
            public bool installed { get; set; }
        }

        private class LoadModelResponse
        {
            public bool success { get; set; }
            public string? error { get; set; }
            public string? model_id { get; set; }
        }

        private class RecognizeResponse
        {
            public string text { get; set; } = "";
            public bool success { get; set; }
        }

        #endregion
    }
}
