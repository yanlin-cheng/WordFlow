using System;
using System.Collections.Generic;
using System.Threading;
using WordFlow.Utils;

namespace WordFlow.Infrastructure
{
    /// <summary>
    /// 事件总线 - 解耦服务与 UI 的通信
    /// 使用强引用确保处理器不会被垃圾回收
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Action<object>>> _handlers = new();
        private static readonly ReaderWriterLockSlim _lock = new();

        /// <summary>
        /// 订阅事件
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : class
        {
            var wrappedHandler = new Action<object>(obj =>
            {
                if (obj is T typedObj)
                {
                    handler(typedObj);
                }
            });

            _lock.EnterWriteLock();
            try
            {
                if (!_handlers.TryGetValue(typeof(T), out var list))
                {
                    list = new List<Action<object>>();
                    _handlers[typeof(T)] = list;
                }
                list.Add(wrappedHandler);
                Logger.Log($"EventBus: 已订阅 {typeof(T).Name}，当前共 {list.Count} 个处理器");
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 发布事件（不依赖任何 UI 线程）
        /// </summary>
        public static void Publish<T>(T eventData) where T : class
        {
            List<Action<object>> handlersToInvoke = new();
            
            _lock.EnterReadLock();
            try
            {
                if (_handlers.TryGetValue(typeof(T), out var list))
                {
                    handlersToInvoke.AddRange(list);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            Logger.Log($"EventBus: 发布 {typeof(T).Name}，共 {handlersToInvoke.Count} 个处理器");

            // 在调用链外部执行，不持有锁
            foreach (var handler in handlersToInvoke)
            {
                try
                {
                    handler(eventData);
                }
                catch (Exception ex)
                {
                    Logger.Log($"EventBus: 事件处理异常 - {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"事件处理异常：{ex.Message}");
                }
            }
        }
    }

    #region 事件定义

    /// <summary>
    /// 录音开始事件
    /// </summary>
    public class RecordingStartedEvent
    {
        public IntPtr TargetWindow { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 录音结束事件
    /// </summary>
    public class RecordingStoppedEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 模型缺失事件
    /// </summary>
    public class ModelNeededEvent
    {
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 设置变更事件
    /// </summary>
    public class SettingsChangedEvent
    {
        public string ChangedProperty { get; set; } = "";
        public object? NewValue { get; set; }
    }

    /// <summary>
    /// 识别完成事件
    /// </summary>
    public class RecognitionCompletedEvent
    {
        public string Text { get; set; } = "";
        public IntPtr TargetWindow { get; set; }
        public string? TargetWindowTitle { get; set; }
    }

    /// <summary>
    /// 状态更新事件
    /// </summary>
    public class StatusChangedEvent
    {
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 请求显示主窗口事件
    /// </summary>
    public class ShowMainWindowRequest { }

    /// <summary>
    /// 请求退出应用事件
    /// </summary>
    public class ExitApplicationRequest { }

    #endregion
}
