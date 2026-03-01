using System;
using System.Text;
using System.Text.RegularExpressions;

namespace WordFlow.Services
{
    /// <summary>
    /// 文本后处理器 - 智能处理识别结果
    /// 1. 自动识别列表逻辑（一、二、三 → 1、2、3）
    /// 2. 自动处理换行格式
    /// 3. 智能识别待办事项意图并格式化
    /// </summary>
    public static class TextPostProcessor
    {
        /// <summary>
        /// 处理文本 - 应用所有后处理规则
        /// </summary>
        public static string Process(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var result = text;

            // 1. 智能待办事项识别（优先处理）
            result = ProcessTodoList(result);

            // 2. 处理中文数字列表
            result = ProcessChineseNumberedList(result);

            // 3. 处理换行格式
            result = ProcessLineBreaks(result);

            // 4. 处理标点符号
            result = ProcessPunctuation(result);

            // 5. 处理空格
            result = ProcessSpaces(result);

            return result;
        }

        /// <summary>
        /// 智能待办事项识别
        /// 识别"待办"、"清单"、"我要做"等意图，自动格式化为复选框列表
        /// </summary>
        private static string ProcessTodoList(string text)
        {
            // 待办事项关键词
            var todoKeywords = new[]
            {
                "待办", "代办", "要做", "要做的事情", "清单", "列表",
                "今天要做", "明天要做", "记得", "别忘了", "记住",
                "任务", "计划", "安排", "工作"
            };

            // 检查是否包含待办关键词
            bool isTodoContext = false;
            foreach (var keyword in todoKeywords)
            {
                if (text.Contains(keyword))
                {
                    isTodoContext = true;
                    break;
                }
            }

            if (!isTodoContext)
                return text;

            // 检测到待办上下文，开始格式化
            var result = text;

            // 1. 如果开头有待办关键词，添加标题
            foreach (var keyword in todoKeywords)
            {
                if (result.StartsWith(keyword))
                {
                    result = "待办事项：\n" + result;
                    break;
                }
            }

            // 2. 识别序号模式（第一、第二、第三 或 一、二、三）
            // 将"第一 xxx"、"第二 xxx"转换为"[ ] xxx"
            result = Regex.Replace(result, @"[第]?[一二三四五六七八九十]+[、.]\s*", "[ ] ");

            // 3. 识别"然后"、"还有"、"另外"等连接词，在它们前面添加换行和复选框
            result = Regex.Replace(result, @"[，,]\s*(然后 | 还有 | 另外 | 再 | 接着)\s*", "\n[ ] ");

            // 4. 如果文本中有多个分句（用逗号分隔），每个分句作为单独的待办项
            // 检测是否有多项内容（至少两个逗号）
            int commaCount = 0;
            foreach (var c in result)
            {
                if (c == '，' || c == ',') commaCount++;
            }
            if (commaCount >= 2)
            {
                // 在逗号后添加换行和复选框（如果后面还有内容）
                result = Regex.Replace(result, @"[，,]\s*(?=[^\n])", "\n[ ] ");
            }

            // 5. 确保每个非空行都以复选框开头
            var lines = result.Split('\n');
            var processedLines = new System.Collections.Generic.List<string>();
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // 跳过已经是复选框的行和标题行
                if (trimmed.StartsWith("[ ]") || trimmed.StartsWith("[x]") || trimmed.EndsWith("："))
                {
                    processedLines.Add(line);
                    continue;
                }

                // 给普通内容添加复选框
                processedLines.Add("[ ] " + trimmed);
            }

            return string.Join("\n", processedLines);
        }

        /// <summary>
        /// 处理中文数字列表
        /// 将"一、" "二、" "三、"等转换为"1." "2." "3."
        /// </summary>
        private static string ProcessChineseNumberedList(string text)
        {
            // 中文数字映射
            var chineseToArabic = new System.Collections.Generic.Dictionary<string, int>
            {
                { "一", 1 }, { "二", 2 }, { "三", 3 }, { "四", 4 }, { "五", 5 },
                { "六", 6 }, { "七", 7 }, { "八", 8 }, { "九", 9 }, { "十", 10 },
                { "十一", 11 }, { "十二", 12 }, { "十三", 13 }, { "十四", 14 }, { "十五", 15 },
                { "十六", 16 }, { "十七", 17 }, { "十八", 18 }, { "十九", 19 }, { "二十", 20 }
            };

            // 匹配模式：中文数字 + "、" 或 "."
            var pattern = @"([一二三四五六七八九十]+)[、.．]";
            
            var result = new StringBuilder();
            var lastEnd = 0;
            var expectedNextNumber = 1;
            var isListContext = false;

            var matches = Regex.Matches(text, pattern);
            
            foreach (Match match in matches)
            {
                var chineseNum = match.Groups[1].Value;
                
                if (chineseToArabic.TryGetValue(chineseNum, out int arabicNum))
                {
                    // 检查是否是连续的列表
                    if (arabicNum == expectedNextNumber || !isListContext)
                    {
                        // 添加到结果
                        result.Append(text.Substring(lastEnd, match.Index - lastEnd));
                        result.Append($"{arabicNum}.");
                        lastEnd = match.Index + match.Length;
                        expectedNextNumber = arabicNum + 1;
                        isListContext = true;
                    }
                    else
                    {
                        // 不是连续列表，保持原样
                        expectedNextNumber = arabicNum + 1;
                    }
                }
            }

            // 添加剩余文本
            if (lastEnd < text.Length)
            {
                result.Append(text.Substring(lastEnd));
            }

            return result.Length > 0 ? result.ToString() : text;
        }

        /// <summary>
        /// 处理换行格式
        /// 1. 句号、问号、感叹号后自动换行
        /// 2. 分号后可选换行
        /// </summary>
        private static string ProcessLineBreaks(string text)
        {
            var result = text;

            // 在句号、问号、感叹号后添加换行（如果后面还有内容）
            // 但要避免在引号内换行
            result = Regex.Replace(result, @"([。！？!?])(?=[^""'」』】])", "$1\n");

            // 清理多余的空白行
            result = Regex.Replace(result, @"\n\s*\n", "\n");

            return result;
        }

        /// <summary>
        /// 处理标点符号
        /// 1. 统一标点符号格式
        /// 2. 修复常见标点错误
        /// </summary>
        private static string ProcessPunctuation(string text)
        {
            var result = text;

            // 统一省略号
            result = result.Replace("...", "……");
            result = result.Replace("．．．", "……");

            // 统一破折号
            result = result.Replace("--", "——");
            result = result.Replace("——", "——");

            // 修复重复标点
            result = Regex.Replace(result, @"([。！？!?])\1+", "$1");
            result = Regex.Replace(result, @"([，,])\1+", "$1");

            return result;
        }

        /// <summary>
        /// 处理空格
        /// 1. 中英文混排时添加适当空格
        /// 2. 移除多余空格
        /// </summary>
        private static string ProcessSpaces(string text)
        {
            var result = text;

            // 中文和英文/数字之间添加空格
            result = Regex.Replace(result, @"([\u4e00-\u9fa5])([a-zA-Z0-9])", "$1 $2");
            result = Regex.Replace(result, @"([a-zA-Z0-9])([\u4e00-\u9fa5])", "$1 $2");

            // 移除行首行尾空格
            var lines = result.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
            }
            result = string.Join("\n", lines);

            // 移除标点前的空格
            result = Regex.Replace(result, @" +([,.!?.!?])", "$1");

            return result;
        }
    }
}
