using System;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using WordFlow.Infrastructure;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// 多语言本地化服务
    /// 负责管理应用语言切换和资源加载
    /// </summary>
    public class LocalizationService
    {
        private static LocalizationService? _instance;
        private static readonly object Lock = new();
        
        /// <summary>
        /// 单例实例
        /// </summary>
        public static LocalizationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (Lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LocalizationService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 语言变更事件
        /// </summary>
        public event Action<string>? LanguageChanged;

        /// <summary>
        /// 当前语言代码
        /// </summary>
        public string CurrentLanguageCode { get; private set; } = "zh-CN";

        private LocalizationService()
        {
        }

        /// <summary>
        /// 初始化语言（应用启动时调用）
        /// </summary>
        public void Initialize(string languageCode)
        {
            Logger.Log($"[LocalizationService] Initialize 被调用，languageCode={languageCode}");
            CurrentLanguageCode = languageCode;
            ApplyCulture(languageCode);
            Logger.Log($"[LocalizationService] 初始化完成：{languageCode}");
        }

        /// <summary>
        /// 切换语言
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            Logger.Log($"[LocalizationService] SetLanguage 被调用：{languageCode}, 当前={CurrentLanguageCode}");
            
            if (CurrentLanguageCode == languageCode)
            {
                Logger.Log($"[LocalizationService] 语言未变更：{languageCode}");
                return;
            }

            Logger.Log($"[LocalizationService] 切换语言：{CurrentLanguageCode} -> {languageCode}");
            CurrentLanguageCode = languageCode;
            ApplyCulture(languageCode);
            
            // 发布语言变更事件
            LanguageChanged?.Invoke(languageCode);
            Logger.Log($"[LocalizationService] 语言变更事件已发布");
        }

        /// <summary>
        /// 应用文化设置
        /// </summary>
        private void ApplyCulture(string languageCode)
        {
            try
            {
                Logger.Log($"[LocalizationService] ApplyCulture 开始：{languageCode}");
                
                var cultureInfo = new CultureInfo(languageCode);
                Logger.Log($"[LocalizationService] CultureInfo 创建成功：{cultureInfo.Name} - {cultureInfo.DisplayName}");

                // 关键修复：设置 Strings.Culture，这是 ResourceManager 查找资源的依据
                WordFlow.Resources.Strings.Strings.Culture = cultureInfo;
                Logger.Log($"[LocalizationService] Strings.Culture 已设置：{cultureInfo.Name}");

                // 设置当前线程的文化特性
                System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;
                System.Threading.Thread.CurrentThread.CurrentUICulture = cultureInfo;
                Logger.Log($"[LocalizationService] Thread 文化设置完成");

                // 设置 WPF 的默认语言
                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(
                        XmlLanguage.GetLanguage(cultureInfo.IetfLanguageTag)));

                Logger.Log($"[LocalizationService] WPF 语言设置完成");
                Logger.Log($"[LocalizationService] 文化设置已应用：{languageCode}");
            }
            catch (Exception ex)
            {
                Logger.Log($"[LocalizationService] 应用文化设置失败：{ex.Message}");
                Logger.Log($"[LocalizationService] 堆栈跟踪：{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 获取资源字符串
        /// </summary>
        public string GetString(string key)
        {
            return Resources.Strings.Strings.ResourceManager.GetString(key) ?? key;
        }

        /// <summary>
        /// 获取资源字符串（带格式参数）
        /// </summary>
        public string GetString(string key, params object[] args)
        {
            var format = Resources.Strings.Strings.ResourceManager.GetString(key) ?? key;
            return string.Format(format, args);
        }
    }
}
