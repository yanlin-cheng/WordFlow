using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WordFlow.Utils;

namespace WordFlow.Services
{
    /// <summary>
    /// 全局热键服务 - 支持自定义热键
    /// </summary>
    public class GlobalHotkeyService : IDisposable
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

        public event EventHandler? RecordingKeyPressed;
        public event EventHandler? RecordingKeyReleased;

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
                    Logger.Log($"热键已更改为: {SettingsService.GetKeyName(value)}");
                }
            }
        }

        public GlobalHotkeyService(int hotkeyCode = 0x14)
        {
            _hotkeyCode = hotkeyCode;
            _proc = HookCallback;
            _hookId = SetHook(_proc);
            Logger.Log($"全局热键服务已启动：按住 {SettingsService.GetKeyName(hotkeyCode)} 说话");
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule?.ModuleName ?? ""), 0);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

                // 检查是否是配置的热键
                if (vkCode == _hotkeyCode)
                {
                    if (isKeyDown && !_isRecordingKeyPressed)
                    {
                        _isRecordingKeyPressed = true;
                        Logger.Log($"热键按下: {SettingsService.GetKeyName(vkCode)}");
                        RecordingKeyPressed?.Invoke(this, EventArgs.Empty);
                    }
                    else if (isKeyUp && _isRecordingKeyPressed)
                    {
                        _isRecordingKeyPressed = false;
                        Logger.Log($"热键释放: {SettingsService.GetKeyName(vkCode)}");
                        RecordingKeyReleased?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            Logger.Log("全局热键服务已停止");
        }

        #region Windows API

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
