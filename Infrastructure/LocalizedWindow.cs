using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WordFlow.Resources.Strings;
using WordFlow.Services;

namespace WordFlow.Infrastructure
{
    /// <summary>
    /// 本地化窗口基类 - 自动处理多语言切换
    /// 所有窗口应继承此类以获得自动多语言支持
    /// </summary>
    public abstract class LocalizedWindow : Window
    {
        protected LocalizedWindow()
        {
            // 窗口加载时应用本地化
            Loaded += OnLoaded;
            
            // 订阅语言变化事件
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyLocalization();
        }

        private void OnLanguageChanged(string languageCode)
        {
            // 语言变化时，在 UI 线程重新应用本地化
            Dispatcher.Invoke(() =>
            {
                ApplyLocalization();
                OnLanguageChangedInternal(languageCode);
            });
        }

        /// <summary>
        /// 应用本地化到窗口及其所有子元素
        /// </summary>
        internal void ApplyLocalization()
        {
            // 设置窗口标题
            var titleKey = GetWindowTitleResourceKey();
            if (!string.IsNullOrEmpty(titleKey))
            {
                this.Title = Strings.ResourceManager.GetString(titleKey, Strings.Culture) ?? titleKey;
            }

            // 递归应用本地化到所有子元素
            ApplyLocalizationToElement(this);
        }

        /// <summary>
        /// 递归应用本地化到元素树
        /// </summary>
        private void ApplyLocalizationToElement(DependencyObject element)
        {
            // 获取元素的资源键
            var resourceKey = LocalizeExtension.GetResourceKey(element);
            
            if (!string.IsNullOrEmpty(resourceKey))
            {
                ApplyResourceValue(element, resourceKey);
            }

            // 处理特殊控件的附加属性
            if (element is FrameworkElement fe)
            {
                // 处理 ToolTip
                var toolTipKey = LocalizeExtension.GetToolTipKey(element);
                if (!string.IsNullOrEmpty(toolTipKey))
                {
                    var toolTipValue = Strings.ResourceManager.GetString(toolTipKey, Strings.Culture) ?? toolTipKey;
                    if (element is Button button)
                    {
                        ToolTipService.SetToolTip(button, new ToolTip { Content = toolTipValue });
                    }
                }
            }

            // 递归处理子元素
            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                ApplyLocalizationToElement(child);
            }
        }

        /// <summary>
        /// 应用资源值到控件
        /// </summary>
        private void ApplyResourceValue(DependencyObject element, string resourceKey)
        {
            var value = Strings.ResourceManager.GetString(resourceKey, Strings.Culture) ?? resourceKey;

            if (element is TextBlock textBlock)
            {
                textBlock.Text = value;
            }
            else if (element is Button button && button.Content is string)
            {
                button.Content = value;
            }
            else if (element is Label label && label.Content is string)
            {
                label.Content = value;
            }
            else if (element is CheckBox checkBox && checkBox.Content is string)
            {
                checkBox.Content = value;
            }
            else if (element is RadioButton radioButton && radioButton.Content is string)
            {
                radioButton.Content = value;
            }
            else if (element is TabItem tabItem && tabItem.Header is string)
            {
                tabItem.Header = value;
            }
            else if (element is HeaderedContentControl headeredControl && headeredControl.Header is string)
            {
                headeredControl.Header = value;
            }
            else if (element is GroupBox groupBox && groupBox.Header is string)
            {
                groupBox.Header = value;
            }
            else if (element is MenuItem menuItem && menuItem.Header is string)
            {
                menuItem.Header = value;
            }
        }

        /// <summary>
        /// 子类重写返回窗口标题的资源键
        /// </summary>
        protected virtual string GetWindowTitleResourceKey() => string.Empty;

        /// <summary>
        /// 子类可重写此方法进行额外的本地化
        /// </summary>
        protected virtual void OnLanguageChangedInternal(string languageCode) { }

