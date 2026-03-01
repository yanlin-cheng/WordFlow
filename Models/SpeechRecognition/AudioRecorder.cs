using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using WordFlow.Utils;

namespace WordFlow.Models.SpeechRecognition
{
    /// <summary>
    /// 音频录制器
    /// </summary>
    public class AudioRecorder : IDisposable
    {
        private WaveInEvent? _waveIn;
        private MemoryStream? _memoryStream;
        private WaveFileWriter? _waveWriter;
        private bool _isRecording;
        private string? _debugFilePath;

        public bool IsRecording => _isRecording;
        public event EventHandler<byte[]>? DataAvailable;
        public event EventHandler? RecordingStopped;

        /// <summary>
        /// 获取可用的录音设备列表
        /// </summary>
        public static string[] GetRecordingDevices()
        {
            var devices = new System.Collections.Generic.List<string>();
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                devices.Add($"{i}: {caps.ProductName}");
            }
            return devices.ToArray();
        }

        /// <summary>
        /// 开始录音
        /// </summary>
        /// <param name="deviceNumber">录音设备编号，默认自动选择有声音的耳机设备</param>
        public void StartRecording(int deviceNumber = -1)
        {
            if (_isRecording) return;

            // 打印可用设备供调试
            Debug.WriteLine("可用录音设备:");
            var devices = GetRecordingDevices();
            foreach (var dev in devices)
            {
                Debug.WriteLine($"  {dev}");
            }

            // 自动选择：如果 deviceNumber < 0，尝试找到耳机设备
            if (deviceNumber < 0)
            {
                deviceNumber = 0; // 默认第一个
                for (int i = 0; i < devices.Length; i++)
                {
                    var devName = devices[i].ToLower();
                    // 优先选择耳机设备（包含 headphone/耳机/headset 关键字）
                    if (devName.Contains("耳机") || devName.Contains("headphone") || 
                        devName.Contains("headset") || devName.Contains("edifier"))
                    {
                        deviceNumber = i;
                        Debug.WriteLine($"自动选择耳机设备: {devices[i]}");
                        break;
                    }
                }
            }

            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber, // 指定设备
                WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16bit, 单声道
                BufferMilliseconds = 100 // 100ms buffer
            };

            _memoryStream = new MemoryStream();
            _waveWriter = new WaveFileWriter(_memoryStream, _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            // 创建调试文件 - 保存到WordFlow/Recordings目录
            _debugFilePath = AppPaths.GetRecordingFilePath();
            Debug.WriteLine($"录音文件: {_debugFilePath}");

            _waveIn.StartRecording();
            _isRecording = true;

            var caps = WaveIn.GetCapabilities(deviceNumber);
            Debug.WriteLine($"开始使用设备 {deviceNumber}: {caps.ProductName}");
        }

        /// <summary>
        /// 停止录音
        /// </summary>
        public byte[] StopRecording(float gain = 1.0f)
        {
            if (!_isRecording) return Array.Empty<byte>();

            _waveIn?.StopRecording();
            _isRecording = false;

            _waveWriter?.Flush();
            
            byte[] result = _memoryStream?.ToArray() ?? Array.Empty<byte>();
            
            // 应用数字增益
            if (gain != 1.0f && result.Length > 44)
            {
                result = ApplyGain(result, gain);
            }
            
            // 保存调试文件
            if (_debugFilePath != null && result.Length > 0)
            {
                try
                {
                    File.WriteAllBytes(_debugFilePath, result);
                    Debug.WriteLine($"录音已保存到: {_debugFilePath}");
                    
                    // 检查音频电平
                    var avgLevel = CalculateAverageLevel(result);
                    Debug.WriteLine($"音频平均电平: {avgLevel:P0} (建议 > 10%)");
                    if (avgLevel < 0.05)
                    {
                        Debug.WriteLine("警告: 录音音量太低，请检查麦克风设置！");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"保存调试文件失败: {ex.Message}");
                }
            }
            
            Cleanup();
            
            return result;
        }

        /// <summary>
        /// 应用数字增益到音频数据
        /// </summary>
        private byte[] ApplyGain(byte[] wavData, float gain)
        {
            if (wavData.Length <= 44) return wavData;
            
            // 复制数据（保留WAV头部）
            byte[] result = new byte[wavData.Length];
            Buffer.BlockCopy(wavData, 0, result, 0, wavData.Length);
            
            // 只处理音频数据部分
            for (int i = 44; i < result.Length - 1; i += 2)
            {
                short sample = BitConverter.ToInt16(result, i);
                
                // 使用double防止中间计算溢出
                double amplified = (double)sample * gain;
                
                // 限制范围防止爆音（使用Math.Clamp更安全）
                amplified = Math.Clamp(amplified, -32768.0, 32767.0);
                
                // 转换为short
                short newSample = (short)Math.Round(amplified);
                
                // 写回字节数组
                result[i] = (byte)(newSample & 0xFF);
                result[i + 1] = (byte)((newSample >> 8) & 0xFF);
            }
            
            Debug.WriteLine($"应用增益: {gain:F1}x");
            return result;
        }

        /// <summary>
        /// 计算音频平均电平
        /// </summary>
        private float CalculateAverageLevel(byte[] wavData)
        {
            try
            {
                // 直接从WAV字节数据解析PCM样本
                // 标准WAV头部44字节
                const int wavHeaderSize = 44;
                
                if (wavData.Length <= wavHeaderSize) return 0;
                
                // 确保是RIFF/WAVE格式
                if (wavData[0] != 'R' || wavData[1] != 'I' || 
                    wavData[2] != 'F' || wavData[3] != 'F')
                {
                    return 0;
                }
                
                long sum = 0;
                int count = 0;
                
                // 从头部之后读取16-bit PCM样本
                for (int i = wavHeaderSize; i < wavData.Length - 1; i += 2)
                {
                    short sample = BitConverter.ToInt16(wavData, i);
                    sum += Math.Abs(sample);
                    count++;
                }
                
                if (count == 0) return 0;
                return (sum / count) / 32768f;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取录音音频数据（float数组）
        /// </summary>
        public float[] GetAudioData(byte[] wavData)
        {
            using var stream = new MemoryStream(wavData);
            using var reader = new WaveFileReader(stream);
            
            var sampleCount = (int)(reader.Length / sizeof(short));
            var samples = new float[sampleCount];
            
            var buffer = new byte[reader.Length];
            reader.Read(buffer, 0, buffer.Length);
            
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(buffer, i * 2);
                samples[i] = sample / 32768f; // 转换为 -1.0 到 1.0
            }
            
            return samples;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            _waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            _waveWriter?.Flush();
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"录音错误: {e.Exception.Message}");
            }
            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }

        private void Cleanup()
        {
            _waveWriter?.Dispose();
            _waveWriter = null;
            
            _memoryStream?.Dispose();
            _memoryStream = null;
            
            _waveIn?.Dispose();
            _waveIn = null;
        }

        public void Dispose()
        {
            StopRecording();
            Cleanup();
        }
    }
}
