using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// 应用程序初始化服务 - 确保所有必要的文件和目录结构存在
    /// </summary>
    public class AppInitializer
    {
        private readonly string _appDir;

        public AppInitializer()
        {
            // 使用 AppContext.BaseDirectory 获取程序所在目录
            _appDir = AppContext.BaseDirectory;
        }

        /// <summary>
        /// 初始化应用程序环境
        /// </summary>
        public async Task InitializeAsync()
        {
            Logger.Log("AppInitializer: 开始初始化应用程序环境...");

            try
            {
                // 1. 确保 Data 目录存在
                EnsureDataDirectory();

                // 2. 确保 PythonASR 目录结构存在
                await EnsurePythonASRStructureAsync();

                // 3. 确保模型目录存在
                EnsureModelsDirectory();

                Logger.Log("AppInitializer: 应用程序环境初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Log($"AppInitializer: 初始化失败 - {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 确保 Data 目录存在
        /// </summary>
        private void EnsureDataDirectory()
        {
            var dataDir = Path.Combine(_appDir, "Data");
            Directory.CreateDirectory(dataDir);
            Logger.Log($"AppInitializer: Data 目录 - {dataDir}");

            // 创建默认的 models.json 配置文件
            var modelsJsonPath = Path.Combine(dataDir, "models.json");
            if (!File.Exists(modelsJsonPath))
            {
                var defaultConfig = @"{
  ""models"": [
    {
      ""id"": ""paraformer-zh"",
      ""name"": ""Paraformer-中文"",
      ""description"": ""中文语音识别模型，支持普通话"",
      ""size"": ""200MB"",
      ""language"": ""zh"",
      ""installed"": false
    }
  ]
}";
                File.WriteAllText(modelsJsonPath, defaultConfig);
                Logger.Log($"AppInitializer: 创建默认 models.json");
            }
        }

        /// <summary>
        /// 确保 PythonASR 目录结构存在
        /// </summary>
        private async Task EnsurePythonASRStructureAsync()
        {
            var pythonASRDir = Path.Combine(_appDir, "PythonASR");
            Directory.CreateDirectory(pythonASRDir);
            Logger.Log($"AppInitializer: PythonASR 目录 - {pythonASRDir}");

            // 确保 models 子目录存在
            var modelsDir = Path.Combine(pythonASRDir, "models");
            Directory.CreateDirectory(modelsDir);

            // 检查是否需要释放嵌入的 PythonASR 文件
            // 注意：在单文件发布模式下，我们需要从嵌入资源中提取文件
            await ExtractEmbeddedPythonASRAsync(pythonASRDir);
        }

        /// <summary>
        /// 从嵌入资源中提取 PythonASR 文件
        /// </summary>
        private async Task ExtractEmbeddedPythonASRAsync(string targetDir)
        {
            // 定义需要提取的关键文件
            var requiredFiles = new[]
            {
                "start_server.bat",
                "asr_server.py",
                "requirements.txt",
                "download_model.py",
                "setup_model.py",
                "quick_test.py",
                "test_asr.py"
            };

            // 检查关键文件是否已存在
            var missingFiles = false;
            foreach (var file in requiredFiles)
            {
                var filePath = Path.Combine(targetDir, file);
                if (!File.Exists(filePath))
                {
                    missingFiles = true;
                    Logger.Log($"AppInitializer: 缺少文件 - {file}");
                    
                    // 尝试从嵌入资源中提取
                    await ExtractResourceAsync($"PythonASR.{file}", filePath);
                }
            }

            if (!missingFiles)
            {
                Logger.Log("AppInitializer: PythonASR 文件已存在");
            }
        }

        /// <summary>
        /// 从嵌入资源中提取文件
        /// </summary>
        private async Task ExtractResourceAsync(string resourceName, string targetPath)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fullResourceName = $"WordFlow.{resourceName}";
                
                using var stream = assembly.GetManifestResourceStream(fullResourceName);
                if (stream != null)
                {
                    using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fileStream);
                    Logger.Log($"AppInitializer: 提取资源 - {resourceName} -> {targetPath}");
                }
                else
                {
                    Logger.Log($"AppInitializer: 未找到嵌入资源 - {fullResourceName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"AppInitializer: 提取资源失败 - {resourceName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保模型目录存在
        /// </summary>
        private void EnsureModelsDirectory()
        {
            var modelsDir = Path.Combine(_appDir, "PythonASR", "models");
            Directory.CreateDirectory(modelsDir);
            Logger.Log($"AppInitializer: 模型目录 - {modelsDir}");
        }

        /// <summary>
        /// 检查 PythonASR 环境是否完整
        /// </summary>
        public bool IsPythonASRReady()
        {
            var pythonASRDir = Path.Combine(_appDir, "PythonASR");
            var startServerBat = Path.Combine(pythonASRDir, "start_server.bat");
            var asrServerPy = Path.Combine(pythonASRDir, "asr_server.py");

            return Directory.Exists(pythonASRDir) &&
                   File.Exists(startServerBat) &&
                   File.Exists(asrServerPy);
        }

        /// <summary>
        /// 获取 PythonASR 目录路径
        /// </summary>
        public string GetPythonASRDirectory()
        {
            return Path.Combine(_appDir, "PythonASR");
        }
    }
}