        /// <summary>
        /// 获取本地化字符串的辅助方法
        /// </summary>
        protected string GetString(string key)
        {
            return Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // 取消订阅，防止内存泄漏
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        }
    }

    /// <summary>
    /// 本地化用户控件基类
    /// </summary>
    public abstract class LocalizedUserControl : UserControl
    {
        protected LocalizedUserControl()
        {
            Loaded += (s, e) => ApplyLocalization();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(string languageCode)
        {
            Dispatcher.Invoke(() => ApplyLocalization());
        }

        internal void ApplyLocalization()
        {
            ApplyLocalizationToElement(this);
        }

        private void ApplyLocalizationToElement(DependencyObject element)
        {
            var resourceKey = LocalizeExtension.GetResourceKey(element);
            if (!string.IsNullOrEmpty(resourceKey))
            {
                ApplyResourceValue(element, resourceKey);
            }

            // 处理特殊控件的附加属性
            if (element is FrameworkElement fe)
            {
                // 处理 ToolTip
                var toolTipKey = LocalizeExtension.GetToolTipKey(element);
                if (!string.IsNullOrEmpty(toolTipKey))
                {
                    var toolTipValue = Strings.ResourceManager.GetString(toolTipKey, Strings.Culture) ?? toolTipKey;
                    if (element is Button button)
                    {
                        ToolTipService.SetToolTip(button, new ToolTip { Content = toolTipValue });
                    }
                }
            }

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                ApplyLocalizationToElement(child);
            }
        }

        private void ApplyResourceValue(DependencyObject element, string resourceKey)
        {
            var value = Strings.ResourceManager.GetString(resourceKey, Strings.Culture) ?? resourceKey;

            if (element is TextBlock textBlock)
            {
                textBlock.Text = value;
            }
            else if (element is Button button && button.Content is string)
            {
                button.Content = value;
            }
            else if (element is Label label && label.Content is string)
            {
                label.Content = value;
            }
            else if (element is TabItem tabItem && tabItem.Header is string)
            {
                tabItem.Header = value;
            }
            else if (element is CheckBox checkBox && checkBox.Content is string)
            {
                checkBox.Content = value;
            }
            else if (element is RadioButton radioButton && radioButton.Content is string)
            {
                radioButton.Content = value;
            }
            else if (element is ComboBoxItem comboBoxItem && comboBoxItem.Content is string)
            {
                comboBoxItem.Content = value;
            }
            else if (element is GroupBox groupBox && groupBox.Header is string)
            {
                groupBox.Header = value;
            }
            else if (element is HeaderedContentControl headeredControl && headeredControl.Header is string)
            {
                headeredControl.Header = value;
            }
        }

        protected string GetString(string key)
        {
            return Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;
        }
    }

    /// <summary>
    /// XAML 附加属性 - 用于声明式本地化
    /// </summary>
    public static class LocalizeExtension
    {
        // 资源键附加属性
        public static readonly DependencyProperty ResourceKeyProperty =
            DependencyProperty.RegisterAttached(
                "ResourceKey", typeof(string), typeof(LocalizeExtension),
                new PropertyMetadata(null, OnResourceKeyChanged));

        // ToolTip 资源键附加属性
        public static readonly DependencyProperty ToolTipKeyProperty =
            DependencyProperty.RegisterAttached(
                "ToolTipKey", typeof(string), typeof(LocalizeExtension),
                new PropertyMetadata(null));

        public static void SetResourceKey(DependencyObject element, string value) =>
            element.SetValue(ResourceKeyProperty, value);

        public static string GetResourceKey(DependencyObject element) =>
            (string)element.GetValue(ResourceKeyProperty);

        public static void SetToolTipKey(DependencyObject element, string value) =>
            element.SetValue(ToolTipKeyProperty, value);

        public static string GetToolTipKey(DependencyObject element) =>
            (string)element.GetValue(ToolTipKeyProperty);

        private static void OnResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && e.NewValue is string resourceKey)
            {
                // 如果窗口已加载，立即应用本地化
                if (element is Window window && window.IsLoaded)
                {
                    if (window is LocalizedWindow localizedWindow)
                    {
                        localizedWindow.ApplyLocalization();
                    }
                }
                else if (element is UserControl userControl && userControl.IsLoaded)
                {
                    if (userControl is LocalizedUserControl localizedUserControl)
                    {
                        localizedUserControl.ApplyLocalization();
                    }
                }
            }
        }
    }
}
