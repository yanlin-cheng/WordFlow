# WordFlow 语音模型管理设计文档

> 文档版本：1.0  
> 创建日期：2026-03-07  
> 状态：设计稿

---

## 目录

1. [支持的模型类型](#1-支持的模型类型)
2. [模型下载与更新](#2-模型下载与更新)
3. [模型切换机制](#3-模型切换机制)
4. [模型包格式规范](#4-模型包格式规范)
5. [模型源选择](#5-模型源选择)

---

## 1. 支持的模型类型

### 1.1 当前支持模型

| 模型名称 | 类型 | 语言 | 大小 | 说明 |
|---------|------|------|------|------|
| **SenseVoice Small** | ONNX | 多语言 | ~200MB | 通义实验室，支持中/英/日/韩/粤 |
| **Paraformer-zh** | ONNX | 中文 | ~200MB | 阿里达摩院，中文优化 |

### 1.2 计划支持模型

| 模型名称 | 类型 | 语言 | 说明 |
|---------|------|------|------|
| **SenseVoice Streaming** | ONNX | 多语言 | 流式识别版本 |
| **Whisper Tiny/Base** | ONNX | 多语言 | 开源多语言模型 |
| **定制领域模型** | ONNX | 中文 | 医疗/法律等专业领域 |

### 1.3 模型格式要求

所有模型必须转换为 **ONNX 格式**，原因：
- 跨平台兼容
- 推理性能好
- 无需 PyTorch/TensorFlow 依赖
- 支持量化（int8）

---

## 2. 模型下载与更新

### 2.1 模型源配置

```json
{
  "models": [
    {
      "id": "sensevoice-small",
      "name": "SenseVoice Small",
      "description": "通义实验室多语言模型，支持中文、英文、日文、韩文、粤语",
      "languages": ["zh", "en", "ja", "ko", "yue"],
      "size": "220MB",
      "format": "ONNX",
      "type": "offline",
      "sources": [
        {
          "name": "ModelScope",
          "url": "https://modelscope.cn/models/iic/SenseVoiceSmall/resolve/master/model.onnx",
          "priority": 1,
          "region": "cn"
        },
        {
          "name": "HuggingFace",
          "url": "https://huggingface.co/FunAudioLLM/SenseVoiceSmall/resolve/main/model.onnx",
          "priority": 2,
          "region": "global"
        }
      ],
      "files": [
        {
          "path": "model.onnx",
          "url": "model.onnx",
          "sha256": "abc123..."
        },
        {
          "path": "tokens.txt",
          "url": "tokens.txt",
          "sha256": "def456..."
        }
      ]
    },
    {
      "id": "paraformer-zh",
      "name": "Paraformer 中文",
      "description": "阿里达摩院中文语音识别模型",
      "languages": ["zh"],
      "size": "200MB",
      "format": "ONNX",
      "type": "offline",
      "sources": [
        {
          "name": "ModelScope",
          "url": "https://modelscope.cn/models/iic/paraformer-zh/resolve/master/",
          "priority": 1,
          "region": "cn"
        }
      ],
      "files": [
        {
          "path": "model.onnx",
          "url": "model.onnx",
          "sha256": "..."
        },
        {
          "path": "tokens.txt",
          "url": "tokens.txt",
          "sha256": "..."
        }
      ]
    }
  ]
}
```

### 2.2 下载管理器

```csharp
public class ModelDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly string _downloadPath;
    
    /// <summary>
    /// 下载模型
    /// </summary>
    public async Task DownloadModelAsync(
        ModelInfo model, 
        IProgress<DownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        // 1. 选择最佳下载源
        var source = SelectBestSource(model.Sources);
        
        // 2. 创建下载目录
        var modelDir = Path.Combine(_downloadPath, model.Id);
        Directory.CreateDirectory(modelDir);
        
        // 3. 下载每个文件
        foreach (var file in model.Files)
        {
            var filePath = Path.Combine(modelDir, file.Path);
            
            if (File.Exists(filePath))
            {
                // 检查是否需要重新下载
                if (await VerifyFileHashAsync(filePath, file.SHA256))
                {
                    continue; // 文件完整，跳过
                }
            }
            
            // 下载文件
            await DownloadFileAsync(
                source.Url + file.Url, 
                filePath, 
                progress, 
                cancellationToken);
        }
        
        // 4. 验证完整性
        await ValidateModelAsync(model, modelDir);
        
        // 5. 更新模型状态
        model.Status = ModelStatus.Installed;
        model.InstallPath = modelDir;
    }
    
    /// <summary>
    /// 选择最佳下载源
    /// </summary>
    private ModelSource SelectBestSource(IEnumerable<ModelSource> sources)
    {
        // 根据网络位置选择
        var isChina = await IsChinaIPAsync();
        
        return sources
            .Where(s => isChina ? s.Region == "cn" : s.Region == "global")
            .OrderBy(s => s.Priority)
            .FirstOrDefault() 
            ?? sources.First();
    }
    
    /// <summary>
    /// 断点续传下载
    /// </summary>
    private async Task DownloadFileAsync(
        string url, 
        string filePath, 
        IProgress<DownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        var downloadedSize = 0L;
        
        // 检查是否存在部分下载的文件
        if (File.Exists(filePath))
        {
            downloadedSize = new FileInfo(filePath).Length;
        }
        
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        if (downloadedSize > 0)
        {
            request.Headers.Range = new RangeHeaderValue(downloadedSize, null);
        }
        
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var totalSize = downloadedSize + response.Content.Headers.ContentLength.Value;
        
        using var fileStream = new FileStream(
            filePath, 
            FileMode.Append, 
            FileAccess.Write, 
            FileShare.None, 
            bufferSize: 81920);
        
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        
        var buffer = new byte[81920];
        var totalRead = downloadedSize;
        
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0) break;
            
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            
            totalRead += read;
            
            progress?.Report(new DownloadProgress
            {
                TotalSize = totalSize,
                DownloadedSize = totalRead,
                Percentage = (double)totalRead / totalSize * 100
            });
        }
    }
}

public class DownloadProgress
{
    public long TotalSize { get; set; }
    public long DownloadedSize { get; set; }
    public double Percentage { get; set; }
    public string Speed { get; set; } // 下载速度
    public TimeSpan RemainingTime { get; set; } // 剩余时间
}
```

### 2.3 模型验证

```csharp
public class ModelValidator
{
    /// <summary>
    /// 验证模型文件完整性
    /// </summary>
    public async Task<bool> ValidateModelAsync(ModelInfo model, string modelDir)
    {
        foreach (var file in model.Files)
        {
            var filePath = Path.Combine(modelDir, file.Path);
            
            if (!File.Exists(filePath))
            {
                return false;
            }
            
            // 验证 SHA256
            var hash = await ComputeSHA256Async(filePath);
            if (hash != file.SHA256.ToLower())
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// 验证模型可加载性
    /// </summary>
    public bool ValidateModelLoadable(string modelDir)
    {
        try
        {
            // 尝试加载模型
            var modelPath = Path.Combine(modelDir, "model.onnx");
            using var session = new OrtInferenceSession(modelPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<string> ComputeSHA256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
```

---

## 3. 模型切换机制

### 3.1 模型管理器

```csharp
public class ModelManager
{
    private readonly string _modelsPath;
    private ModelInfo _currentModel;
    private readonly Dictionary<string, ModelInfo> _installedModels;
    
    public ModelManager(string modelsPath)
    {
        _modelsPath = modelsPath;
        _installedModels = LoadInstalledModels();
    }
    
    /// <summary>
    /// 获取已安装的模型列表
    /// </summary>
    public List<ModelInfo> GetInstalledModels()
    {
        return _installedModels.Values.ToList();
    }
    
    /// <summary>
    /// 获取当前模型
    /// </summary>
    public ModelInfo GetCurrentModel()
    {
        return _currentModel;
    }
    
    /// <summary>
    /// 切换模型
    /// </summary>
    public async Task<bool> SwitchModelAsync(ModelInfo newModel)
    {
        // 1. 检查模型是否已安装
        if (!_installedModels.ContainsKey(newModel.Id))
        {
            // 提示下载
            await DownloadModelAsync(newModel);
        }
        
        // 2. 验证模型
        var modelDir = _installedModels[newModel.Id].InstallPath;
        if (!await _validator.ValidateModelAsync(newModel, modelDir))
        {
            // 模型损坏，重新下载
            await DownloadModelAsync(newModel);
        }
        
        // 3. 通知 ASR 服务切换模型
        var response = await _asrClient.LoadModelAsync(modelDir);
        
        if (response.Success)
        {
            _currentModel = newModel;
            SaveCurrentModel();
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 卸载模型
    /// </summary>
    public void UninstallModel(ModelInfo model)
    {
        if (_installedModels.ContainsKey(model.Id))
        {
            var modelDir = _installedModels[model.Id].InstallPath;
            
            // 不能卸载当前模型
            if (_currentModel?.Id == model.Id)
            {
                throw new InvalidOperationException("不能卸载当前正在使用的模型");
            }
            
            // 删除模型目录
            Directory.Delete(modelDir, true);
            
            // 更新状态
            _installedModels.Remove(model.Id);
            SaveInstalledModels();
        }
    }
    
    /// <summary>
    /// 加载已安装的模型
    /// </summary>
    private Dictionary<ModelInfo> LoadInstalledModels()
    {
        var models = new Dictionary<string, ModelInfo>();
        
        if (!Directory.Exists(_modelsPath))
        {
            return models;
        }
        
        foreach (var dir in Directory.GetDirectories(_modelsPath))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                var manifest = JsonSerializer.Deserialize<ModelManifest>(File.ReadAllText(manifestPath));
                var model = new ModelInfo
                {
                    Id = manifest.Id,
                    Name = manifest.Name,
                    InstallPath = dir,
                    Status = ModelStatus.Installed
                };
                models[manifest.Id] = model;
            }
        }
        
        return models;
    }
}
```

### 3.2 ASR 服务模型加载

```python
# Python ASR 服务
class ASRService:
    def __init__(self):
        self.current_model = None
        self.recognizer = None
    
    def load_model(self, model_path: str) -> bool:
        """加载模型"""
        try:
            # 停止当前服务
            if self.recognizer:
                self.recognizer = None
            
            # 加载新模型
            config = ModelConfig(
                model_dir=model_path,
                tokens_file=os.path.join(model_path, "tokens.txt")
            )
            
            self.recognizer = Recognizer(
                model_dir=config.model_dir,
                tokens_file=config.tokens_file
            )
            
            self.current_model = model_path
            return True
            
        except Exception as e:
            logger.error(f"加载模型失败：{e}")
            return False
    
    @app.route('/api/v1/load_model', methods=['POST'])
    def api_load_model():
        """API: 切换模型"""
        data = request.json
        model_path = data.get('model_path')
        
        success = asr_service.load_model(model_path)
        
        return jsonify({
            'success': success,
            'current_model': asr_service.current_model
        })
```

---

## 4. 模型包格式规范

### 4.1 目录结构

```
sensevoice-small/
├── manifest.json          # 模型元信息
├── model.onnx             # ONNX 模型文件
├── tokens.txt             # 词表文件
├── README.md              # 模型说明
└── config.yaml            # 模型配置（可选）
```

### 4.2 manifest.json 格式

```json
{
  "id": "sensevoice-small",
  "name": "SenseVoice Small",
  "version": "20240717",
  "description": "通义实验室多语言语音识别模型",
  "author": "Alibaba DAMO Academy",
  "languages": ["zh", "en", "ja", "ko", "yue"],
  "type": "offline",
  "format": "onnx",
  "size": 220000000,
  "sha256": "abc123...",
  "homepage": "https://modelscope.cn/models/iic/SenseVoiceSmall",
  "license": "Apache-2.0",
  "requirements": {
    "sherpa_onnx": ">=1.10.0"
  },
  "config": {
    "sample_rate": 16000,
    "num_features": 80,
    "embedding_dim": 512
  }
}
```

### 4.3 tokens.txt 格式

```
<blk>
<bos>
<eos>
<unk>
a
b
c
...
中
文
汉
字
...
```

---

## 5. 模型源选择

### 5.1 推荐模型源

| 源名称 | 地址 | 优势 | 适用地区 |
|-------|------|------|---------|
| **ModelScope** | modelscope.cn | 国内速度快，阿里官方 | 中国大陆 |
| **HuggingFace** | huggingface.co | 全球可用，模型丰富 | 海外 |
| **Gitee** | gitee.com | 国内镜像 | 中国大陆 |
| **阿里云 OSS** | oss.aliyuncs.com | CDN 加速 | 中国大陆 |

### 5.2 自动选择策略

```csharp
public class ModelSourceSelector
{
    public async Task<string> SelectBestSourceAsync(
        List<ModelSource> sources)
    {
        // 1. 检测网络位置
        var isChina = await IsChinaIPAsync();
        
        // 2. 过滤地区
        var candidates = sources
            .Where(s => isChina ? s.Region == "cn" : s.Region == "global")
            .ToList();
        
        if (!candidates.Any())
        {
            candidates = sources; // 无匹配，使用全部
        }
        
        // 3. 测速选择最快的
        var speeds = await Task.WhenAll(
            candidates.Select(async s => new
            {
                Source = s,
                Speed = await MeasureSpeedAsync(s.Url)
            })
        );
        
        return speeds
            .OrderByDescending(s => s.Speed)
            .First()
            .Source.Url;
    }
    
    private async Task<double> MeasureSpeedAsync(string baseUrl)
    {
        try
        {
            // 下载小文件测速
            var testUrl = baseUrl.TrimEnd('/') + "/tokens.txt";
            var sw = Stopwatch.StartNew();
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            var response = await client.GetAsync(testUrl);
            sw.Stop();
            
            if (response.IsSuccessStatusCode)
            {
                var size = response.Content.Headers.ContentLength ?? 0;
                return size / sw.Elapsed.TotalSeconds; // bytes/sec
            }
        }
        catch { }
        
        return 0;
    }
    
    private async Task<bool> IsChinaIPAsync()
    {
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync("https://api.ip.sb/geoip");
            
            if (response.IsSuccessStatusCode)
            {
                var geo = JsonSerializer.Deserialize<GeoIP>(await response.Content.ReadAsStringAsync());
                return geo?.Country == "CN";
            }
        }
        catch { }
        
        // 默认返回 true（假设国内用户多）
        return true;
    }
}

public class GeoIP
{
    public string Country { get; set; }
    public string Region { get; set; }
    public string City { get; set; }
}
```

---

## 6. UI 设计

### 6.1 模型管理窗口

```
┌─────────────────────────────────────────────────────────────┐
│  模型管理                                        [×]        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  当前模型：SenseVoice Small                    [切换模型]   │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  已安装模型                                          │   │
│  │  ┌─────────────────────────────────────────────┐   │   │
│  │  │ ✓ SenseVoice Small                   [卸载] │   │   │
│  │  │   多语言 (中/英/日/韩/粤) | 220MB            │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  │  ┌─────────────────────────────────────────────┐   │   │
│  │  │ ✓ Paraformer 中文                      [卸载] │   │   │
│  │  │   中文优化 | 200MB                           │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  可用模型                                  [刷新]    │   │
│  │  ┌─────────────────────────────────────────────┐   │   │
│  │  │ Whisper Base                        [下载]  │   │   │
│  │  │   多语言 | 150MB                             │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  [打开模型目录]                    [关闭]                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 7. 实施计划

### 7.1 第一阶段：基础功能

- [ ] 模型列表配置 (models.json)
- [ ] 下载管理器实现
- [ ] 模型验证机制

### 7.2 第二阶段：模型切换

- [ ] 模型管理器实现
- [ ] ASR 服务模型加载
- [ ] UI 界面实现

### 7.3 第三阶段：优化

- [ ] 断点续传
- [ ] 自动测速选源
- [ ] 后台下载

---

*本文档为 WordFlow 语音模型管理设计，将根据开发进度持续更新。*

*最后更新：2026-03-07*
