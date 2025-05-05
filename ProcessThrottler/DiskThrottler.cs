using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProcessThrottler
{
    /// <summary>
    /// 磁盘IO限制器，提供限制进程磁盘读写速度的静态方法
    /// </summary>
    public static class DiskThrottler
    {
        // 进程ID到IO监控信息的映射
        private static readonly Dictionary<int, ProcessIOInfo> _processIOMap = new Dictionary<int, ProcessIOInfo>();
        // 监控线程
        private static Thread _monitorThread;
        // 线程同步锁
        private static readonly object _lockObj = new object();
        // 线程停止标志
        private static bool _stopMonitoring = false;
        // 是否已启动监控
        private static bool _isMonitoringStarted = false;

        /// <summary>
        /// 进程IO监控信息类
        /// </summary>
        private class ProcessIOInfo
        {
            public Process Process { get; set; }
            public string ProcessName { get; set; }
            public double LimitMBPerSecond { get; set; }
            public double CurrentMBPerSecond { get; set; }
            public IO_COUNTERS LastCounters { get; set; }
            public DateTime LastCheckTime { get; set; }

            public ProcessIOInfo(Process process, string processName, double limitMBPerSecond)
            {
                Process = process;
                ProcessName = processName;
                LimitMBPerSecond = limitMBPerSecond;
                CurrentMBPerSecond = 0;
                LastCounters = new IO_COUNTERS();
                LastCheckTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 启动磁盘IO监控线程
        /// </summary>
        private static void StartMonitoring()
        {
            lock (_lockObj)
            {
                if (_isMonitoringStarted)
                    return;

                _stopMonitoring = false;
                _monitorThread = new Thread(MonitorLoop)
                {
                    IsBackground = true,
                    Name = "DiskThrottler_Monitor"
                };
                _monitorThread.Start();
                _isMonitoringStarted = true;
                Console.WriteLine("磁盘IO监控线程已启动");
            }
        }

        /// <summary>
        /// 停止磁盘IO监控线程
        /// </summary>
        private static void StopMonitoring()
        {
            lock (_lockObj)
            {
                if (!_isMonitoringStarted)
                    return;

                _stopMonitoring = true;
                if (_monitorThread != null && _monitorThread.IsAlive)
                {
                    // 等待线程结束，但最多等待3秒
                    _monitorThread.Join(3000);
                    if (_monitorThread.IsAlive)
                    {
                        Console.WriteLine("磁盘IO监控线程未能正常停止，将强制终止");
                        try
                        {
                            _monitorThread.Abort();
                        }
                        catch
                        {
                            // 忽略线程终止异常
                        }
                    }
                }
                _isMonitoringStarted = false;
                Console.WriteLine("磁盘IO监控线程已停止");
            }
        }

        /// <summary>
        /// 注册一个进程到IO监控
        /// </summary>
        private static void RegisterProcess(Process process, string processName, double limitMBPerSecond)
        {
            lock (_lockObj)
            {
                if (process == null)
                    return;

                int processId = process.Id;
                // 如果已存在，更新限制值
                if (_processIOMap.ContainsKey(processId))
                {
                    _processIOMap[processId].LimitMBPerSecond = limitMBPerSecond;
                    Console.WriteLine($"更新进程 {processName} (ID: {processId}) 的磁盘IO限制为 {limitMBPerSecond} MB/s");
                }
                else
                {
                    // 添加新进程
                    _processIOMap[processId] = new ProcessIOInfo(process, processName, limitMBPerSecond);
                    Console.WriteLine($"已注册进程 {processName} (ID: {processId}) 到磁盘IO监控");
                }

                // 如果这是第一个进程，启动监控线程
                if (_processIOMap.Count == 1 && !_isMonitoringStarted)
                {
                    StartMonitoring();
                }
            }
        }

        /// <summary>
        /// 注销一个进程的IO监控
        /// </summary>
        public static void UnregisterProcess(int processId)
        {
            lock (_lockObj)
            {
                if (_processIOMap.ContainsKey(processId))
                {
                    _processIOMap.Remove(processId);
                    Console.WriteLine($"已从磁盘IO监控中移除进程ID: {processId}");

                    // 如果没有进程了，停止监控线程
                    if (_processIOMap.Count == 0 && _isMonitoringStarted)
                    {
                        StopMonitoring();
                    }
                }
            }
        }

        /// <summary>
        /// 主监控循环，检查并限制所有注册进程的磁盘IO
        /// </summary>
        private static void MonitorLoop()
        {
            while (!_stopMonitoring)
            {
                try
                {
                    // 获取所有注册的进程的快照
                    List<ProcessIOInfo> activeProcesses;
                    lock (_lockObj)
                    {
                        activeProcesses = _processIOMap.Values.ToList();
                    }

                    // 检查每个进程的磁盘IO使用情况
                    foreach (var processInfo in activeProcesses)
                    {
                        try
                        {
                            // 获取进程和ID
                            Process process = processInfo.Process;
                            int processId = process.Id;
                            
                            // 计算经过的时间
                            DateTime now = DateTime.Now;
                            double elapsedSeconds = (now - processInfo.LastCheckTime).TotalSeconds;
                            
                            // 限制至少需要经过0.1秒才计算速率
                            if (elapsedSeconds < 0.1)
                                continue;
                                
                            processInfo.LastCheckTime = now;

                            // 获取当前IO计数
                            IO_COUNTERS currentCounters = GetIOCounters(process.Handle);

                            // 计算IO速率
                            if (processInfo.LastCounters.ReadTransferCount > 0 || processInfo.LastCounters.WriteTransferCount > 0)
                            {
                                // 计算读取速率（字节/秒）
                                ulong readBytesPerSecond = (currentCounters.ReadTransferCount - processInfo.LastCounters.ReadTransferCount) / (ulong)elapsedSeconds;
                                // 计算写入速率（字节/秒）
                                ulong writeBytesPerSecond = (currentCounters.WriteTransferCount - processInfo.LastCounters.WriteTransferCount) / (ulong)elapsedSeconds;
                                // 总速率（MB/秒）
                                double totalMBPerSecond = (readBytesPerSecond + writeBytesPerSecond) / (1024.0 * 1024.0);

                                // 更新当前速率
                                processInfo.CurrentMBPerSecond = totalMBPerSecond;

                                // 检查是否超过限制
                                if (totalMBPerSecond > processInfo.LimitMBPerSecond)
                                {
                                    // 计算需要挂起的时间（秒）
                                    double suspendTimeNeeded = (totalMBPerSecond - processInfo.LimitMBPerSecond) / totalMBPerSecond;
                                    // 限制挂起时间最大为0.9秒
                                    int suspendMilliseconds = (int)(Math.Min(suspendTimeNeeded, 0.9) * 1000);

                                    if (suspendMilliseconds > 0)
                                    {
                                        // 挂起进程
                                        SuspendProcess(processId);
                                        Console.WriteLine($"进程 {processInfo.ProcessName} (ID: {processId}) 磁盘IO超限 " +
                                                      $"({totalMBPerSecond:F2} MB/s > {processInfo.LimitMBPerSecond:F2} MB/s)，" +
                                                      $"挂起 {suspendMilliseconds} 毫秒");

                                        // 等待指定时间
                                        Thread.Sleep(suspendMilliseconds);

                                        // 恢复进程
                                        ResumeProcess(processId);
                                        Console.WriteLine($"进程 {processInfo.ProcessName} (ID: {processId}) 已恢复运行");
                                    }
                                }
                            }

                            // 更新上次的计数器
                            processInfo.LastCounters = currentCounters;
                        }
                        catch (Exception ex)
                        {
                            // 处理可能已经结束的进程
                            Console.WriteLine($"监控进程 {processInfo.ProcessName} (ID: {processInfo.Process.Id}) 时出错: {ex.Message}");
                            // 标记进程以便稍后清理
                            UnregisterProcess(processInfo.Process.Id);
                        }
                    }

                    // 每秒检查一次
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"磁盘IO监控线程出错: {ex.Message}");
                    Thread.Sleep(1000); // 错误后等待一秒再继续
                }
            }
        }

        /// <summary>
        /// 应用磁盘限制到进程组
        /// </summary>
        /// <param name="processGroup">要应用限制的进程组</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(ProcessGroup processGroup)
        {
            if (processGroup == null || !processGroup.Config.DiskLimit.IsEnabled || 
                processGroup.Processes.Count == 0)
                return false;

            bool success = true;

            // 循环应用磁盘限制到每个进程
            foreach (var process in processGroup.Processes)
            {
                try
                {
                    string processName = process.ProcessName;
                    int processId = process.Id;

                    // 目前磁盘限制只能在进程级别实现，没有作业对象级别的API
                    bool processSuccess = ApplyLimit(process.Handle, process, processName, processId, processGroup.Config.DiskLimit);
                    if (!processSuccess)
                    {
                        Console.WriteLine($"对进程 {processName} (ID: {processId}) 应用磁盘限制失败");
                        success = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"对进程 {process.ProcessName} (ID: {process.Id}) 应用磁盘限制时出错: {ex.Message}");
                    success = false;
                }
            }

            if (success)
                Console.WriteLine($"成功为进程组 '{processGroup.Config.Name}' 应用磁盘限制");
            else
                Console.WriteLine($"部分进程组 '{processGroup.Config.Name}' 的磁盘限制应用失败");

            return success;
        }

        /// <summary>
        /// 应用磁盘限制到指定进程
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="process">进程对象</param>
        /// <param name="processName">进程名称（用于日志）</param>
        /// <param name="processId">进程ID</param>
        /// <param name="diskLimit">磁盘限制配置</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(IntPtr processHandle, Process process, string processName, int processId, DiskLimit diskLimit)
        {
            if (process == null || !diskLimit.IsEnabled)
                return false;

            try
            {
                // 注册进程到监控系统
                RegisterProcess(process, processName, diskLimit.ReadWriteRateLimit);
                
                Console.WriteLine($"为进程 {processName} (ID: {processId}) 设置磁盘读写速率限制为 {diskLimit.ReadWriteRateLimit}MB/s");
                Console.WriteLine($"已开始监控进程 {processName} 的磁盘IO活动");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用磁盘限制时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取进程的当前磁盘IO速率
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>当前IO速率（MB/秒），如果进程未监控则返回0</returns>
        public static double GetCurrentIORate(int processId)
        {
            lock (_lockObj)
            {
                if (_processIOMap.TryGetValue(processId, out ProcessIOInfo info))
                {
                    return info.CurrentMBPerSecond;
                }
                return 0;
            }
        }

        /// <summary>
        /// 获取进程的IO计数器
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <returns>IO计数器信息</returns>
        private static IO_COUNTERS GetIOCounters(IntPtr processHandle)
        {
            IO_COUNTERS counters = new IO_COUNTERS();
            if (processHandle != IntPtr.Zero)
            {
                try
                {
                    if (!NativeMethods.GetProcessIoCounters(processHandle, out counters))
                    {
                        throw new Exception($"获取进程IO计数器失败: {Marshal.GetLastWin32Error()}");
                    }
                }
                catch
                {
                    // 如果获取失败，返回空计数器
                }
            }
            return counters;
        }

        /// <summary>
        /// 挂起进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        private static void SuspendProcess(int processId)
        {
            try
            {
                NativeMethods.SuspendProcess(processId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"挂起进程ID: {processId} 时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复进程运行
        /// </summary>
        /// <param name="processId">进程ID</param>
        private static void ResumeProcess(int processId)
        {
            try
            {
                NativeMethods.ResumeProcess(processId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"恢复进程ID: {processId} 时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理所有资源，停止监控
        /// </summary>
        public static void Cleanup()
        {
            lock (_lockObj)
            {
                // 停止监控线程
                StopMonitoring();
                // 清空进程映射
                _processIOMap.Clear();
            }
        }
    }

    /// <summary>
    /// 提供磁盘IO相关的本机方法
    /// </summary>
    internal static partial class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessIoCounters(IntPtr ProcessHandle, out IO_COUNTERS IoCounters);

        /// <summary>
        /// 挂起进程的所有线程
        /// </summary>
        /// <param name="processId">进程ID</param>
        public static void SuspendProcess(int processId)
        {
            Process process = Process.GetProcessById(processId);

            foreach (ProcessThread thread in process.Threads)
            {
                IntPtr threadHandle = OpenThread(ThreadAccess.THREAD_SUSPEND_RESUME, false, (uint)thread.Id);
                if (threadHandle != IntPtr.Zero)
                {
                    try
                    {
                        SuspendThread(threadHandle);
                    }
                    finally
                    {
                        CloseHandle(threadHandle);
                    }
                }
            }
        }

        /// <summary>
        /// 恢复进程的所有线程
        /// </summary>
        /// <param name="processId">进程ID</param>
        public static void ResumeProcess(int processId)
        {
            Process process = Process.GetProcessById(processId);

            foreach (ProcessThread thread in process.Threads)
            {
                IntPtr threadHandle = OpenThread(ThreadAccess.THREAD_SUSPEND_RESUME, false, (uint)thread.Id);
                if (threadHandle != IntPtr.Zero)
                {
                    try
                    {
                        ResumeThread(threadHandle);
                    }
                    finally
                    {
                        CloseHandle(threadHandle);
                    }
                }
            }
        }
    }
}