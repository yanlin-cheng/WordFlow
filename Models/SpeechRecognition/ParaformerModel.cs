using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WordFlow.Models.SpeechRecognition
{
    /// <summary>
    /// Paraformer ONNX 语音识别模型
    /// </summary>
    public class ParaformerModel : IDisposable
    {
        private InferenceSession? _session;
        private readonly string _modelPath;
        private readonly string _tokensPath;
        private readonly string _amMvnPath;
        private List<string>? _tokens;
        private float[]? _amMvnMeans;
        private float[]? _amMvnVars;
        private bool _isLoaded;

        public bool IsLoaded => _isLoaded;
        public string ModelPath => _modelPath;

        public ParaformerModel(string modelDir)
        {
            _modelPath = Path.Combine(modelDir, "model_quant.onnx");
            _tokensPath = Path.Combine(modelDir, "tokens.txt");
            _amMvnPath = Path.Combine(modelDir, "am.mvn");
            
            // 如果量化版不存在，使用完整版
            if (!File.Exists(_modelPath))
            {
                _modelPath = Path.Combine(modelDir, "model.onnx");
            }
        }

        /// <summary>
        /// 加载模型
        /// </summary>
        public void Load()
        {
            if (_isLoaded) return;

            try
            {
                // 加载ONNX模型
                var options = new SessionOptions
                {
                    InterOpNumThreads = 4,
                    IntraOpNumThreads = 4,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };
                
                _session = new InferenceSession(_modelPath, options);
                
                // 加载词汇表
                LoadTokens();
                
                // 加载特征归一化参数
                LoadAmMvn();
                
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载模型失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 卸载模型释放内存
        /// </summary>
        public void Unload()
        {
            _session?.Dispose();
            _session = null;
            _isLoaded = false;
        }

        /// <summary>
        /// 语音识别
        /// </summary>
        public string Recognize(float[] audioData, int sampleRate = 16000)
        {
            if (!_isLoaded || _session == null)
                throw new InvalidOperationException("模型未加载");

            Utils.Logger.Log($"[Recognize] 开始识别，音频长度: {audioData.Length}, 采样率: {sampleRate}");

            if (sampleRate != 16000)
            {
                audioData = Resample(audioData, sampleRate, 16000);
                Utils.Logger.Log($"[Recognize] 重采样后长度: {audioData.Length}");
            }

            // 提取FBank特征
            var features = ExtractFeatures(audioData);
            Utils.Logger.Log($"[Recognize] 特征维度: [{features.GetLength(0)}, {features.GetLength(1)}]");
            
            // 归一化
            NormalizeFeatures(features);
            
            // 将二维数组转换为一维并创建张量
            int numFrames = features.GetLength(0);
            int numFeatures = features.GetLength(1);
            var flatFeatures = new float[1 * numFrames * numFeatures];
            
            for (int i = 0; i < numFrames; i++)
            {
                for (int j = 0; j < numFeatures; j++)
                {
                    flatFeatures[i * numFeatures + j] = features[i, j];
                }
            }
            
            // 创建输入张量 [batch, frames, features]
            var inputTensor = new DenseTensor<float>(flatFeatures, new[] { 1, numFrames, numFeatures });
            var lengthTensor = new DenseTensor<int>(new[] { numFrames }, new[] { 1 });
            
            Utils.Logger.Log($"[Recognize] 输入张量形状: [{1}, {numFrames}, {numFeatures}]");
            
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("speech", inputTensor),
                NamedOnnxValue.CreateFromTensor("speech_lengths", lengthTensor)
            };

            // 推理
            Utils.Logger.Log("[Recognize] 开始ONNX推理...");
            using var results = _session.Run(inputs);
            Utils.Logger.Log($"[Recognize] 推理完成，输出数量: {results.Count}");
            
            // 检查输出
            var firstResult = results.FirstOrDefault();
            if (firstResult == null)
            {
                Utils.Logger.Log("[Recognize] 错误: 推理结果为空!");
                throw new InvalidOperationException("推理结果为空");
            }
            
            // 调试输出信息
            Utils.Logger.Log($"[Recognize] 输出名称: {firstResult.Name}");
            Utils.Logger.Log($"[Recognize] 输出类型: {firstResult.GetType().Name}");
            Utils.Logger.Log($"[Recognize] 输出Value类型: {firstResult.Value?.GetType()?.Name ?? "null"}");
            
            // Paraformer输出的是logits (float)，需要在最后一维取argmax得到token ID
            var logits = firstResult.AsTensor<float>();
            if (logits == null)
            {
                Utils.Logger.Log("[Recognize] 错误: logits为null!");
                throw new InvalidOperationException("logits为null");
            }
            
            Utils.Logger.Log("[Recognize] 成功读取logits (float)");
            Utils.Logger.Log($"[Recognize] logits形状: [{string.Join(",", logits.Dimensions.ToArray())}]");
            
            // 取argmax得到token IDs
            var tokenIds = ArgMax(logits);
            
            Utils.Logger.Log($"[Recognize] tokenIds数量: {tokenIds.Length}");
            
            var decoded = Decode(tokenIds);
            Utils.Logger.Log($"[Recognize] 解码结果: {decoded}");
            
            return decoded;
        }

        private void LoadTokens()
        {
            Utils.Logger.Log($"[LoadTokens] 开始加载词汇表: {_tokensPath}");
            
            if (!File.Exists(_tokensPath))
                throw new FileNotFoundException("词汇表文件不存在", _tokensPath);

            var lines = File.ReadAllLines(_tokensPath);
            Utils.Logger.Log($"[LoadTokens] 文件行数: {lines.Length}");
            
            _tokens = lines
                     .Select((line, index) => new { line, index })
                     .Where(x => !string.IsNullOrWhiteSpace(x.line))
                     .Select(x => x.line.Trim())
                     .ToList();
            
            Utils.Logger.Log($"[LoadTokens] 词汇表数量: {_tokens?.Count ?? 0}");
            
            if (_tokens == null || _tokens.Count == 0)
            {
                throw new InvalidOperationException("词汇表为空，无法加载");
            }
            
            // 显示前10个token用于调试
            for (int i = 0; i < Math.Min(10, _tokens.Count); i++)
            {
                Utils.Logger.Log($"[LoadTokens] Token[{i}]: '{_tokens[i]}'");
            }
        }

        private void LoadAmMvn()
        {
            if (!File.Exists(_amMvnPath)) return;

            var lines = File.ReadAllLines(_amMvnPath);
            foreach (var line in lines)
            {
                if (line.StartsWith("<Mean>"))
                {
                    var values = line.Replace("<Mean>", "").Trim().Split(' ')
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .Select(float.Parse)
                                    .ToArray();
                    _amMvnMeans = values;
                }
                else if (line.StartsWith("<Vars>"))
                {
                    var values = line.Replace("<Vars>", "").Trim().Split(' ')
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .Select(float.Parse)
                                    .ToArray();
                    _amMvnVars = values;
                }
            }
        }

        private float[,] ExtractFeatures(float[] audioData)
        {
            // Paraformer 模型需要 560 维特征输入
            // 简化的特征提取 - 实际应该用完整的 FBank
            int frameSize = 400;  // 25ms @ 16kHz
            int hopSize = 160;    // 10ms @ 16kHz
            int numFeatures = 560;
            
            int numFrames = (audioData.Length - frameSize) / hopSize + 1;
            if (numFrames < 1) numFrames = 1;
            
            var features = new float[numFrames, numFeatures];
            
            // 简单特征：复制音频数据到特征维度
            for (int i = 0; i < numFrames; i++)
            {
                int frameStart = i * hopSize;
                for (int j = 0; j < numFeatures && (frameStart + j) < audioData.Length; j++)
                {
                    features[i, j] = audioData[frameStart + j];
                }
            }
            
            return features;
        }

        private void NormalizeFeatures(float[,] features)
        {
            if (_amMvnMeans == null || _amMvnVars == null) return;

            int numFrames = features.GetLength(0);
            int numFeatures = features.GetLength(1);

            for (int i = 0; i < numFrames; i++)
            {
                for (int j = 0; j < numFeatures && j < _amMvnMeans.Length; j++)
                {
                    features[i, j] = (features[i, j] - _amMvnMeans[j]) / _amMvnVars[j];
                }
            }
        }

        /// <summary>
        /// 在logits上取argmax
        /// </summary>
        private long[] ArgMax(Tensor<float> logits)
        {
            // logits形状: [batch, time, vocab_size]
            int batchSize = logits.Dimensions[0];
            int timeSteps = logits.Dimensions[1];
            int vocabSize = logits.Dimensions[2];
            
            var result = new long[timeSteps];
            
            for (int t = 0; t < timeSteps; t++)
            {
                float maxVal = float.MinValue;
                int maxIdx = 0;
                
                for (int v = 0; v < vocabSize; v++)
                {
                    float val = logits[0, t, v];
                    if (val > maxVal)
                    {
                        maxVal = val;
                        maxIdx = v;
                    }
                }
                
                result[t] = maxIdx;
            }
            
            return result;
        }

        private string Decode(long[] tokenIds)
        {
            Utils.Logger.Log("[Decode] 开始解码...");
            
            if (_tokens == null)
            {
                Utils.Logger.Log("[Decode] 错误: _tokens 为 null!");
                return string.Empty;
            }
            
            if (tokenIds == null || tokenIds.Length == 0)
            {
                Utils.Logger.Log("[Decode] 错误: tokenIds为空!");
                return string.Empty;
            }

            Utils.Logger.Log($"[Decode] 输出ID数量: {tokenIds.Length}");

            var sb = new System.Text.StringBuilder();
            foreach (var id in tokenIds)
            {
                if (id >= 0 && id < _tokens.Count)
                {
                    var token = _tokens[(int)id];
                    // 过滤特殊标记
                    if (token != "<blank>" && 
                        token != "<sos/eos>" && 
                        token != "<s>" && 
                        token != "</s>")
                    {
                        sb.Append(token);
                    }
                }
                else
                {
                    Utils.Logger.Log($"[Decode] 警告: ID {id} 超出范围 [0, {_tokens.Count})");
                }
            }
            
            return sb.ToString();
        }

        private float[] Resample(float[] audioData, int srcRate, int dstRate)
        {
            // 简化的重采样
            double ratio = (double)dstRate / srcRate;
            int newLength = (int)(audioData.Length * ratio);
            var result = new float[newLength];
            
            for (int i = 0; i < newLength; i++)
            {
                int srcIdx = (int)(i / ratio);
                if (srcIdx < audioData.Length)
                {
                    result[i] = audioData[srcIdx];
                }
            }
            
            return result;
        }

        public void Dispose()
        {
            Unload();
        }
    }
}
