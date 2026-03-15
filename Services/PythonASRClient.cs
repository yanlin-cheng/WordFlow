using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// Python ASR 服务客户端
    /// 通过 HTTP 调用 Python 后端进行语音识别
    /// </summary>
    public class PythonASRClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _serviceUrl;
        private bool _isConnected;
        private bool _isServiceStarting;
        private readonly SemaphoreSlim _startSemaphore = new SemaphoreSlim(1, 1);

        public event EventHandler<string>? StatusChanged;

        public PythonASRClient(string serviceUrl = "http://127.0.0.1:5000")
        {
            _serviceUrl = serviceUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        /// <summary>
        /// 检查服务是否可用
        /// </summary>
        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_serviceUrl);
                _isConnected = response.IsSuccessStatusCode;
                if (_isConnected)
                {
                    Logger.Info("PythonASR 服务连接成功");
                }
                return _isConnected;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Logger.Debug($"PythonASR 服务连接失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查服务连接，如果失败则尝试启动服务
        /// </summary>
        public async Task<bool> CheckConnectionWithAutoStartAsync()
        {
            // 首先尝试连接
            if (await CheckConnectionAsync())
            {
                return true;
            }

            // 服务未连接，尝试启动
            Logger.Info("PythonASR 服务未响应，尝试启动服务...");
            StatusChanged?.Invoke(this, "正在启动语音识别服务...");
            
            var started = await StartServiceAsync();
            if (started)
            {
                // 等待服务启动
                StatusChanged?.Invoke(this, "等待服务启动...");
                for (int i = 0; i < 30; i++) // 最多等待 30 秒
                {
                    await Task.Delay(1000);
                    if (await CheckConnectionAsync())
                    {
                        Logger.Info("PythonASR 服务启动成功");
                        return true;
                    }
                }
                Logger.Warning("PythonASR 服务启动超时");
                StatusChanged?.Invoke(this, "服务启动超时，请检查配置");
            }
            else
            {
                Logger.Warning("无法启动 PythonASR 服务");
                StatusChanged?.Invoke(this, "无法启动语音识别服务");
            }
            
            return false;
        }

        /// <summary>
        /// 尝试启动 Python ASR 服务
        /// </summary>
        private async Task<bool> StartServiceAsync()
        {
            await _startSemaphore.WaitAsync();
            try
            {
                if (_isServiceStarting)
                {
                    Logger.Debug("服务已在启动中，跳过");
                    return false;
                }

                _isServiceStarting = true;
                Logger.Info("开始启动 Python ASR 服务");

                // 获取应用程序目录
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // 尝试多个可能的路径来找到 Python ASR 服务
                var possiblePaths = new[]
                {
                    // 安装后的路径
                    Path.Combine(exeDir, "PythonASR", "start_server.bat"),
                    Path.Combine(exeDir, "PythonASR", "start_server.py"),
                    // 开发环境路径
                    Path.Combine(exeDir, "..", "..", "..", "PythonASR", "start_server.bat"),
                    Path.Combine(exeDir, "..", "..", "..", "PythonASR", "start_server.py"),
                    // 发布目录
                    Path.Combine(exeDir, "..", "PythonASR", "start_server.bat"),
                    Path.Combine(exeDir, "..", "PythonASR", "start_server.py"),
                };

                string? batchPath = null;
                string? pythonPath = null;

                foreach (var path in possiblePaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    Logger.Debug($"检查路径：{fullPath}");
                    
                    if (File.Exists(fullPath))
                    {
                        if (fullPath.EndsWith(".bat"))
                        {
                            batchPath = fullPath;
                            Logger.Info($"找到批处理文件：{batchPath}");
                        }
                        else if (fullPath.EndsWith(".py"))
                        {
                            pythonPath = fullPath;
                            Logger.Info($"找到 Python 脚本：{pythonPath}");
                        }
                    }
                }

                if (batchPath != null)
                {
                    // 使用批处理文件启动
                    Logger.Info($"使用批处理文件启动服务：{batchPath}");
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{batchPath}\"",
                        WorkingDirectory = Path.GetDirectoryName(batchPath),
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(processInfo);
                    Logger.Info("Python ASR 服务进程已启动（批处理）");
                    return true;
                }
                else if (pythonPath != null)
                {
                    // 尝试使用内置 Python 启动
                    var pythonExePath = Path.Combine(exeDir, "PythonASR", "python", "python.exe");
                    if (!File.Exists(pythonExePath))
                    {
                        pythonExePath = "python"; // 使用系统 Python
                    }

                    Logger.Info($"使用 Python 启动服务：{pythonExePath} {pythonPath}");
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = pythonExePath,
                        Arguments = $"\"{pythonPath}\"",
                        WorkingDirectory = Path.GetDirectoryName(pythonPath),
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(processInfo);
                    Logger.Info("Python ASR 服务进程已启动（Python）");
                    return true;
                }
                else
                {
                    Logger.Error("未找到 Python ASR 服务启动文件");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"启动 Python ASR 服务失败：{ex.Message}", ex);
                return false;
            }
            finally
            {
                _isServiceStarting = false;
                _startSemaphore.Release();
            }
        }

        /// <summary>
        /// 语音识别
        /// </summary>
        public async Task<string> RecognizeAsync(byte[] wavData, int sampleRate = 16000)
        {
            if (!_isConnected)
            {
                // 尝试连接
                var connected = await CheckConnectionAsync();
                if (!connected)
                {
                    throw new InvalidOperationException("无法连接到ASR服务，请确保Python服务已启动");
                }
            }

            StatusChanged?.Invoke(this, "正在发送音频到ASR服务...");

            // 构建请求
            var request = new
            {
                audio = Convert.ToBase64String(wavData),
                sample_rate = sampleRate
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 发送请求
            var response = await _httpClient.PostAsync($"{_serviceUrl}/recognize", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"ASR服务错误: {error}");
            }

            // 解析响应
            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ASRResponse>(responseJson);

            if (result?.success != true)
            {
                throw new InvalidOperationException($"识别失败: {result?.error ?? "未知错误"}");
            }

            return result.text ?? string.Empty;
        }

        /// <summary>
        /// 将float数组转换为WAV格式字节
        /// </summary>
        public byte[] ConvertToWavBytes(float[] audioData, int sampleRate = 16000)
        {
            // 转换为16-bit PCM
            var pcmData = new short[audioData.Length];
            for (int i = 0; i < audioData.Length; i++)
            {
                pcmData[i] = (short)(audioData[i] * 32767);
            }

            // 写入WAV文件格式
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // WAV头部
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcmData.Length * 2);  // 文件大小
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);  // fmt块大小
            writer.Write((short)1);  // 音频格式 (PCM)
            writer.Write((short)1);  // 声道数
            writer.Write(sampleRate);  // 采样率
            writer.Write(sampleRate * 2);  // 字节率
            writer.Write((short)2);  // 块对齐
            writer.Write((short)16);  // 采样位数
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcmData.Length * 2);  // 数据块大小

            // 写入PCM数据
            foreach (var sample in pcmData)
            {
                writer.Write(sample);
            }

            return ms.ToArray();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private class ASRResponse
        {
            public bool success { get; set; }
            public string? text { get; set; }
            public string? error { get; set; }
        }
    }
}
