using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WordFlow.Models;
using WordFlow.Utils;
using Dapper;

namespace WordFlow.Services
{
    /// <summary>
    /// 输入历史服务 - 管理用户输入历史记录
    /// </summary>
    public class HistoryService : IDisposable
    {
        private readonly string _dbPath;
        private SqliteConnection? _connection;
        private bool _initialized = false;
        private readonly object _lock = new object();
        
        public HistoryService()
        {
            try
            {
                // 使用 AppPaths 管理的路径
                _dbPath = AppPaths.DatabasePath;
                
                // 确保数据库所在目录存在
                var dbDir = System.IO.Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(dbDir) && !System.IO.Directory.Exists(dbDir))
                {
                    System.IO.Directory.CreateDirectory(dbDir);
                    Logger.Log($"HistoryService: 创建数据库目录 {dbDir}");
                }
                
                Logger.Log($"HistoryService: 数据库路径 {_dbPath}");
                
                // 延迟初始化数据库连接
                // 避免在应用启动时立即加载 SQLite 原生库
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 构造函数失败 - {ex.Message}");
                // 不抛出异常，允许服务继续运行（数据库功能将不可用）
            }
        }
        
        /// <summary>
        /// 确保数据库连接已初始化
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized && _connection != null)
                return;
                
            lock (_lock)
            {
                if (_initialized && _connection != null)
                    return;
                    
                try
                {
                    Logger.Log($"HistoryService: 正在初始化数据库连接...");
                    _connection = new SqliteConnection($"Data Source={_dbPath}");
                    _connection.Open();
                    InitializeDatabase();
                    _initialized = true;
                    Logger.Log("HistoryService: 数据库初始化成功");
                }
                catch (Exception ex)
                {
                    Logger.Log($"HistoryService: 数据库初始化失败 - {ex.Message}");
                    Logger.Log($"HistoryService: 内部异常 - {ex.InnerException?.Message}");
                    Logger.Log($"HistoryService: 堆栈跟踪 - {ex.StackTrace}");
                    
                    // 不抛出异常，允许服务继续运行（数据库功能将不可用）
                    _initialized = false;
                }
            }
        }
        
        /// <summary>
        /// 检查数据库是否可用
        /// </summary>
        public bool IsDatabaseAvailable => _initialized && _connection != null && _connection.State == System.Data.ConnectionState.Open;
        
        /// <summary>
        /// 初始化数据库表
        /// </summary>
        private void InitializeDatabase()
        {
            // 输入历史表
            _connection.Execute(@"
                CREATE TABLE IF NOT EXISTS InputHistory (
                    Id TEXT PRIMARY KEY,
                    Timestamp TEXT NOT NULL,
                    OriginalText TEXT NOT NULL,
                    CorrectedText TEXT,
                    TargetWindowTitle TEXT,
                    TargetApplication TEXT,
                    RecordingDuration REAL,
                    Confidence REAL,
                    Scene INTEGER DEFAULT 0,
                    Tags TEXT,
                    IsSynced INTEGER DEFAULT 0,
                    IsUsedForTraining INTEGER DEFAULT 0,
                    AudioFilePath TEXT
                )");
            
            // 个人词典表
            _connection.Execute(@"
                CREATE TABLE IF NOT EXISTS PersonalVocabulary (
                    Id TEXT PRIMARY KEY,
                    Word TEXT NOT NULL UNIQUE,
                    Pinyin TEXT,
                    Frequency INTEGER DEFAULT 1,
                    FirstUsed TEXT,
                    LastUsed TEXT,
                    Weight REAL DEFAULT 1.0,
                    Category INTEGER DEFAULT 0,
                    Contexts TEXT,
                    ConfusableWords TEXT,
                    Source INTEGER DEFAULT 1,
                    IsSynced INTEGER DEFAULT 0,
                    RelatedHistoryIds TEXT
                )");
            
            // 修正记录表
            _connection.Execute(@"
                CREATE TABLE IF NOT EXISTS CorrectionLog (
                    Id TEXT PRIMARY KEY,
                    InputHistoryId TEXT NOT NULL,
                    Timestamp TEXT NOT NULL,
                    WrongWord TEXT NOT NULL,
                    CorrectWord TEXT NOT NULL,
                    WrongPinyin TEXT,
                    CorrectPinyin TEXT,
                    OriginalSentence TEXT,
                    CorrectedSentence TEXT,
                    ContextBefore TEXT,
                    ContextAfter TEXT,
                    ErrorType INTEGER DEFAULT 0,
                    IsUsedForTraining INTEGER DEFAULT 0,
                    GeneratedVocabularyId TEXT
                )");
            
            // 创建索引优化查询
            _connection.Execute("CREATE INDEX IF NOT EXISTS idx_history_time ON InputHistory(Timestamp)");
            _connection.Execute("CREATE INDEX IF NOT EXISTS idx_vocab_word ON PersonalVocabulary(Word)");
            _connection.Execute("CREATE INDEX IF NOT EXISTS idx_correction_time ON CorrectionLog(Timestamp)");
        }
        
        #region 输入历史操作
        
        /// <summary>
        /// 保存输入历史
        /// </summary>
        public async Task SaveInputHistoryAsync(InputHistory history)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                Logger.Log("HistoryService: 数据库不可用，跳过保存输入历史");
                return;
            }
            
            try
            {
                var sql = @"
                    INSERT OR REPLACE INTO InputHistory 
                    (Id, Timestamp, OriginalText, CorrectedText, TargetWindowTitle, 
                     TargetApplication, RecordingDuration, Confidence, Scene, Tags, 
                     IsSynced, IsUsedForTraining, AudioFilePath)
                    VALUES 
                    (@Id, @Timestamp, @OriginalText, @CorrectedText, @TargetWindowTitle, 
                     @TargetApplication, @RecordingDuration, @Confidence, @Scene, @Tags, 
                     @IsSynced, @IsUsedForTraining, @AudioFilePath)";
                
                await _connection.ExecuteAsync(sql, new
                {
                    history.Id,
                    Timestamp = history.Timestamp.ToString("O"),
                    history.OriginalText,
                    history.CorrectedText,
                    history.TargetWindowTitle,
                    history.TargetApplication,
                    history.RecordingDuration,
                    history.Confidence,
                    Scene = (int)history.Scene,
                    Tags = string.Join(",", history.Tags),
                    IsSynced = history.IsSynced ? 1 : 0,
                    IsUsedForTraining = history.IsUsedForTraining ? 1 : 0,
                    history.AudioFilePath
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 保存输入历史失败 - {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取所有输入历史
        /// </summary>
        public async Task<List<InputHistory>> GetAllHistoryAsync()
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                return new List<InputHistory>();
            }
            
            try
            {
                var sql = "SELECT * FROM InputHistory ORDER BY Timestamp DESC";
                var results = await _connection.QueryAsync<dynamic>(sql);
                return results.Select(MapToInputHistory).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 获取所有历史失败 - {ex.Message}");
                return new List<InputHistory>();
            }
        }
        
        /// <summary>
        /// 按日期范围获取输入历史
        /// </summary>
        public async Task<List<InputHistory>> GetHistoryByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                return new List<InputHistory>();
            }
            
            try
            {
                var sql = @"
                    SELECT * FROM InputHistory 
                    WHERE Timestamp >= @StartDate AND Timestamp <= @EndDate
                    ORDER BY Timestamp DESC";
                
                var results = await _connection.QueryAsync<dynamic>(sql, new 
                { 
                    StartDate = startDate.ToString("O"),
                    EndDate = endDate.ToString("O")
                });
                return results.Select(MapToInputHistory).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 按日期范围获取历史失败 - {ex.Message}");
                return new List<InputHistory>();
            }
        }
        
        /// <summary>
        /// 获取最近 N 条输入历史
        /// </summary>
        public async Task<List<InputHistory>> GetRecentHistoryAsync(int count = 100)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                Logger.Log("HistoryService: 数据库不可用，返回空列表");
                return new List<InputHistory>();
            }
            
            try
            {
                var sql = @"
                    SELECT * FROM InputHistory 
                    ORDER BY Timestamp DESC 
                    LIMIT @Count";
                
                var results = await _connection.QueryAsync<dynamic>(sql, new { Count = count });
                return results.Select(MapToInputHistory).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 获取最近历史失败 - {ex.Message}");
                return new List<InputHistory>();
            }
        }
        
        /// <summary>
        /// 搜索输入历史
        /// </summary>
        public async Task<List<InputHistory>> SearchHistoryAsync(string keyword, int limit = 50)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                return new List<InputHistory>();
            }
            
            try
            {
                var sql = @"
                    SELECT * FROM InputHistory 
                    WHERE OriginalText LIKE @Keyword OR CorrectedText LIKE @Keyword
                    ORDER BY Timestamp DESC 
                    LIMIT @Limit";
                
                var results = await _connection.QueryAsync<dynamic>(sql, new 
                { 
                    Keyword = $"%{keyword}%",
                    Limit = limit 
                });
                return results.Select(MapToInputHistory).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 搜索历史失败 - {ex.Message}");
                return new List<InputHistory>();
            }
        }
        
        /// <summary>
        /// 获取未用于训练的输入历史
        /// </summary>
        public async Task<List<InputHistory>> GetUnprocessedHistoryAsync(int count = 1000)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                return new List<InputHistory>();
            }
            
            try
            {
                var sql = @"
                    SELECT * FROM InputHistory 
                    WHERE IsUsedForTraining = 0
                    ORDER BY Timestamp DESC 
                    LIMIT @Count";
                
                var results = await _connection.QueryAsync<dynamic>(sql, new { Count = count });
                return results.Select(MapToInputHistory).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 获取未训练历史失败 - {ex.Message}");
                return new List<InputHistory>();
            }
        }
        
        /// <summary>
        /// 标记已用于训练
        /// </summary>
        public async Task MarkAsTrainedAsync(Guid historyId)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                return;
            }
            
            try
            {
                var sql = "UPDATE InputHistory SET IsUsedForTraining = 1 WHERE Id = @Id";
                await _connection.ExecuteAsync(sql, new { Id = historyId.ToString() });
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 标记已训练失败 - {ex.Message}");
            }
        }
        
        #endregion
        
        #region 个人词典操作
        
        /// <summary>
        /// 添加或更新词汇
        /// </summary>
        public async Task UpsertVocabularyAsync(PersonalVocabulary vocab)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                Logger.Log("HistoryService: 数据库不可用，跳过添加词汇");
                return;
            }
            
            try
            {
                // 先检查是否存在
                var existing = await GetVocabularyByWordAsync(vocab.Word);
                
                if (existing != null)
                {
                    // 更新频率和权重
                    vocab.Id = existing.Id;
                    vocab.Frequency += existing.Frequency;
                    vocab.Weight = Math.Max(vocab.Weight, existing.Weight);
                    vocab.FirstUsed = existing.FirstUsed;
                    vocab.Contexts = existing.Contexts.Union(vocab.Contexts).Take(10).ToList();
                    // 确保列表已实例化
                    if (vocab.Contexts == null) vocab.Contexts = new List<string>();
                }
                
                var sql = @"
                    INSERT OR REPLACE INTO PersonalVocabulary 
                    (Id, Word, Pinyin, Frequency, FirstUsed, LastUsed, Weight, Category, 
                     Contexts, ConfusableWords, Source, IsSynced, RelatedHistoryIds)
                    VALUES 
                    (@Id, @Word, @Pinyin, @Frequency, @FirstUsed, @LastUsed, @Weight, @Category,
                     @Contexts, @ConfusableWords, @Source, @IsSynced, @RelatedHistoryIds)";
                
                // 确保列表不为 null
                var contexts = vocab.Contexts ?? new List<string>();
                var confusableWords = vocab.ConfusableWords ?? new List<string>();
                var relatedHistoryIds = vocab.RelatedHistoryIds ?? new List<Guid>();
                
                await _connection.ExecuteAsync(sql, new
                {
                    vocab.Id,
                    vocab.Word,
                    vocab.Pinyin,
                    vocab.Frequency,
                    FirstUsed = vocab.FirstUsed.ToString("O"),
                    LastUsed = vocab.LastUsed.ToString("O"),
                    vocab.Weight,
                    Category = (int)vocab.Category,
                    Contexts = string.Join("|", contexts),
                    ConfusableWords = string.Join("|", confusableWords),
                    Source = (int)vocab.Source,
                    IsSynced = vocab.IsSynced ? 1 : 0,
                    RelatedHistoryIds = string.Join("|", relatedHistoryIds)
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 添加词汇失败 - {ex.Message}");
            }
        }
        
        /// <summary>
        /// 根据词查询
        /// </summary>
        public async Task<PersonalVocabulary?> GetVocabularyByWordAsync(string word)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                return null;
            }
            
            try
            {
                var sql = "SELECT * FROM PersonalVocabulary WHERE Word = @Word";
                var result = await _connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { Word = word });
                return result == null ? null : MapToVocabulary(result);
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 查询词汇失败 - {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 获取高频词汇（用于导出热词）
        /// </summary>
        public async Task<List<PersonalVocabulary>> GetTopVocabularyAsync(int count = 100)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                return new List<PersonalVocabulary>();
            }
            
            try
            {
                var sql = @"
                    SELECT * FROM PersonalVocabulary 
                    ORDER BY Frequency DESC, Weight DESC 
                    LIMIT @Count";
                
                var results = await _connection.QueryAsync<dynamic>(sql, new { Count = count });
                return results.Select(MapToVocabulary).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 获取高频词汇失败 - {ex.Message}");
                return new List<PersonalVocabulary>();
            }
        }
        
        /// <summary>
        /// 导出热词文件（供语音识别引擎使用）
        /// </summary>
        public async Task<string> ExportHotwordsFileAsync(string outputPath)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                return outputPath;
            }
            
            try
            {
                var vocabList = await GetTopVocabularyAsync(500);
                
                var lines = vocabList.Select(v => $"{v.Word} {v.CalculateDynamicWeight():F1}");
                var content = string.Join("\n", lines);
                
                await System.IO.File.WriteAllTextAsync(outputPath, content);
                return outputPath;
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 导出热词失败 - {ex.Message}");
                return outputPath;
            }
        }
        
        #endregion
        
        #region 修正记录操作
        
        /// <summary>
        /// 保存修正记录
        /// </summary>
        public async Task SaveCorrectionAsync(CorrectionLog correction)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                Logger.Log("HistoryService: 数据库不可用，跳过保存修正记录");
                return;
            }
            
            try
            {
                var sql = @"
                    INSERT INTO CorrectionLog 
                    (Id, InputHistoryId, Timestamp, WrongWord, CorrectWord, WrongPinyin, CorrectPinyin,
                     OriginalSentence, CorrectedSentence, ContextBefore, ContextAfter, ErrorType,
                     IsUsedForTraining, GeneratedVocabularyId)
                    VALUES 
                    (@Id, @InputHistoryId, @Timestamp, @WrongWord, @CorrectWord, @WrongPinyin, @CorrectPinyin,
                     @OriginalSentence, @CorrectedSentence, @ContextBefore, @ContextAfter, @ErrorType,
                     @IsUsedForTraining, @GeneratedVocabularyId)";
                
                await _connection.ExecuteAsync(sql, new
                {
                    correction.Id,
                    InputHistoryId = correction.InputHistoryId.ToString(),
                    Timestamp = correction.Timestamp.ToString("O"),
                    correction.WrongWord,
                    correction.CorrectWord,
                    correction.WrongPinyin,
                    correction.CorrectPinyin,
                    correction.OriginalSentence,
                    correction.CorrectedSentence,
                    correction.ContextBefore,
                    correction.ContextAfter,
                    ErrorType = (int)correction.ErrorType,
                    IsUsedForTraining = correction.IsUsedForTraining ? 1 : 0,
                    GeneratedVocabularyId = correction.GeneratedVocabularyId?.ToString()
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 保存修正记录失败 - {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取未训练的修正记录
        /// </summary>
        public async Task<List<CorrectionLog>> GetUnprocessedCorrectionsAsync(int count = 500)
        {
            EnsureInitialized();
            if (!IsDatabaseAvailable || _connection == null)
            {
                return new List<CorrectionLog>();
            }
            
            try
            {
                var sql = @"
                    SELECT * FROM CorrectionLog 
                    WHERE IsUsedForTraining = 0
                    ORDER BY Timestamp DESC 
                    LIMIT @Count";
                
                var results = await _connection.QueryAsync<dynamic>(sql, new { Count = count });
                return results.Select(MapToCorrection).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"HistoryService: 获取修正记录失败 - {ex.Message}");
                return new List<CorrectionLog>();
            }
        }
        
        #endregion
        
        #region 数据映射
        
        private InputHistory MapToInputHistory(dynamic row)
        {
            var tagsStr = row.Tags?.ToString();
            var tags = string.IsNullOrEmpty(tagsStr)
                ? new List<string>()
                : new List<string>(tagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries));

            return new InputHistory
            {
                Id = Guid.Parse(row.Id),
                Timestamp = DateTime.Parse(row.Timestamp),
                OriginalText = row.OriginalText,
                CorrectedText = row.CorrectedText,
                TargetWindowTitle = row.TargetWindowTitle,
                TargetApplication = row.TargetApplication,
                RecordingDuration = row.RecordingDuration != null ? (double)row.RecordingDuration : 0.0,
                Confidence = row.Confidence != null ? (double)row.Confidence : 0.0,
                Scene = (InputScene)(int)row.Scene,
                Tags = tags,
                IsSynced = row.IsSynced == 1,
                IsUsedForTraining = row.IsUsedForTraining == 1,
                AudioFilePath = row.AudioFilePath
            };
        }
        
        private PersonalVocabulary MapToVocabulary(dynamic row)
        {
            var contextsStr = row.Contexts?.ToString();
            var contexts = string.IsNullOrEmpty(contextsStr)
                ? new List<string>()
                : new List<string>(contextsStr.Split('|', StringSplitOptions.RemoveEmptyEntries));

            var confusableStr = row.ConfusableWords?.ToString();
            var confusableWords = string.IsNullOrEmpty(confusableStr)
                ? new List<string>()
                : new List<string>(confusableStr.Split('|', StringSplitOptions.RemoveEmptyEntries));

            return new PersonalVocabulary
            {
                Id = Guid.Parse(row.Id),
                Word = row.Word,
                Pinyin = row.Pinyin,
                Frequency = row.Frequency != null ? (int)row.Frequency : 0,
                FirstUsed = DateTime.Parse(row.FirstUsed),
                LastUsed = DateTime.Parse(row.LastUsed),
                Weight = row.Weight != null ? (double)row.Weight : 1.0,
                Category = (VocabularyCategory)(int)row.Category,
                Contexts = contexts,
                ConfusableWords = confusableWords,
                Source = (VocabularySource)(int)row.Source,
                IsSynced = row.IsSynced == 1,
                RelatedHistoryIds = ParseGuidList(row.RelatedHistoryIds)
            };
        }
        
        private CorrectionLog MapToCorrection(dynamic row)
        {
            return new CorrectionLog
            {
                Id = Guid.Parse(row.Id),
                InputHistoryId = Guid.Parse(row.InputHistoryId),
                Timestamp = DateTime.Parse(row.Timestamp),
                WrongWord = row.WrongWord,
                CorrectWord = row.CorrectWord,
                WrongPinyin = row.WrongPinyin,
                CorrectPinyin = row.CorrectPinyin,
                OriginalSentence = row.OriginalSentence,
                CorrectedSentence = row.CorrectedSentence,
                ContextBefore = row.ContextBefore,
                ContextAfter = row.ContextAfter,
                ErrorType = (ErrorType)(int)row.ErrorType,
                IsUsedForTraining = row.IsUsedForTraining == 1,
                GeneratedVocabularyId = row.GeneratedVocabularyId != null ? Guid.Parse(row.GeneratedVocabularyId) : null
            };
        }
        
        #endregion
        
        #region 辅助方法
        
        private List<Guid> ParseGuidList(dynamic? value)
        {
            if (value == null) return new List<Guid>();
            var str = value.ToString();
            if (string.IsNullOrEmpty(str)) return new List<Guid>();
            
            var guids = new List<Guid>();
            foreach (var part in str.Split('|'))
            {
                if (Guid.TryParse(part, out Guid guid))
                {
                    guids.Add(guid);
                }
            }
            return guids;
        }
        
        #endregion
        
        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
