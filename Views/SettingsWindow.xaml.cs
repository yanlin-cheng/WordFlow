using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WordFlow.Infrastructure;
using WordFlow.Resources.Strings;
using WordFlow.Services;
using WordFlow.Utils;

namespace WordFlow.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly GlobalHotkeyServiceV2 _hotkeyService;
        private int _selectedHotkeyCode;
        private bool _isSaved = false;
        private string _selectedLanguageCode;

        public SettingsWindow(SettingsService settingsService, GlobalHotkeyServiceV2 hotkeyService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _hotkeyService = hotkeyService;

            LoadSettings();

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

            // 加载语言设置
            _selectedLanguageCode = _settingsService.Settings.LanguageCode ?? "zh-CN";
            var languageIndex = GetLanguageIndexByCode(_selectedLanguageCode);
            LanguageComboBox.SelectedIndex = languageIndex;
        }

        private int GetLanguageIndexByCode(string code)
        {
            return code switch
            {
                "zh-CN" => 0,
                "en-US" => 1,
                "ja-JP" => 2,
                "ko-KR" => 3,
                _ => 0
            };
        }

        private string GetLanguageCodeByIndex(int index)
        {
            return index switch
            {
                0 => "zh-CN",
                1 => "en-US",
                2 => "ja-JP",
                3 => "ko-KR",
                _ => "zh-CN"
            };
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _selectedLanguageCode = selectedItem.Tag?.ToString() ?? "zh-CN";
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
            
            MessageBox.Show(Strings.Message_SaveSuccess, Strings.SettingsWindow_Title, MessageBoxButton.OK, MessageBoxImage.Information);
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

            // 保存语言设置
            var oldLanguageCode = _settingsService.Settings.LanguageCode ?? "zh-CN";
            _settingsService.Settings.LanguageCode = _selectedLanguageCode;

            // 保存其他设置
            _settingsService.Settings.AutoStart = AutoStartCheckBox.IsChecked ?? false;
            _settingsService.Settings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
            _settingsService.Save();
            _isSaved = true;

            // 如果语言改变了，提示重启
            if (oldLanguageCode != _selectedLanguageCode)
            {
                // 检查是否在调试模式下
                if (Debugger.IsAttached)
                {
                    // 调试模式下不重启，只提示用户
                    MessageBox.Show(
                        $"语言设置已保存。\n\n{Strings.Message_RestartRequired}",
                        Strings.Message_SaveSuccess,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    var result = MessageBox.Show(
                        Strings.Message_RestartRequired,
                        Strings.Message_SaveSuccess,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 重启应用 - 使用 AppContext.BaseDirectory 替代 Assembly.Location
                        var appPath = AppContext.BaseDirectory + "\\WordFlow.exe";
                        System.Diagnostics.Process.Start(new ProcessStartInfo
                        {
                            FileName = appPath,
                            UseShellExecute = true
                        });
                        Application.Current.Shutdown();
                    }
                }
            }
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

        private void RunWizardButton_Click(object sender, RoutedEventArgs e)
        {
            // 打开首次运行向导
            var wizard = new FirstRunWizard();
            wizard.Owner = this;
            wizard.ShowDialog();
        }

        private void ManageModelButton_Click(object sender, RoutedEventArgs e)
        {
            // 打开模型管理窗口
            var modelManager = new ModelManagerWindow();
            modelManager.Owner = this;
            modelManager.ShowDialog();
        }
    }
}
