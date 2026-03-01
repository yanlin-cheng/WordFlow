using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace WordFlow.Utils
{
    /// <summary>
    /// 虚拟键码枚举（不依赖 System.Windows.Forms）
    /// </summary>
    public enum Keys
    {
        None = 0,
        Back = 8,
        Tab = 9,
        Enter = 13,
        Shift = 16,
        Control = 17,
        Alt = 18,
        Escape = 27,
        Space = 32,
        PageUp = 33,
        PageDown = 34,
        End = 35,
        Home = 36,
        Left = 37,
        Up = 38,
        Right = 39,
        Down = 40,
        Delete = 46,
        D0 = 48,
        D1 = 49,
        D2 = 50,
        D3 = 51,
        D4 = 52,
        D5 = 53,
        D6 = 54,
        D7 = 55,
        D8 = 56,
        D9 = 57,
        A = 65,
        B = 66,
        C = 67,
        V = 86,
        X = 88,
        Z = 90,
        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123
    }

    /// <summary>
    /// 键盘模拟器 - 使用 SendInput API 模拟键盘输入
    /// </summary>
    public static class KeyboardSimulator
    {
        #region Windows API

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion

        /// <summary>
        /// 发送文本到当前光标位置
        /// </summary>
        /// <param name="text">要发送的文本</param>
        /// <param name="addSpaceAfter">是否在文本后添加空格</param>
        public static void SendText(string text, bool addSpaceAfter = true)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // 稍微延迟，让用户有时间松开按键
            Thread.Sleep(200);

            // 尝试使用剪贴板+粘贴方式（更可靠）
            try
            {
                SendTextViaClipboard(text, addSpaceAfter);
            }
            catch (Exception ex)
            {
                Logger.Log($"剪贴板方式失败，回退到 SendInput: {ex.Message}");
                // 使用 Unicode 输入方式
                SendUnicodeText(text);
                if (addSpaceAfter)
                {
                    Thread.Sleep(50);
                    SendKey(Keys.Space);
                }
            }
        }

        /// <summary>
        /// 剪贴板操作需要在 STA 线程执行，这里只负责发送键盘事件
        /// 剪贴板设置由调用方在主线程完成
        /// </summary>
        private static void SendTextViaClipboard(string text, bool addSpaceAfter)
        {
            Logger.Log("剪贴板方案：发送 Ctrl+V 粘贴");
            
            // 先点击一下目标窗口，确保焦点正确
            Thread.Sleep(50);
            
            // 发送 Ctrl+V 粘贴（在后台线程发送键盘事件没问题）
            SendKeyCombo(Keys.Control, Keys.V);
            Thread.Sleep(200);
            
            // 可选：添加空格
            if (addSpaceAfter)
            {
                SendKey(Keys.Space);
                Thread.Sleep(100);
            }

            Logger.Log("剪贴板方案：键盘事件发送完成");
        }

        /// <summary>
        /// 使用 Unicode 输入方式发送文本（支持中文）
        /// </summary>
        public static void SendUnicodeText(string text)
        {
            var inputs = new List<INPUT>();

            foreach (char c in text)
            {
                // 按下键
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                });

                // 释放键
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                });
            }

            // 批量发送输入事件
            if (inputs.Count > 0)
            {
                SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            }
        }

        /// <summary>
        /// 发送单个按键
        /// </summary>
        public static void SendKey(Keys key)
        {
            var inputs = new INPUT[2];

            // 按下
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            // 释放
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// 发送组合键（如 Ctrl+V）
        /// </summary>
        public static void SendKeyCombo(params Keys[] keys)
        {
            if (keys == null || keys.Length == 0)
                return;

            Logger.Log($"SendKeyCombo: 发送按键 - {string.Join("+", keys)}");

            var inputs = new List<INPUT>();

            // 按下所有键
            foreach (var key in keys)
            {
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                });
            }

            // 反向释放所有键
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)keys[i],
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                });
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// 测试键盘模拟是否正常工作
        /// </summary>
        public static void Test()
        {
            // 等待 2 秒让用户切换窗口
            Thread.Sleep(2000);
            SendText("这是一段测试文本，来自 WordFlow 语音输入。");
        }
    }
}
