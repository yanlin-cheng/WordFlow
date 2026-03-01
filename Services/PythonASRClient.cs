using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WordFlow.Services
{
    /// <summary>
    /// Python ASR服务客户端
    /// 通过HTTP调用Python后端进行语音识别
    /// </summary>
    public class PythonASRClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _serviceUrl;
        private bool _isConnected;

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
                return _isConnected;
            }
            catch
            {
                _isConnected = false;
                return false;
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
