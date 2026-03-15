using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using WordFlow.Infrastructure;
using WordFlow.Models;
using WordFlow.Services;
using WordFlow.Utils;

// 解决 WinForms/WPF 命名空间冲突
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace WordFlow.Views
{
    /// <summary>
    /// 个人词典管理窗口
    /// </summary>
    public partial class VocabularyManagerWindow : LocalizedWindow
    {
        private readonly HistoryService? _historyService;
        private readonly VocabularyLearningEngine? _learningEngine;
        private readonly AIVocabularyService? _aiService;
        private List<PersonalVocabulary> _allVocabularies = new();
        private List<InputHistory> _allHistory = new();
        private List<Guid> _selectedHistoryIds = new();

        public VocabularyManagerWindow()
        {
            InitializeComponent();

            try
            {
                _historyService = new HistoryService();
                _learningEngine = new VocabularyLearningEngine(_historyService);
                _aiService = new AIVocabularyService(_historyService);

                Loaded += OnWindowLoaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            await RefreshVocabularyAsync();
            await RefreshHistoryAsync();
        }

        #region 词典标签页功能

        /// <summary>
        /// 刷新词典数据
        /// </summary>
        private async Task RefreshVocabularyAsync()
        {
            if (_historyService == null)
            {
                StatusText.Text = "历史服务未初始化";
                Debug.WriteLine("[VocabularyManager] _historyService is null");
                return;
            }

            try
            {
                StatusText.Text = "正在加载词典...";
                Debug.WriteLine("[VocabularyManager] 开始加载词汇列表...");
                
                _allVocabularies = await _historyService.GetTopVocabularyAsync(1000);
                Debug.WriteLine($"[VocabularyManager] 从数据库获取到 {_allVocabularies.Count} 个词汇");

                // 按频率排序
                _allVocabularies = _allVocabularies.OrderByDescending(v => v.Frequency).ToList();

                // 确保在UI线程更新
                Dispatcher.Invoke(() =>
                {
                    VocabularyDataGrid.ItemsSource = null; // 先清空
                    VocabularyDataGrid.ItemsSource = _allVocabularies;
                    Debug.WriteLine($"[VocabularyManager] DataGrid ItemsSource 已设置，数量: {_allVocabularies.Count}");
                });
                
                UpdateStats();
                StatusText.Text = $"共 {_allVocabularies.Count} 个词汇";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"加载失败: {ex.Message}";
                Debug.WriteLine($"[VocabularyManager] 加载词汇失败: {ex}");
                MessageBox.Show($"加载词汇失败: {ex.Message}\n\n{ex.StackTrace}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStats()
        {
            var total = _allVocabularies.Count;
            var aiGenerated = _allVocabularies.Count(v => v.Source == VocabularySource.AIGenerated);
            var autoLearned = _allVocabularies.Count(v => v.Source == VocabularySource.AutoLearned);
            var manual = _allVocabularies.Count(v => v.Source == VocabularySource.Manual);

            StatsText.Text = $"总计: {total} | AI生成: {aiGenerated} | 自动学习: {autoLearned} | 手动添加: {manual}";
        }

        /// <summary>
        /// 搜索过滤
        /// </summary>
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var keyword = SearchBox.Text?.ToLower() ?? "";

            if (string.IsNullOrEmpty(keyword))
            {
                VocabularyDataGrid.ItemsSource = _allVocabularies;
            }
            else
            {
                var filtered = _allVocabularies.Where(v =>
                    v.Word.ToLower().Contains(keyword) ||
                    v.Pinyin.ToLower().Contains(keyword) ||
                    v.Category.ToString().ToLower().Contains(keyword)
                ).ToList();
                VocabularyDataGrid.ItemsSource = filtered;
            }
        }

        /// <summary>
        /// 添加新词汇
        /// </summary>
        private async void OnAddButtonClick(object sender, RoutedEventArgs e)
        {
            if (_historyService == null) return;

            var dialog = new AddVocabularyDialog();
            if (dialog.ShowDialog() == true && dialog.Vocabulary != null)
            {
                try
                {
                    await _historyService.UpsertVocabularyAsync(dialog.Vocabulary);
                    await RefreshVocabularyAsync();
                    StatusText.Text = $"已添加: {dialog.Vocabulary.Word}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"添加失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 删除选中词汇
        /// </summary>
        private async void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            var selected = VocabularyDataGrid.SelectedItem as PersonalVocabulary;
            if (selected == null)
            {
                MessageBox.Show("请先选择一个词汇", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"确定要删除词汇 \"{selected.Word}\" 吗？",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={AppPaths.DatabasePath}");
                    connection.Open();
                    connection.Execute("DELETE FROM PersonalVocabulary WHERE Id = @Id",
                        new { Id = selected.Id.ToString() });

                    await RefreshVocabularyAsync();
                    StatusText.Text = $"已删除: {selected.Word}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 导入词典包
        /// </summary>
        private async void OnImportButtonClick(object sender, RoutedEventArgs e)
        {
            if (_aiService == null || _historyService == null) return;

            var dialog = new OpenFileDialog
            {
                Filter = "词典包 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "导入词典包"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    StatusText.Text = "正在导入...";
                    var count = await _aiService.ImportVocabularyPackageAsync(dialog.FileName);
                    await RefreshVocabularyAsync();
                    StatusText.Text = $"成功导入 {count} 个词汇";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 导出热词文件
        /// </summary>
        private async void OnExportButtonClick(object sender, RoutedEventArgs e)
        {
            if (_historyService == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "热词文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                Title = "导出热词文件",
                FileName = $"WordFlow_Hotwords_{DateTime.Now:yyyyMMdd}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    StatusText.Text = "正在导出...";
                    var path = await _historyService.ExportHotwordsFileAsync(dialog.FileName);
                    StatusText.Text = $"已导出到: {path}";

                    if (MessageBox.Show("导出成功！是否打开所在文件夹？", "完成",
                        MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 刷新按钮
        /// </summary>
        private async void OnRefreshButtonClick(object sender, RoutedEventArgs e)
        {
            await RefreshVocabularyAsync();
        }

        /// <summary>
        /// AI分析按钮
        /// </summary>
        private async void OnAIButtonClick(object sender, RoutedEventArgs e)
        {
            if (_aiService == null || _historyService == null) return;

            var result = MessageBox.Show(
                "AI分析将检查您的输入历史，智能生成专业词典。\n\n" +
                "注意：此功能需要连接到AI服务（未来可作为付费功能）。\n\n" +
                "是否继续？",
                "AI智能分析", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                AIButton.IsEnabled = false;
                StatusText.Text = "AI正在分析您的输入习惯...";

                var analysis = await _aiService.AnalyzeUserInputHistoryAsync(30);

                if (analysis.Success)
                {
                    await RefreshVocabularyAsync();
                    StatusText.Text = $"AI分析完成！生成了 {analysis.GeneratedTerms} 个专业词汇";

                    MessageBox.Show(
                        $"分析完成！\n\n" +
                        $"分析记录: {analysis.AnalyzedRecords} 条\n" +
                        $"生成词汇: {analysis.GeneratedTerms} 个\n" +
                        $"推荐分类: {analysis.SuggestedCategory}\n\n" +
                        $"洞察: {analysis.Insights}",
                        "AI分析结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = $"AI分析: {analysis.Message}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AI分析失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AIButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 智能学习按钮
        /// </summary>
        private async void OnLearnButtonClick(object sender, RoutedEventArgs e)
        {
            if (_learningEngine == null) return;

            var result = MessageBox.Show(
                "智能学习将分析您的输入历史，自动学习高频词汇和错误模式。\n\n" +
                "是否继续？",
                "智能学习", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                LearnButton.IsEnabled = false;
                StatusText.Text = "正在学习您的输入习惯...";

                var learnResult = await _learningEngine.LearnAsync(100);

                if (learnResult.Success)
                {
                    await RefreshVocabularyAsync();
                    StatusText.Text = $"学习完成！新增 {learnResult.TotalLearned} 个词汇";

                    MessageBox.Show(
                        $"学习完成！\n\n" +
                        $"从历史学习: {learnResult.LearnedFromHistory.Count} 个新词\n" +
                        $"从历史更新: {learnResult.UpdatedFromHistory.Count} 个词\n" +
                        $"从修正学习: {learnResult.LearnedFromCorrections.Count} 个词\n" +
                        $"生成纠错规则: {learnResult.GeneratedRules.Count} 条",
                        "学习结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"学习失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LearnButton.IsEnabled = true;
            }
        }

        #endregion

        #region 历史记录标签页功能

        /// <summary>
        /// 刷新历史记录
        /// </summary>
        private async Task RefreshHistoryAsync()
        {
            if (_historyService == null)
            {
                StatusText.Text = "历史服务未初始化";
                return;
            }

            try
            {
                StatusText.Text = "正在加载历史记录...";
                _allHistory = await _historyService.GetRecentHistoryAsync(500);
                _selectedHistoryIds.Clear();
                RenderHistoryList();
                StatusText.Text = $"共 {_allHistory.Count} 条历史记录";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"加载失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 渲染历史记录列表（按日期分组）
        /// </summary>
        private void RenderHistoryList()
        {
            HistoryListPanel.Children.Clear();

            // 按日期分组
            var grouped = _allHistory
                .OrderByDescending(h => h.Timestamp)
                .GroupBy(h => GetDateGroup(h.Timestamp));

            foreach (var group in grouped)
            {
                // 日期分组标题
                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 10, 0, 5)
                };
                var headerText = new TextBlock
                {
                    Text = group.Key,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
                };
                headerBorder.Child = headerText;
                HistoryListPanel.Children.Add(headerBorder);

                // 该日期的记录
                foreach (var history in group)
                {
                    var itemPanel = CreateHistoryItemPanel(history);
                    HistoryListPanel.Children.Add(itemPanel);
                }
            }
        }

        /// <summary>
        /// 创建单条历史记录UI
        /// </summary>
        private Border CreateHistoryItemPanel(InputHistory history)
        {
            var isSelected = _selectedHistoryIds.Contains(history.Id);

            var border = new Border
            {
                Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(232, 245, 253))
                    : Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 3, 0, 3),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = history.Id
            };

            // 鼠标点击事件
            border.MouseLeftButtonUp += (s, e) => ToggleHistorySelection(history.Id, border);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 复选框
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 时间
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 内容
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // 应用

            // 复选框
            var checkBox = new CheckBox
            {
                IsChecked = isSelected,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            checkBox.Checked += (s, e) => SelectHistory(history.Id);
            checkBox.Unchecked += (s, e) => DeselectHistory(history.Id);
            Grid.SetColumn(checkBox, 0);
            grid.Children.Add(checkBox);

            // 时间
            var timeText = new TextBlock
            {
                Text = history.Timestamp.ToString("HH:mm"),
                Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(timeText, 1);
            grid.Children.Add(timeText);

            // 内容区域
            var contentStack = new StackPanel();

            // 识别文本
            var textBlock = new TextBlock
            {
                Text = history.FinalText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = Brushes.Black
            };
            contentStack.Children.Add(textBlock);

            // 场景标签（如果不是通用场景）
            if (history.Scene != InputScene.General)
            {
                var tagPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
                var tagBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(225, 245, 254)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2)
                };
                var tagText = new TextBlock
                {
                    Text = GetSceneDisplayName(history.Scene),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(3, 155, 229))
                };
                tagBorder.Child = tagText;
                tagPanel.Children.Add(tagBorder);
                contentStack.Children.Add(tagPanel);
            }

            Grid.SetColumn(contentStack, 2);
            grid.Children.Add(contentStack);

            // 目标应用
            if (!string.IsNullOrEmpty(history.TargetApplication))
            {
                var appText = new TextBlock
                {
                    Text = history.TargetApplication,
                    Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                    FontSize = 11,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(appText, 3);
                grid.Children.Add(appText);
            }

            border.Child = grid;
            return border;
        }

        /// <summary>
        /// 获取日期分组名称
        /// </summary>
        private string GetDateGroup(DateTime timestamp)
        {
            var today = DateTime.Today;
            var date = timestamp.Date;

            if (date == today)
                return "今天";
            if (date == today.AddDays(-1))
                return "昨天";
            if (date > today.AddDays(-7))
                return date.ToString("dddd"); // 星期几
            return date.ToString("yyyy年MM月dd日");
        }

        /// <summary>
        /// 获取场景显示名称
        /// </summary>
        private string GetSceneDisplayName(InputScene scene)
        {
            return scene switch
            {
                InputScene.Chat => "聊天",
                InputScene.Medical => "医疗",
                InputScene.Legal => "法律",
                InputScene.Programming => "编程",
                InputScene.Business => "商务",
                InputScene.Academic => "学术",
                _ => "通用"
            };
        }

        /// <summary>
        /// 切换历史记录选择状态
        /// </summary>
        private void ToggleHistorySelection(Guid id, Border border)
        {
            if (_selectedHistoryIds.Contains(id))
            {
                DeselectHistory(id);
            }
            else
            {
                SelectHistory(id);
            }
        }

        /// <summary>
        /// 选择历史记录
        /// </summary>
        private void SelectHistory(Guid id)
        {
            if (!_selectedHistoryIds.Contains(id))
            {
                _selectedHistoryIds.Add(id);
                UpdateHistoryItemVisual(id, true);
            }
            UpdateSelectionStatus();
        }

        /// <summary>
        /// 取消选择历史记录
        /// </summary>
        private void DeselectHistory(Guid id)
        {
            if (_selectedHistoryIds.Contains(id))
            {
                _selectedHistoryIds.Remove(id);
                UpdateHistoryItemVisual(id, false);
            }
            UpdateSelectionStatus();
        }

        /// <summary>
        /// 更新历史记录项的视觉效果
        /// </summary>
        private void UpdateHistoryItemVisual(Guid id, bool isSelected)
        {
            foreach (var child in HistoryListPanel.Children)
            {
                if (child is Border border && border.Tag is Guid itemId && itemId == id)
                {
                    border.Background = isSelected
                        ? new SolidColorBrush(Color.FromRgb(232, 245, 253))
                        : Brushes.White;

                    // 更新复选框状态
                    if (border.Child is Grid grid)
                    {
                        foreach (var gridChild in grid.Children)
                        {
                            if (gridChild is CheckBox checkBox)
                            {
                                checkBox.IsChecked = isSelected;
                            }
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 更新选择状态显示
        /// </summary>
        private void UpdateSelectionStatus()
        {
            StatusText.Text = $"已选择 {_selectedHistoryIds.Count} 条记录";
        }

        /// <summary>
        /// 全选历史记录
        /// </summary>
        private void OnSelectAllHistoryClick(object sender, RoutedEventArgs e)
        {
            _selectedHistoryIds = _allHistory.Select(h => h.Id).ToList();
            RenderHistoryList();
            UpdateSelectionStatus();
        }

        /// <summary>
        /// 取消全选
        /// </summary>
        private void OnSelectNoneHistoryClick(object sender, RoutedEventArgs e)
        {
            _selectedHistoryIds.Clear();
            RenderHistoryList();
            UpdateSelectionStatus();
        }

        /// <summary>
        /// 训练选中项
        /// </summary>
        private async void OnTrainSelectedClick(object sender, RoutedEventArgs e)
        {
            if (_learningEngine == null || _historyService == null) return;
            if (_selectedHistoryIds.Count == 0)
            {
                MessageBox.Show("请先选择要训练的历史记录", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                StatusText.Text = $"正在训练 {_selectedHistoryIds.Count} 条记录...";

                // 标记选中的记录为待训练
                foreach (var id in _selectedHistoryIds)
                {
                    await _historyService.MarkAsTrainedAsync(id);
                }

                // 执行学习
                var result = await _learningEngine.LearnAsync(_selectedHistoryIds.Count);

                if (result.Success)
                {
                    await RefreshVocabularyAsync();
                    MessageBox.Show(
                        $"训练完成！\n\n" +
                        $"学习新词: {result.LearnedFromHistory.Count} 个\n" +
                        $"更新词汇: {result.UpdatedFromHistory.Count} 个",
                        "训练结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"训练失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 自动训练全部
        /// </summary>
        private async void OnAutoTrainClick(object sender, RoutedEventArgs e)
        {
            if (_learningEngine == null) return;

            var result = MessageBox.Show(
                $"将对全部 {_allHistory.Count} 条未训练记录进行自动学习。\n\n" +
                "是否继续？",
                "自动训练", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                StatusText.Text = "正在自动训练...";
                var learnResult = await _learningEngine.LearnAsync(500);

                if (learnResult.Success)
                {
                    await RefreshVocabularyAsync();
                    await RefreshHistoryAsync();
                    MessageBox.Show(
                        $"自动训练完成！\n\n" +
                        $"新增词汇: {learnResult.TotalLearned} 个",
                        "训练结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"训练失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除选中的历史记录
        /// </summary>
        private async void OnDeleteHistoryClick(object sender, RoutedEventArgs e)
        {
            if (_historyService == null) return;
            if (_selectedHistoryIds.Count == 0)
            {
                MessageBox.Show("请先选择要删除的记录", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除选中的 {_selectedHistoryIds.Count} 条记录吗？\n\n" +
                "此操作不可恢复！",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var connection = new SqliteConnection($"Data Source={AppPaths.DatabasePath}");
                connection.Open();

                foreach (var id in _selectedHistoryIds)
                {
                    connection.Execute("DELETE FROM InputHistory WHERE Id = @Id",
                        new { Id = id.ToString() });
                }

                _selectedHistoryIds.Clear();
                await RefreshHistoryAsync();
                StatusText.Text = "删除成功";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 历史记录筛选
        /// </summary>
        private async void OnHistoryFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_historyService == null) return;

            var filter = (HistoryFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            var cutoffDate = filter switch
            {
                "今天" => DateTime.Today,
                "最近7天" => DateTime.Today.AddDays(-7),
                "最近30天" => DateTime.Today.AddDays(-30),
                _ => DateTime.MinValue
            };

            try
            {
                StatusText.Text = "正在筛选...";
                _allHistory = await _historyService.GetRecentHistoryAsync(500);

                if (cutoffDate > DateTime.MinValue)
                {
                    _allHistory = _allHistory.Where(h => h.Timestamp >= cutoffDate).ToList();
                }

                _selectedHistoryIds.Clear();
                RenderHistoryList();
                StatusText.Text = $"共 {_allHistory.Count} 条记录";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"筛选失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 搜索历史记录
        /// </summary>
        private void OnHistorySearchChanged(object sender, TextChangedEventArgs e)
        {
            var keyword = HistorySearchBox.Text?.ToLower() ?? "";

            if (string.IsNullOrEmpty(keyword))
            {
                RenderHistoryList();
                return;
            }

            // 过滤并重新渲染
            var filtered = _allHistory.Where(h =>
                h.FinalText?.ToLower().Contains(keyword) == true ||
                h.TargetApplication?.ToLower().Contains(keyword) == true
            ).ToList();

            // 临时替换列表并渲染
            var originalList = _allHistory;
            _allHistory = filtered;
            RenderHistoryList();
            _allHistory = originalList;

            StatusText.Text = $"找到 {filtered.Count} 条匹配记录";
        }

        #endregion
    }

    #region 值转换器

    /// <summary>
    /// 上下文列表转字符串显示
    /// </summary>
    public class ContextsToStringConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is List<string> contexts && contexts.Count > 0)
            {
                return contexts[0]; // 显示第一个上下文
            }
            return ""; // 空列表时显示空字符串
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 分类转中文显示
    /// </summary>
    public class CategoryToStringConverter : System.Windows.Data.IValueConverter
    {
        private static readonly Dictionary<VocabularyCategory, string> DisplayNames = new()
        {
            [VocabularyCategory.General] = "通用",
            [VocabularyCategory.Medical] = "医疗",
            [VocabularyCategory.Legal] = "法律",
            [VocabularyCategory.Programming] = "编程",
            [VocabularyCategory.Business] = "商务",
            [VocabularyCategory.Academic] = "学术",
            [VocabularyCategory.Name] = "人名",
            [VocabularyCategory.Place] = "地名",
            [VocabularyCategory.Organization] = "组织",
            [VocabularyCategory.Product] = "产品"
        };

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is VocabularyCategory category)
            {
                return DisplayNames.GetValueOrDefault(category, category.ToString());
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 来源转中文显示
    /// </summary>
    public class SourceToStringConverter : System.Windows.Data.IValueConverter
    {
        private static readonly Dictionary<VocabularySource, string> DisplayNames = new()
        {
            [VocabularySource.AutoLearned] = "自动学习",
            [VocabularySource.Manual] = "手动添加",
            [VocabularySource.Imported] = "导入",
            [VocabularySource.AIGenerated] = "AI生成"
        };

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is VocabularySource source)
            {
                return DisplayNames.GetValueOrDefault(source, source.ToString());
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion

    #region 添加词汇对话框

    /// <summary>
    /// 添加词汇对话框
    /// </summary>
    public partial class AddVocabularyDialog : Window
    {
        /// <summary>
        /// 分类显示名称映射
        /// </summary>
        private static readonly Dictionary<VocabularyCategory, string> CategoryDisplayNames = new()
        {
            [VocabularyCategory.General] = "通用词汇",
            [VocabularyCategory.Medical] = "医疗/医学",
            [VocabularyCategory.Legal] = "法律/法务",
            [VocabularyCategory.Programming] = "编程/技术",
            [VocabularyCategory.Business] = "商务/商业",
            [VocabularyCategory.Academic] = "学术/教育",
            [VocabularyCategory.Name] = "人名",
            [VocabularyCategory.Place] = "地名",
            [VocabularyCategory.Organization] = "组织/机构",
            [VocabularyCategory.Product] = "产品名称"
        };

        /// <summary>
        /// 权重说明映射
        /// </summary>
        private static readonly Dictionary<double, string> WeightDescriptions = new()
        {
            [1.0] = "1.0 - 普通词汇",
            [1.5] = "1.5 - 常用词汇",
            [2.0] = "2.0 - 重要词汇",
            [2.5] = "2.5 - 专业术语",
            [3.0] = "3.0 - 核心词汇"
        };

        public PersonalVocabulary? Vocabulary { get; private set; }

        public AddVocabularyDialog()
        {
            Title = "添加新词汇";
            Width = 420;
            Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;

            var mainStack = new StackPanel { Margin = new Thickness(20) };

            // 说明文字
            var descText = new TextBlock 
            { 
                Text = "将常用词汇添加到个人词典，可提高语音识别准确率",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            };
            mainStack.Children.Add(descText);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 词汇输入
            var wordLabel = new TextBlock { Text = "词汇：", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) };
            var wordBox = new TextBox { 
                Margin = new Thickness(0, 0, 0, 12), 
                Padding = new Thickness(5),
                ToolTip = "输入您常用的词汇，如专业术语、人名、地名等"
            };
            Grid.SetRow(wordLabel, 0);
            Grid.SetRow(wordBox, 0);
            grid.Children.Add(wordLabel);
            grid.Children.Add(wordBox);

            // 拼音输入
            var pinyinLabel = new TextBlock { Text = "拼音（可选）：", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) };
            var pinyinPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var pinyinBox = new TextBox { 
                Padding = new Thickness(5),
                ToolTip = "如果不填写，系统会自动生成拼音"
            };
            var pinyinHint = new TextBlock { 
                Text = "如不填写，系统会自动生成拼音", 
                FontSize = 10, 
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            pinyinPanel.Children.Add(pinyinBox);
            pinyinPanel.Children.Add(pinyinHint);
            Grid.SetRow(pinyinLabel, 1);
            Grid.SetRow(pinyinPanel, 1);
            grid.Children.Add(pinyinLabel);
            grid.Children.Add(pinyinPanel);

            // 分类选择
            var catLabel = new TextBlock { Text = "分类：", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) };
            var catCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(5) };
            
            foreach (VocabularyCategory cat in Enum.GetValues(typeof(VocabularyCategory)))
            {
                catCombo.Items.Add(new 
                { 
                    Category = cat, 
                    DisplayName = CategoryDisplayNames.GetValueOrDefault(cat, cat.ToString()) 
                });
            }
            catCombo.DisplayMemberPath = "DisplayName";
            catCombo.SelectedValuePath = "Category";
            catCombo.SelectedIndex = 0;
            catCombo.ToolTip = "选择词汇的分类，有助于系统更好地理解和识别";
            
            Grid.SetRow(catLabel, 2);
            Grid.SetRow(catCombo, 2);
            grid.Children.Add(catLabel);
            grid.Children.Add(catCombo);

            // 权重选择
            var weightLabel = new TextBlock { Text = "优先级：", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) };
            var weightPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var weightCombo = new ComboBox { Padding = new Thickness(5) };
            
            foreach (var kvp in WeightDescriptions)
            {
                weightCombo.Items.Add(kvp.Value);
            }
            weightCombo.SelectedIndex = 1; // 默认选中 1.5
            weightCombo.ToolTip = "优先级越高，语音识别时越容易被优先匹配";
            
            var weightHint = new TextBlock { 
                Text = "优先级越高，语音识别时越容易被优先匹配", 
                FontSize = 10, 
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            weightPanel.Children.Add(weightCombo);
            weightPanel.Children.Add(weightHint);
            
            Grid.SetRow(weightLabel, 3);
            Grid.SetRow(weightPanel, 3);
            grid.Children.Add(weightLabel);
            grid.Children.Add(weightPanel);

            // 按钮
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            var cancelBtn = new Button { 
                Content = "取消", 
                Padding = new Thickness(20, 6, 20, 6), 
                Margin = new Thickness(5, 0, 0, 0),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204))
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
            
            var okBtn = new Button { 
                Content = "确定", 
                Padding = new Thickness(20, 6, 20, 6), 
                Margin = new Thickness(10, 0, 0, 0), 
                IsDefault = true,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            okBtn.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(wordBox.Text))
                {
                    MessageBox.Show("请输入词汇", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 解析权重
                var weightStr = weightCombo.SelectedItem?.ToString() ?? "1.5";
                var weight = 1.5;
                if (weightStr.Contains("1.0")) weight = 1.0;
                else if (weightStr.Contains("1.5")) weight = 1.5;
                else if (weightStr.Contains("2.0")) weight = 2.0;
                else if (weightStr.Contains("2.5")) weight = 2.5;
                else if (weightStr.Contains("3.0")) weight = 3.0;

                var selectedCategory = ((dynamic)catCombo.SelectedItem).Category;

                Vocabulary = new PersonalVocabulary
                {
                    Word = wordBox.Text.Trim(),
                    Pinyin = string.IsNullOrWhiteSpace(pinyinBox.Text) 
                        ? TinyPinyin.PinyinHelper.GetPinyin(wordBox.Text.Trim(), " ").ToLower()
                        : pinyinBox.Text.Trim(),
                    Category = (VocabularyCategory)selectedCategory,
                    Weight = weight,
                    Source = VocabularySource.Manual,
                    FirstUsed = DateTime.Now,
                    LastUsed = DateTime.Now,
                    Frequency = 1,
                    Contexts = new List<string>(),
                    ConfusableWords = new List<string>(),
                    RelatedHistoryIds = new List<Guid>()
                };
                DialogResult = true;
                Close();
            };
            buttonPanel.Children.Add(cancelBtn);
            buttonPanel.Children.Add(okBtn);
            Grid.SetRow(buttonPanel, 4);
            grid.Children.Add(buttonPanel);

            mainStack.Children.Add(grid);
            Content = mainStack;
        }
    }

    #endregion
}
