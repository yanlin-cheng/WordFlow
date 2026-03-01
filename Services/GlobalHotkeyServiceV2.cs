using System;
using System.Runtime.InteropServices;
using WordFlow.Infrastructure;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// 全局热键服务 V2 - 完全独立于 UI
    /// 使用事件总线通信，不依赖任何 Dispatcher
    /// </summary>
    public class GlobalHotkeyServiceV2 : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private IntPtr _hookId;
        private readonly LowLevelKeyboardProc _proc;
        private bool _isRecordingKeyPressed = false;
        private int _hotkeyCode;

        /// <summary>
        /// 当前热键键码
        /// </summary>
        public int HotkeyCode
        {
            get => _hotkeyCode;
            set
            {
                if (_hotkeyCode != value)
                {
                    _hotkeyCode = value;
                    Logger.Log($"热键已更改为: {GetKeyName(value)}");
                }
            }
        }

        // 0xC0 = VK_OEM_3 = 波浪线键（ESC 下面、数字 1 左边的 `~ 键）
        // 0xA5 = VK_RMENU = 右 Alt
        public GlobalHotkeyServiceV2(int hotkeyCode = 0xC0)
        {
            _hotkeyCode = hotkeyCode;
            _proc = HookCallback;
            _hookId = SetHook(_proc);
            Logger.Log($"全局热键服务 V2 已启动：按住 {GetKeyName(hotkeyCode)} 说话");
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            // 对于全局低级键盘钩子，使用 GetModuleHandle(null) 获取当前进程模块
            // 这比通过 Process.MainModule 更可靠，尤其在窗口最小化时
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(null), 0);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

                if (vkCode == _hotkeyCode)
                {
                    if (isKeyDown && !_isRecordingKeyPressed)
                    {
                        _isRecordingKeyPressed = true;
                        Logger.Log($"热键按下：{GetKeyName(_hotkeyCode)} (vkCode={vkCode})");
                        OnRecordingKeyPressed();
                    }
                    else if (isKeyUp && _isRecordingKeyPressed)
                    {
                        _isRecordingKeyPressed = false;
                        Logger.Log($"热键释放：{GetKeyName(_hotkeyCode)} (vkCode={vkCode})");
                        OnRecordingKeyReleased();
                    }

                    // 对于 `~ 键和右 Alt 键，拦截消息防止系统处理
                    // 这样可以避免焦点丢失
                    if (_hotkeyCode == 0xA5 || _hotkeyCode == 0xC0) // VK_RMENU 或 VK_OEM_3
                    {
                        return new IntPtr(1); // 拦截消息
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// 热键按下 - 发布事件，不直接调用 UI
        /// </summary>
        private void OnRecordingKeyPressed()
        {
            Logger.Log($"热键按下: {GetKeyName(_hotkeyCode)}");

            // 获取当前焦点窗口（在钩子线程中执行）
            var targetWindow = GetForegroundWindow();

            // 发布事件，不依赖任何 UI 线程
            EventBus.Publish(new RecordingStartedEvent
            {
                TargetWindow = targetWindow
            });
        }

        /// <summary>
        /// 热键释放 - 发布事件
        /// </summary>
        private void OnRecordingKeyReleased()
        {
            Logger.Log($"热键释放：{GetKeyName(_hotkeyCode)}");

            EventBus.Publish(new RecordingStoppedEvent());
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            Logger.Log("全局热键服务 V2 已停止");
        }

        /// <summary>
        /// 获取键的友好名称
        /// </summary>
        private static string GetKeyName(int vkCode)
        {
            return vkCode switch
            {
                0xC0 => "` 键",  // ESC 下面、数字 1 左边的波浪线键（推荐）
                0xA5 => "右 Alt",
                0x14 => "CapsLock",  // 已废弃，仅保留显示
                _ => $"键码 {vkCode}"
            };
        }

        #region Windows API

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion
    }
}
