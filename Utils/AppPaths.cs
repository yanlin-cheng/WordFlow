using System;
using System.IO;

namespace WordFlow.Utils
{
    /// <summary>
    /// 应用程序路径管理
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// 应用根目录（程序所在文件夹）
        /// </summary>
        public static string AppDirectory => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// 数据目录（安装目录/Data）
        /// </summary>
        public static string DataDirectory
        {
            get
            {
                var path = Path.Combine(AppDirectory, "Data");
                EnsureDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// 录音文件目录
        /// </summary>
        public static string RecordingsDirectory
        {
            get
            {
                var path = Path.Combine(DataDirectory, "Recordings");
                EnsureDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// 词典导出目录
        /// </summary>
        public static string ExportsDirectory
        {
            get
            {
                var path = Path.Combine(DataDirectory, "Exports");
                EnsureDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// 数据库文件路径
        /// </summary>
        public static string DatabasePath => Path.Combine(DataDirectory, "history.db");

        /// <summary>
        /// 生成录音文件名
        /// </summary>
        public static string GenerateRecordingFileName()
        {
            return $"recording_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav";
        }

        /// <summary>
        /// 获取完整的录音文件路径
        /// </summary>
        public static string GetRecordingFilePath()
        {
            return Path.Combine(RecordingsDirectory, GenerateRecordingFileName());
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// 清理旧录音文件（保留最近N天的）
        /// </summary>
        public static void CleanupOldRecordings(int keepDays = 7)
        {
            try
            {
                if (!Directory.Exists(RecordingsDirectory)) return;

                var cutoffDate = DateTime.Now.AddDays(-keepDays);
                var files = Directory.GetFiles(RecordingsDirectory, "*.wav");
                int deletedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            fileInfo.Delete();
                            deletedCount++;
                        }
                    }
                    catch { /* 忽略单个文件删除错误 */ }
                }

                if (deletedCount > 0)
                {
                    Logger.Log($"清理了 {deletedCount} 个旧录音文件");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"清理旧录音文件失败: {ex.Message}");
            }
        }
    }
}
