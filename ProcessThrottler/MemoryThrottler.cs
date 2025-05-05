using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ProcessThrottler
{
    /// <summary>
    /// 内存限制器，提供限制进程内存使用的静态方法
    /// </summary>
    public static class MemoryThrottler
    {
        // 进程ID到内存监控信息的映射
        private static readonly Dictionary<int, ProcessMemoryInfo> _processMemoryMap = new Dictionary<int, ProcessMemoryInfo>();
        // 监控线程
        private static Thread _monitorThread;
        // 线程同步锁
        private static readonly object _lockObj = new object();
        // 线程停止标志
        private static bool _stopMonitoring = false;
        // 是否已启动监控
        private static bool _isMonitoringStarted = false;
        // 监控间隔（毫秒）
        private static int _monitorInterval = 2000;

        /// <summary>
        /// 进程内存监控信息类
        /// </summary>
        private class ProcessMemoryInfo
        {
            public Process Process { get; set; }
            public string ProcessName { get; set; }
            public double LimitMB { get; set; }
            public double CurrentMB { get; set; }
            public bool EnableAutoTrim { get; set; }
            public bool TerminateOnExceed { get; set; }

            public ProcessMemoryInfo(Process process, string processName, double limitMB, bool enableAutoTrim, bool terminateOnExceed)
            {
                Process = process;
                ProcessName = processName;
                LimitMB = limitMB;
                CurrentMB = 0;
                EnableAutoTrim = enableAutoTrim;
                TerminateOnExceed = terminateOnExceed;
            }
        }

        /// <summary>
        /// 应用内存限制到进程组
        /// </summary>
        /// <param name="processGroup">要应用限制的进程组</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(ProcessGroup processGroup)
        {
            if (processGroup == null || !processGroup.Config.MemoryLimit.IsEnabled || 
                processGroup.Processes.Count == 0)
                return false;

            // 确认作业对象有效
            IntPtr jobHandle = processGroup.JobHandle;
            if (jobHandle == IntPtr.Zero)
            {
                Console.WriteLine($"应用内存限制失败：进程组 '{processGroup.Config.Name}' 没有有效的作业对象句柄");
                return false;
            }

            bool success = true;

            try
            {
                // 通过作业对象设置内存限制
                var memoryLimit = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_PROCESS_MEMORY,
                        ProcessMemoryLimit = new UIntPtr((ulong)processGroup.Config.MemoryLimit.MemoryUsageLimit * 1024 * 1024)
                    }
                };

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr memLimitPtr = Marshal.AllocHGlobal(length);
                Marshal.StructureToPtr(memoryLimit, memLimitPtr, false);

                success = NativeMethods.SetInformationJobObject(
                    jobHandle,
                    JobObjectInfoType.ExtendedLimitInformation,
                    memLimitPtr,
                    (uint)length);

                Marshal.FreeHGlobal(memLimitPtr);

                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"设置作业对象内存限制失败: 错误代码={error}, 错误信息={new System.ComponentModel.Win32Exception(error).Message}");
                }
                else
                {
                    Console.WriteLine($"已通过作业对象为进程组 '{processGroup.Config.Name}' 设置内存限制为 {processGroup.Config.MemoryLimit.MemoryUsageLimit}MB");
                }

                // 对每个进程单独应用工作集限制
                foreach (var process in processGroup.Processes)
                {
                    try
                    {
                        string processName = process.ProcessName;
                        int processId = process.Id;

                        // 设置进程的工作集大小限制
                        ulong memoryLimitBytes = (ulong)processGroup.Config.MemoryLimit.MemoryUsageLimit * 1024 * 1024;
                        IntPtr minimumWorkingSetSize = (IntPtr)1; // 最小工作集
                        IntPtr maximumWorkingSetSize = (IntPtr)memoryLimitBytes;
                        
                        bool processSuccess = NativeMethods.SetProcessWorkingSetSize(process.Handle, minimumWorkingSetSize, maximumWorkingSetSize);
                        if (!processSuccess)
                        {
                            int error = Marshal.GetLastWin32Error();
                            Console.WriteLine($"设置进程 {processName} (ID: {processId}) 工作集大小失败: 错误代码={error}");
                            success = false;
                        }
                        else
                        {
                            Console.WriteLine($"已为进程 {processName} (ID: {processId}) 设置工作集大小限制为 {processGroup.Config.MemoryLimit.MemoryUsageLimit}MB");
                        }

                        // 只有在启用了内存限制且启用了内存监控时，才注册进程到监控循环
                        if (processGroup.Config.MemoryLimit.IsEnabled && processGroup.Config.MemoryLimit.EnableMonitoring)
                        {
                            bool enableAutoTrim = processGroup.Config.MemoryLimit.EnableAutoTrim || 
                                                 processGroup.Config.MemoryLimit.OveruseAction == MemoryOveruseAction.TransferToPageFile;
                            
                            bool terminateOnExceed = processGroup.Config.MemoryLimit.TerminateOnExceed || 
                                                    processGroup.Config.MemoryLimit.OveruseAction == MemoryOveruseAction.TerminateProcess;
                            
                            RegisterProcess(process, processName, processGroup.Config.MemoryLimit.MemoryUsageLimit, 
                                           enableAutoTrim, terminateOnExceed);
                            
                            Console.WriteLine($"已注册进程 {processName} (ID: {processId}) 到内存监控");
                        }
                        else
                        {
                            Console.WriteLine($"跳过进程 {processName} (ID: {processId}) 的内存监控注册，内存监控未启用");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"对进程 {process.ProcessName} (ID: {process.Id}) 应用内存限制时出错: {ex.Message}");
                        success = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用内存限制到进程组时出错: {ex.Message}");
                success = false;
            }

            return success;
        }

        /// <summary>
        /// 启动内存监控线程
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
                    Name = "MemoryThrottler_Monitor"
                };
                _monitorThread.Start();
                _isMonitoringStarted = true;
                Console.WriteLine("内存监控线程已启动");
            }
        }

        /// <summary>
        /// 停止内存监控线程
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
                        Console.WriteLine("内存监控线程未能正常停止，将强制终止");
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
                Console.WriteLine("内存监控线程已停止");
            }
        }

        /// <summary>
        /// 注册一个进程到内存监控
        /// </summary>
        private static void RegisterProcess(Process process, string processName, double limitMB, bool enableAutoTrim, bool terminateOnExceed)
        {
            lock (_lockObj)
            {
                if (process == null)
                    return;

                // 如果内存限制值小于等于0，不应该注册监控
                if (limitMB <= 0)
                {
                    Console.WriteLine($"跳过进程 {processName} (ID: {process.Id}) 的内存监控注册，内存限制值无效");
                    return;
                }
                
                int processId = process.Id;
                // 如果已存在，更新限制值
                if (_processMemoryMap.ContainsKey(processId))
                {
                    _processMemoryMap[processId].LimitMB = limitMB;
                    _processMemoryMap[processId].EnableAutoTrim = enableAutoTrim;
                    _processMemoryMap[processId].TerminateOnExceed = terminateOnExceed;
                    Console.WriteLine($"更新进程 {processName} (ID: {processId}) 的内存限制为 {limitMB} MB");
                }
                else
                {
                    // 添加新进程
                    _processMemoryMap[processId] = new ProcessMemoryInfo(process, processName, limitMB, enableAutoTrim, terminateOnExceed);
                    Console.WriteLine($"已注册进程 {processName} (ID: {processId}) 到内存监控");
                }

                // 如果这是第一个进程，启动监控线程
                if (_processMemoryMap.Count == 1 && !_isMonitoringStarted)
                {
                    StartMonitoring();
                }
            }
        }

        /// <summary>
        /// 注销一个进程的内存监控
        /// </summary>
        public static void UnregisterProcess(int processId)
        {
            lock (_lockObj)
            {
                if (_processMemoryMap.ContainsKey(processId))
                {
                    _processMemoryMap.Remove(processId);
                    Console.WriteLine($"已从内存监控中移除进程ID: {processId}");

                    // 如果没有进程了，停止监控线程
                    if (_processMemoryMap.Count == 0 && _isMonitoringStarted)
                    {
                        StopMonitoring();
                    }
                }
            }
        }

        /// <summary>
        /// 主监控循环，检查并限制所有注册进程的内存使用
        /// </summary>
        private static void MonitorLoop()
        {
            while (!_stopMonitoring)
            {
                try
                {
                    // 获取所有注册的进程的快照
                    List<ProcessMemoryInfo> activeProcesses;
                    lock (_lockObj)
                    {
                        activeProcesses = _processMemoryMap.Values.ToList();
                    }

                    // 检查每个进程的内存使用情况
                    foreach (var processInfo in activeProcesses)
                    {
                        try
                        {
                            // 获取进程和ID
                            Process process = processInfo.Process;
                            int processId = process.Id;

                            // 刷新进程信息
                            process.Refresh();

                            // 计算内存使用量（MB）
                            double currentMemoryMB = process.WorkingSet64 / (1024.0 * 1024.0);
                            
                            // 更新当前内存使用量
                            processInfo.CurrentMB = currentMemoryMB;

                            // 检查是否超过限制
                            if (currentMemoryMB > processInfo.LimitMB)
                            {
                                Console.WriteLine($"进程 {processInfo.ProcessName} (ID: {processId}) 内存超限 " +
                                               $"({currentMemoryMB:F2} MB > {processInfo.LimitMB:F2} MB)");

                                // 如果启用了自动内存整理
                                if (processInfo.EnableAutoTrim)
                                {
                                    Console.WriteLine($"尝试整理进程 {processInfo.ProcessName} (ID: {processId}) 的内存");
                                    // 获取进程句柄
                                    IntPtr processHandle = process.Handle;
                                    // 将内存移动到分页文件
                                    MoveToPageFile(processHandle, processInfo.ProcessName);
                                }

                                // 如果启用了超限终止
                                if (processInfo.TerminateOnExceed)
                                {
                                    Console.WriteLine($"进程 {processInfo.ProcessName} (ID: {processId}) 内存超限，将被终止");
                                    TerminateProcess(process);
                                    // 从监控列表中移除
                                    UnregisterProcess(processId);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 处理可能已经结束的进程
                            Console.WriteLine($"监控进程 {processInfo.ProcessName} (ID: {processInfo.Process.Id}) 时出错: {ex.Message}");
                            // 标记进程以便稍后清理
                            UnregisterProcess(processInfo.Process.Id);
                        }
                    }

                    // 每隔指定时间检查一次
                    Thread.Sleep(_monitorInterval);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"内存监控线程出错: {ex.Message}");
                    Thread.Sleep(1000); // 错误后等待一秒再继续
                }
            }
        }

        /// <summary>
        /// 应用内存限制到指定进程
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="processName">进程名称（用于日志）</param>
        /// <param name="processId">进程ID</param>
        /// <param name="memoryLimitMB">内存使用上限（MB）</param>
        /// <param name="isEnabled">是否启用内存限制</param>
        /// <param name="enableMonitoring">是否启用内存监控</param>
        /// <param name="enableAutoTrim">是否启用自动内存整理</param>
        /// <param name="terminateOnExceed">是否在超过限制时终止进程</param>
        /// <param name="jobHandle">作业对象句柄，用于作业对象级别限制</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(IntPtr processHandle, string processName, int processId, 
                                     int memoryLimitMB, bool isEnabled = true, 
                                     bool enableMonitoring = false, bool enableAutoTrim = false, 
                                     bool terminateOnExceed = false, IntPtr jobHandle = default)
        {
            if (processHandle == IntPtr.Zero || !isEnabled || memoryLimitMB <= 0)
                return false;

            try
            {
                bool success = true;
                
                // 如果有作业对象句柄，通过作业对象设置内存限制
                if (jobHandle != IntPtr.Zero)
                {
                    // 设置作业对象的内存限制
                    // 在Windows上可以通过JobObject对内存使用进行更细粒度的控制
                    var memoryLimit = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                    {
                        BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                        {
                            LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_PROCESS_MEMORY,
                            ProcessMemoryLimit = new UIntPtr((ulong)memoryLimitMB * 1024 * 1024)
                        }
                    };

                    int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                    IntPtr memLimitPtr = Marshal.AllocHGlobal(length);
                    Marshal.StructureToPtr(memoryLimit, memLimitPtr, false);

                    success = NativeMethods.SetInformationJobObject(
                        jobHandle,
                        JobObjectInfoType.ExtendedLimitInformation,
                        memLimitPtr,
                        (uint)length);

                    Marshal.FreeHGlobal(memLimitPtr);

                    if (!success)
                    {
                        Console.WriteLine($"设置作业对象内存限制失败: {Marshal.GetLastWin32Error()}");
                    }
                    else
                    {
                        Console.WriteLine($"已通过作业对象为进程 {processName} (ID: {processId}) 设置内存限制为 {memoryLimitMB}MB");
                    }
                }

                // 设置进程的工作集大小限制
                Console.WriteLine($"为进程 {processName} (ID: {processId}) 设置内存限制: {memoryLimitMB}MB");
                
                // 计算内存限制（字节）
                ulong memoryLimitBytes = (ulong)memoryLimitMB * 1024 * 1024;
                
                // 设置最小和最大工作集大小
                IntPtr minimumWorkingSetSize = (IntPtr)1; // 最小工作集
                IntPtr maximumWorkingSetSize = (IntPtr)memoryLimitBytes;
                
                // 设置进程工作集限制
                success &= NativeMethods.SetProcessWorkingSetSize(processHandle, minimumWorkingSetSize, maximumWorkingSetSize);
                
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"设置工作集大小失败: {error}");
                    return false;
                }
                
                Console.WriteLine($"已为进程 {processName} (ID: {processId}) 设置内存限制为 {memoryLimitMB}MB");
                
                // 只有在内存限制已启用并且启用了内存监控的情况下才注册进程
                if (isEnabled && enableMonitoring)
                {
                    Process process = Process.GetProcessById(processId);
                    RegisterProcess(process, processName, memoryLimitMB, enableAutoTrim, terminateOnExceed);
                    Console.WriteLine($"已注册进程 {processName} (ID: {processId}) 到内存监控");
                }
                else
                {
                    Console.WriteLine($"跳过进程 {processName} (ID: {processId}) 的内存监控注册，内存监控未启用或内存限制未开启");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用内存限制时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取进程的当前内存使用量
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>当前内存使用量（MB），如果进程未监控则返回0</returns>
        public static double GetCurrentMemoryUsage(int processId)
        {
            lock (_lockObj)
            {
                if (_processMemoryMap.TryGetValue(processId, out ProcessMemoryInfo info))
                {
                    return info.CurrentMB;
                }
                return 0;
            }
        }

        /// <summary>
        /// 将进程内存移动到分页文件
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="processName">进程名称（用于日志）</param>
        public static void MoveToPageFile(IntPtr processHandle, string processName)
        {
            try
            {
                Console.WriteLine($"尝试将进程 {processName} 的内存移动到分页文件");

                // 获取进程工作集信息
                if (processHandle == IntPtr.Zero)
                    return;

                // 清空工作集
                bool success = NativeMethods.EmptyWorkingSet(processHandle);
                if (!success)
                    Console.WriteLine($"清空工作集失败: {Marshal.GetLastWin32Error()}");
                else
                    Console.WriteLine($"已清空进程 {processName} 的工作集");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"移动内存到分页文件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 终止进程
        /// </summary>
        /// <param name="process">要终止的进程</param>
        public static void TerminateProcess(Process process)
        {
            try
            {
                if (process == null)
                    return;
                    
                Console.WriteLine($"准备终止内存超限的进程: {process.ProcessName} (ID: {process.Id})");
                process.Kill();
                Console.WriteLine($"已终止进程: {process.ProcessName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"终止进程时出错: {ex.Message}");
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
                _processMemoryMap.Clear();
            }
        }
    }

    /// <summary>
    /// 内存配额限制结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct QUOTA_LIMITS
    {
        public IntPtr PagedPoolLimit;
        public IntPtr NonPagedPoolLimit;
        public IntPtr MinimumWorkingSetSize;
        public IntPtr MaximumWorkingSetSize;
        public IntPtr PagefileLimit;
        public IntPtr TimeLimit;
    }

    /// <summary>
    /// 提供内存相关的本机方法
    /// </summary>
    internal static partial class NativeMethods
    {
        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessWorkingSetSize(
            IntPtr hProcess,
            IntPtr dwMinimumWorkingSetSize,
            IntPtr dwMaximumWorkingSetSize);
    }
}