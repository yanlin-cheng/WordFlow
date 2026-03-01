using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WordFlow.Infrastructure;
using WordFlow.Services;

namespace WordFlow.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly GlobalHotkeyServiceV2 _hotkeyService;
        private int _selectedHotkeyCode;
        private bool _isSaved = false;
        private ModelDownloadService? _modelDownloadService;

        public SettingsWindow(SettingsService settingsService, GlobalHotkeyServiceV2 hotkeyService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _hotkeyService = hotkeyService;
            _modelDownloadService = new ModelDownloadService();

            LoadSettings();
            LoadModelSettings();

            // 订阅窗口关闭事件，确保点击 X 也能保存
            Closing += OnWindowClosing;
        }
        
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 如果还没保存，自动保存
            if (!_isSaved)
            {
                SaveSettings();
            }
        }

        private void LoadSettings()
        {
            // 加载热键选项
            var hotkeys = SettingsService.GetAvailableHotkeys();
            HotkeyComboBox.ItemsSource = hotkeys;
            HotkeyComboBox.DisplayMemberPath = "Name";

            // 选中当前热键
            var currentHotkey = hotkeys.FirstOrDefault(h => h.Code == _settingsService.Settings.HotkeyCode);
            HotkeyComboBox.SelectedItem = currentHotkey ?? hotkeys[0];
            _selectedHotkeyCode = _settingsService.Settings.HotkeyCode;

            // 加载启动设置
            AutoStartCheckBox.IsChecked = AutoStartService.IsAutoStartEnabled();
            StartMinimizedCheckBox.IsChecked = _settingsService.Settings.StartMinimized;
        }

        private async void LoadModelSettings()
        {
            try
            {
                ModelStatusText.Text = "正在加载模型列表...";
                
                // 加载可用模型
                var models = await _modelDownloadService!.GetAvailableModelsAsync();
                
                if (models.Any())
                {
                    ModelListBox.ItemsSource = models.Select(m => new ModelItem
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Description = m.Description,
                        Size = m.Size,
                        IsInstalled = Directory.Exists(Path.Combine(_modelDownloadService.GetModelsDir(), m.Id)),
                        IsSelected = false
                    }).ToList();
                    
                    ModelStatusText.Text = $"已加载 {models.Count} 个模型配置";
                }
                else
                {
                    ModelStatusText.Text = "未找到模型配置";
                }
            }
            catch (Exception ex)
            {
                ModelStatusText.Text = $"加载模型失败：{ex.Message}";
            }
        }

        private void HotkeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HotkeyComboBox.SelectedItem is HotkeyOption option)
            {
                _selectedHotkeyCode = option.Code;
                HotkeyHintText.Text = $"按住 {option.Name} 说话，松开后自动识别";
            }
        }

        private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // 即时生效
            if (AutoStartCheckBox.IsChecked == true)
            {
                AutoStartService.EnableAutoStart();
            }
            else
            {
                AutoStartService.DisableAutoStart();
            }
        }

        private void StartMinimizedCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // 即时生效
            _settingsService.Settings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
            _settingsService.Save();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            
            // 发布设置变更事件，通知 MainWindow 更新 UI
            EventBus.Publish(new SettingsChangedEvent
            {
                ChangedProperty = "HotkeyCode",
                NewValue = _selectedHotkeyCode
            });
            
            MessageBox.Show("设置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            _isSaved = true;
            Close();
        }
        
        private void SaveSettings()
        {
            // 保存热键设置（始终更新，确保生效）
            _settingsService.Settings.HotkeyCode = _selectedHotkeyCode;
            _hotkeyService.HotkeyCode = _selectedHotkeyCode;
            Utils.Logger.Log($"设置窗口：热键已更新为 {_selectedHotkeyCode}");

            // 保存其他设置
            _settingsService.Settings.AutoStart = AutoStartCheckBox.IsChecked ?? false;
            _settingsService.Settings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
            _settingsService.Save();
            _isSaved = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void RefreshModelButton_Click(object sender, RoutedEventArgs e)
        {
            LoadModelSettings();
        }

        private async void DownloadModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModelListBox.SelectedItem is not ModelItem selectedModel)
            {
                MessageBox.Show("请先选择一个模型", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selectedModel.IsInstalled)
            {
                MessageBox.Show("该模型已安装", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                DownloadModelButton.IsEnabled = false;
                DownloadProgressText.Text = "正在下载...";

                var models = await _modelDownloadService!.GetAvailableModelsAsync();
                var model = models.FirstOrDefault(m => m.Id == selectedModel.Id);
                
                if (model == null)
                {
                    MessageBox.Show("未找到模型配置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = await _modelDownloadService.DownloadModelAsync(model);
                
                if (result.Success)
                {
                    DownloadProgressText.Text = "下载完成！";
                    MessageBox.Show($"模型 {model.Name} 下载并安装成功！", "成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadModelSettings(); // 刷新列表
                }
                else
                {
                    DownloadProgressText.Text = "下载失败";
                    MessageBox.Show($"下载失败：{result.Error}", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                DownloadProgressText.Text = "下载失败";
                MessageBox.Show($"下载异常：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DownloadModelButton.IsEnabled = true;
            }
        }

        private void MirrorRadio_Changed(object sender, RoutedEventArgs e)
        {
            // 这里可以添加切换下载源的逻辑
            // 目前由安装器处理
        }
    }

    public class ModelItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Size { get; set; } = "";
        public bool IsInstalled { get; set; }
        public bool IsSelected { get; set; }
        
        public string StatusText => IsInstalled ? "✓ 已安装" : "⚠ 未安装";
        public System.Windows.Media.Brush StatusColor => IsInstalled 
            ? System.Windows.Media.Brushes.Green 
            : System.Windows.Media.Brushes.Orange;
    }
}
