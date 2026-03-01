using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WordFlow.Models.SpeechRecognition
{
    /// <summary>
    /// 模型管理器 - 负责模型的生命周期管理
    /// 空闲5分钟后自动卸载，节省资源
    /// </summary>
    public class ModelManager : IDisposable
    {
        private ParaformerModel? _model;
        private readonly string _modelDir;
        private DateTime _lastAccessTime;
        private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _warningTime = TimeSpan.FromMinutes(3);
        private Timer? _idleTimer;
        private bool _isDisposed;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler? ModelLoaded;
        public event EventHandler? ModelUnloaded;
        public event EventHandler<TimeSpan>? IdleWarning; // 即将卸载警告

        public bool IsModelLoaded => _model?.IsLoaded ?? false;
        public TimeSpan IdleTime => DateTime.Now - _lastAccessTime;

        public ModelManager(string modelDir)
        {
            _modelDir = modelDir;
            // 每分钟检查一次空闲状态
            _idleTimer = new Timer(CheckIdleState, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// 确保模型已加载
        /// </summary>
        public async Task EnsureModelLoadedAsync()
        {
            if (_model == null)
            {
                _model = new ParaformerModel(_modelDir);
            }

            if (!_model.IsLoaded)
            {
                StatusChanged?.Invoke(this, "正在加载语音模型...");
                await Task.Run(() => _model.Load());
                ModelLoaded?.Invoke(this, EventArgs.Empty);
                StatusChanged?.Invoke(this, "语音模型已就绪");
            }

            _lastAccessTime = DateTime.Now;
        }

        /// <summary>
        /// 获取模型实例（会自动加载）
        /// </summary>
        public async Task<ParaformerModel> GetModelAsync()
        {
            await EnsureModelLoadedAsync();
            return _model!;
        }

        /// <summary>
        /// 主动卸载模型
        /// </summary>
        public void UnloadModel()
        {
            if (_model?.IsLoaded == true)
            {
                StatusChanged?.Invoke(this, "正在释放语音模型...");
                _model.Unload();
                ModelUnloaded?.Invoke(this, EventArgs.Empty);
                StatusChanged?.Invoke(this, "语音模型已释放");
            }
        }

        /// <summary>
        /// 语音识别（自动管理模型生命周期）
        /// </summary>
        public async Task<string> RecognizeAsync(float[] audioData)
        {
            try
            {
                await EnsureModelLoadedAsync();
                var result = await Task.Run(() => _model!.Recognize(audioData));
                _lastAccessTime = DateTime.Now;
                return result;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"识别失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检查空闲状态
        /// </summary>
        private void CheckIdleState(object? state)
        {
            if (_isDisposed || !_model?.IsLoaded == true) return;

            var idleTime = DateTime.Now - _lastAccessTime;

            // 3分钟警告
            if (idleTime >= _warningTime && idleTime < _idleTimeout)
            {
                var remaining = _idleTimeout - idleTime;
                IdleWarning?.Invoke(this, remaining);
                StatusChanged?.Invoke(this, $"模型将在 {remaining.TotalSeconds:0} 秒后释放");
            }
            // 5分钟卸载
            else if (idleTime >= _idleTimeout)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    UnloadModel();
                });
            }
        }

        /// <summary>
        /// 保持模型活跃（重置空闲计时器）
        /// </summary>
        public void KeepAlive()
        {
            _lastAccessTime = DateTime.Now;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            _idleTimer?.Dispose();
            _model?.Dispose();
            _idleTimer = null;
            _model = null;
        }
    }
}
