using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// 首次运行服务 - 检测并处理首次启动时的模型下载
    /// </summary>
    public class FirstRunService
    {
        private readonly ModelDownloadService _downloadService;
        private readonly string _modelsDir;
        private readonly string _firstRunMarkerPath;

        // Gitee Release 配置
        private const string GITEE_USER = "cheng-yanlin";
        private const string REPO = "WordFlow-Release";
        private const string VERSION = "v1.0.0";

        public FirstRunService()
        {
            _downloadService = new ModelDownloadService();
            _modelsDir = _downloadService.GetModelsDir();
            
            // 首次运行标记文件放在 Data 目录，而不是模型目录
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = Path.Combine(exeDir, "Data");
            Directory.CreateDirectory(dataDir); // 确保 Data 目录存在
            _firstRunMarkerPath = Path.Combine(dataDir, ".first_run_completed");
        }

        /// <summary>
        /// 检查是否需要首次运行设置
        /// </summary>
        public bool NeedsFirstRunSetup()
        {
            try
            {
                // 检查标记文件
                if (File.Exists(_firstRunMarkerPath))
                {
                    Logger.Log("FirstRunService: 已存在首次运行标记文件");
                    return false;
                }

                // 检查是否有有效模型
                if (!Directory.Exists(_modelsDir))
                {
                    Logger.Log("FirstRunService: 模型目录不存在，需要首次设置");
                    return true;
                }

                var modelDirs = Directory.GetDirectories(_modelsDir);
                var hasValidModel = modelDirs.Any(dir => IsValidModel(dir));

                Logger.Log($"FirstRunService: 模型目录数={modelDirs.Length}, 有效模型={hasValidModel}");

                return !hasValidModel;
            }
            catch (Exception ex)
            {
                Logger.Log($"FirstRunService: 检测失败 - {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// 检查目录是否包含有效的模型文件
        /// </summary>
        private bool IsValidModel(string modelDir)
        {
            var hasModel = File.Exists(Path.Combine(modelDir, "model.onnx")) ||
                          File.Exists(Path.Combine(modelDir, "model.int8.onnx"));
            var hasTokens = File.Exists(Path.Combine(modelDir, "tokens.txt"));

            return hasModel && hasTokens;
        }

        /// <summary>
        /// 获取默认模型配置
        /// </summary>
        public GiteeModelConfig GetDefaultModelConfig()
        {
            return new GiteeModelConfig
            {
                ModelId = "paraformer-zh",
                Name = "Paraformer-中文",
                Description = "中文语音识别，准确率高",
                PartFiles = new[]
                {
                    "paraformer-zh.tar.bz2.part1",
                    "paraformer-zh.tar.bz2.part2",
                    "paraformer-zh.tar.bz2.part3"
                },
                TotalSize = 200 * 1024 * 1024, // 约 200MB
                GiteeUser = GITEE_USER,
                Repo = REPO,
                Version = VERSION
            };
        }

        /// <summary>
        /// 下载默认模型
        /// </summary>
        public async Task<DownloadResult> DownloadDefaultModelAsync(
            IProgress<DownloadProgressEventArgs>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var config = GetDefaultModelConfig();

            // 订阅进度事件
            _downloadService.ProgressChanged += (s, e) =>
            {
                progress?.Report(e);
            };

            _downloadService.StatusChanged += (s, status) =>
            {
                Logger.Log($"FirstRunService: {status}");
            };

            var result = await _downloadService.DownloadModelFromGiteeAsync(
                config.ModelId,
                config.GiteeUser,
                config.Repo,
                config.Version,
                config.PartFiles,
                config.TotalSize,
                cancellationToken);

            if (result.Success)
            {
                // 创建首次运行完成标记
                try
                {
                    File.WriteAllText(_firstRunMarkerPath, DateTime.Now.ToString("O"));
                    Logger.Log("FirstRunService: 已创建首次运行标记文件");
                }
                catch (Exception ex)
                {
                    Logger.Log($"FirstRunService: 创建标记文件失败 - {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// 标记首次运行已完成（用于跳过下载的情况）
        /// </summary>
        public void MarkFirstRunCompleted()
        {
            try
            {
                Directory.CreateDirectory(_modelsDir);
                File.WriteAllText(_firstRunMarkerPath, DateTime.Now.ToString("O"));
                Logger.Log("FirstRunService: 已手动标记首次运行完成");
            }
            catch (Exception ex)
            {
                Logger.Log($"FirstRunService: 标记失败 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gitee 模型配置
    /// </summary>
    public class GiteeModelConfig
    {
        public string ModelId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string[] PartFiles { get; set; } = Array.Empty<string>();
        public long TotalSize { get; set; }
        public string GiteeUser { get; set; } = "";
        public string Repo { get; set; } = "";
        public string Version { get; set; } = "";
    }
}
