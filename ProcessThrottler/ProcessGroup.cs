using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.PerformanceData;
using System.Runtime.InteropServices;
using System.Text;

namespace ProcessThrottler
{
    /// <summary>
    /// 进程配置组类，表示一个配置及其匹配的进程
    /// </summary>
    public class ProcessGroup : IDisposable
    {
        /// <summary>
        /// 进程组的配置信息
        /// </summary>
        public ProcessConfig Config { get; set; }

        /// <summary>
        /// 进程组中的进程列表
        /// </summary>
        public List<Process> Processes { get; private set; } = new List<Process>();

        /// <summary>
        /// Windows作业对象的句柄
        /// </summary>
        private IntPtr _jobHandle = IntPtr.Zero;

        /// <summary>
        /// 获取作业对象句柄
        /// </summary>
        public IntPtr JobHandle => _jobHandle;

        /// <summary>
        /// 指示是否已经初始化作业对象
        /// </summary>
        private bool _isJobInitialized = false;

        /// <summary>
        /// 指示进程组是否有新匹配的进程
        /// </summary>
        public bool IsNew { get; set; } = false;

        /// <summary>
        /// 构造函数，创建一个新的进程组
        /// </summary>
        /// <param name="config">进程配置</param>
        public ProcessGroup(ProcessConfig config)
        {
            Config = config;
            InitializeJob();
        }

        /// <summary>
        /// 初始化Windows作业对象
        /// </summary>
        private void InitializeJob()
        {
            if (_isJobInitialized)
                return;

            try
            {
                // 创建作业对象
                _jobHandle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
                if (_jobHandle == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    string errorMessage = new System.ComponentModel.Win32Exception(error).Message;
                    Console.WriteLine($"创建作业对象失败: 错误代码={error}, 错误信息={errorMessage}");
                    
                    // 添加更多诊断信息
                    Console.WriteLine($"详细信息: 配置组名称='{Config.Name}', 进程数量={Processes.Count}");
                    Console.WriteLine($"当前进程权限: IsElevated={IsCurrentProcessElevated()}");
                    
                    // 检查系统资源限制
                    Console.WriteLine($"系统资源: 可用内存={GetAvailableMemoryMB()}MB, CPU核心数={Environment.ProcessorCount}");
                    Console.WriteLine($"尝试以管理员权限运行程序可能解决此问题");
                    return;
                }

                // 设置作业对象的基本限制
                var basicInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                };

                var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = basicInfo
                };

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

                bool success = NativeMethods.SetInformationJobObject(_jobHandle,
                    JobObjectInfoType.ExtendedLimitInformation,
                    extendedInfoPtr, (uint)length);
/*
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    string errorMessage = new System.ComponentModel.Win32Exception(error).Message;
                    Console.WriteLine($"设置作业对象信息失败: 错误代码={error}, 错误信息={errorMessage}");
                    
                    // 添加错误代码解释
                    Console.WriteLine($"错误解释: {GetErrorExplanation(error)}");
                    Console.WriteLine($"配置详情: 配置组名称='{Config.Name}', 限制标志={basicInfo.LimitFlags}");
                    
                    NativeMethods.CloseHandle(_jobHandle);
                    _jobHandle = IntPtr.Zero;
                    Marshal.FreeHGlobal(extendedInfoPtr);
                    return;
  
                }
*/
                Console.WriteLine("成功设置作业对象信息");
                Marshal.FreeHGlobal(extendedInfoPtr);
                _isJobInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化作业对象时发生异常: {ex.Message}");
                Console.WriteLine($"异常类型: {ex.GetType().Name}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"内部异常: {ex.InnerException.Message}");
                }
                
                if (_jobHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(_jobHandle);
                    _jobHandle = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// 获取当前进程是否以管理员权限运行
        /// </summary>
        private bool IsCurrentProcessElevated()
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取系统可用内存(MB)
        /// </summary>
        private long GetAvailableMemoryMB()
        {
            try
            {
                using (var pc = new PerformanceCounter("Memory", "Available MBytes"))
                {
                    return (long)pc.NextValue();
                }
            }
            catch
            {
                return -1; // 无法获取
            }
        }

        /// <summary>
        /// 获取Windows错误代码的详细解释
        /// </summary>
        private string GetErrorExplanation(int errorCode)
        {
            switch (errorCode)
            {
                case 5: return "拒绝访问。您可能需要以管理员权限运行程序。";
                case 6: return "句柄无效。";
                case 8: return "内存不足，无法处理此命令。";
                case 87: return "参数错误。";
                case 1314: return "客户端没有所需的特权。尝试以管理员身份运行。";
                case 1450: return "系统资源不足，无法完成请求的服务。";
                case 1455: return "页面文件太小，无法完成操作。";
                default: return "未知错误。";
            }
        }

        /// <summary>
        /// 向进程组添加进程
        /// </summary>
        /// <param name="process">要添加的进程</param>
        public void AddProcess(Process process)
        {
            if (process == null || Processes.Exists(p => p.Id == process.Id))
                return;

            Processes.Add(process);

            // 将进程添加到作业对象
            if (_isJobInitialized && _jobHandle != IntPtr.Zero)
            {
                try
                {
                    if (!NativeMethods.AssignProcessToJobObject(_jobHandle, process.Handle))
                    {
                        int error = Marshal.GetLastWin32Error();
                        string errorMessage = new System.ComponentModel.Win32Exception(error).Message;
                        Console.WriteLine($"将进程 {process.ProcessName} (ID: {process.Id}) 添加到作业对象失败: 错误代码={error}, 错误信息={errorMessage}");
                        
                        // 错误代码5表示拒绝访问，可能是进程已经在其他作业对象中
                        if (error == 5)
                        {
                            Console.WriteLine($"进程 {process.ProcessName} 可能已经属于其他作业对象，无法添加到当前配置组");
                            Console.WriteLine($"解决方案：重启目标进程，或确保没有其他限制工具在运行");
                        }
                        else if (error == 87)
                        {
                            Console.WriteLine($"参数错误：进程句柄可能无效或已关闭");
                        }
                        else if (error == 6)
                        {
                            Console.WriteLine($"句柄无效：作业对象句柄或进程句柄已经关闭");
                        }
                        else
                        {
                            Console.WriteLine($"未知错误：请尝试以管理员权限运行程序，或重启目标进程");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"进程 {process.ProcessName} (ID: {process.Id}) 已成功添加到配置组 '{Config.Name}' 的作业对象");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"将进程添加到作业对象时出错: {ex.Message}");
                    Console.WriteLine($"异常类型: {ex.GetType().Name}");
                    Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                }
            }
            else
            {
                Console.WriteLine($"警告：作业对象未初始化，无法将进程 {process.ProcessName} (ID: {process.Id}) 添加到作业对象");
                Console.WriteLine($"请检查作业对象初始化是否成功，可能需要以管理员权限运行程序");
            }
        }

        /// <summary>
        /// 清空进程组中的所有进程
        /// </summary>
        public void ClearProcesses()
        {
            Processes.Clear();
        }

        /// <summary>
        /// 应用进程配置限制
        /// </summary>
        public void ApplyLimits()
        {
            if (!Config.IsEnabled || Processes.Count == 0)
                return;
                
            // 确保作业对象已初始化
            if (!_isJobInitialized || _jobHandle == IntPtr.Zero)
            {
                Console.WriteLine($"警告：作业对象未初始化，尝试重新初始化");
                InitializeJob();
                if (_jobHandle == IntPtr.Zero)
                {
                    Console.WriteLine($"错误：无法初始化作业对象，无法应用限制");
                    return;
                }
            }

            bool success = true;

            // 应用CPU限制
            if (Config.CpuLimit.IsEnabled)
            {
                success &= CPUThrottler.ApplyLimit(this);
            }

            // 应用内存限制
            if (Config.MemoryLimit.IsEnabled)
            {
                success &= MemoryThrottler.ApplyLimit(this);
            }
            else
            {
                // 如果内存限制未启用，但可能之前启用过，需要注销所有进程的内存监控
                foreach (var process in Processes)
                {
                    try
                    {
                        MemoryThrottler.UnregisterProcess(process.Id);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"注销进程 {process.ProcessName} (ID: {process.Id}) 的内存监控时出错: {ex.Message}");
                    }
                }
            }

            // 应用进程优先级
            if (Config.ProcessPriority.IsEnabled)
            {
                success &= PriorityThrottler.ApplyLimit(this);
            }

            // 应用磁盘限制
            if (Config.DiskLimit.IsEnabled)
            {
                success &= DiskThrottler.ApplyLimit(this);
            }

            // 应用网络限制
            if (Config.NetworkLimit.IsEnabled)
            {
                success &= NetworkThrottler.ApplyLimit(this);
            }
            
            // 重置新进程标记
            IsNew = false;

            if (success)
                Console.WriteLine($"成功应用所有限制到进程组 '{Config.Name}'");
            else
                Console.WriteLine($"部分限制应用到进程组 '{Config.Name}' 失败");
        }

        /// <summary>
        /// 对每个进程应用CPU限制 (已废弃，使用CPUThrottler.ApplyLimit(ProcessGroup)替代)
        /// </summary>
        private void ApplyPerProcessCpuLimits()
        {
            foreach (var process in Processes)
            {
                try
                {
                    string processName = process.ProcessName;
                    // 应用CPU限制
                    bool success = CPUThrottler.ApplyLimit(process.Handle, processName, process.Id, Config.CpuLimit, _jobHandle);
                    if (success)
                    {
                        Console.WriteLine($"成功应用CPU限制到进程 {processName} (ID: {process.Id})");
                    }
                    else
                    {
                        Console.WriteLine($"应用CPU限制到进程 {processName} (ID: {process.Id}) 失败");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"对进程 {process.ProcessName} (ID: {process.Id}) 应用CPU限制时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 对每个进程应用内存限制 (已废弃，使用MemoryThrottler.ApplyLimit(ProcessGroup)替代)
        /// </summary>
        private void ApplyPerProcessMemoryLimits()
        {
            foreach (var process in Processes)
            {
                try
                {
                    string processName = process.ProcessName;
                    // 应用内存限制，启用内存监控
                    bool success = MemoryThrottler.ApplyLimit(
                        process.Handle, 
                        processName, 
                        process.Id, 
                        Config.MemoryLimit.MemoryUsageLimit, 
                        Config.MemoryLimit.IsEnabled, 
                        Config.MemoryLimit.EnableMonitoring,  // 使用配置中的监控设置
                        // 如果开启自动整理或超限操作是移动到分页文件，则启用自动内存整理
                        Config.MemoryLimit.EnableAutoTrim || Config.MemoryLimit.OveruseAction == MemoryOveruseAction.TransferToPageFile,
                        // 如果开启超限终止或超限操作是终止进程，则启用超限终止
                        Config.MemoryLimit.TerminateOnExceed || Config.MemoryLimit.OveruseAction == MemoryOveruseAction.TerminateProcess,
                        _jobHandle // 传递作业对象句柄
                    );
                    
                    if (success)
                    {
                        Console.WriteLine($"成功应用内存限制到进程 {processName} (ID: {process.Id})，并启用内存监控");
                    }
                    else
                    {
                        Console.WriteLine($"应用内存限制到进程 {processName} (ID: {process.Id}) 失败");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"对进程 {process.ProcessName} (ID: {process.Id}) 应用内存限制时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 对每个进程应用网络限制 (已废弃，使用NetworkThrottler.ApplyLimit(ProcessGroup)替代)
        /// </summary>
        private void ApplyPerProcessNetworkLimits()
        {
            foreach (var process in Processes)
            {
                try
                {
                    string processName = process.ProcessName;
                    // 应用网络限制
                    bool success = NetworkThrottler.ApplyLimit(process.Handle, processName, process.Id, Config.NetworkLimit, _jobHandle);
                    if (success)
                    {
                        Console.WriteLine($"成功应用网络限制到进程 {processName} (ID: {process.Id})");
                    }
                    else
                    {
                        Console.WriteLine($"应用网络限制到进程 {processName} (ID: {process.Id}) 失败");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"对进程 {process.ProcessName} (ID: {process.Id}) 应用网络限制时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 对每个进程应用优先级限制 (已废弃，使用PriorityThrottler.ApplyLimit(ProcessGroup)替代)
        /// </summary>
        private void ApplyPerProcessPriorityLimits()
        {
            foreach (var process in Processes)
            {
                try
                {
                    string processName = process.ProcessName;
                    // 应用优先级限制
                    bool success = PriorityThrottler.ApplyLimit(process.Handle, process, processName, process.Id, Config.ProcessPriority);
                    if (success)
                    {
                        Console.WriteLine($"成功应用优先级限制到进程 {processName} (ID: {process.Id})");
                    }
                    else
                    {
                        Console.WriteLine($"应用优先级限制到进程 {processName} (ID: {process.Id}) 失败");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"对进程 {process.ProcessName} (ID: {process.Id}) 应用优先级限制时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 应用磁盘限制 (已废弃，使用DiskThrottler.ApplyLimit(ProcessGroup)替代)
        /// </summary>
        private void ApplyDiskLimit()
        {
            foreach (var process in Processes)
            {
                try
                {
                    // 调用静态的DiskThrottler.ApplyLimit方法
                    bool success = DiskThrottler.ApplyLimit(process.Handle, process, process.ProcessName, process.Id, Config.DiskLimit);
                    if (success)
                    {
                        Console.WriteLine($"成功应用磁盘限制到进程 {process.ProcessName} (ID: {process.Id})");
                    }
                    else
                    {
                        Console.WriteLine($"应用磁盘限制到进程 {process.ProcessName} (ID: {process.Id}) 失败");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"对进程 {process.ProcessName} (ID: {process.Id}) 应用磁盘限制时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取进程组的详细信息
        /// </summary>
        public string GetDetailInfo()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"【配置组】: {Config.Name}");
            sb.AppendLine($"  - 启用状态: {(Config.IsEnabled ? "启用" : "禁用")}");
            sb.AppendLine($"  - 作业对象状态: {(_isJobInitialized ? "已初始化" : "未初始化")}");

            // 显示路径配置
            if (Config.Paths.Count > 0)
            {
                sb.AppendLine("  - 监控路径:");
                foreach (var path in Config.Paths)
                {
                    sb.AppendLine($"    * {path.FilePath}");
                }
            }

            // 显示CPU限制配置
            if (Config.CpuLimit.IsEnabled)
            {
                sb.AppendLine("  - CPU限制: 已启用");
                switch (Config.CpuLimit.LimitType)
                {
                    case CpuLimitType.RelativeWeight:
                        sb.AppendLine($"    * 相对权重: {Config.CpuLimit.RelativeWeight}");
                        break;
                    case CpuLimitType.AbsoluteRate:
                        sb.AppendLine($"    * 限制百分比: {Config.CpuLimit.RatePercentage}%");
                        break;
                }

                switch (Config.CpuLimit.CoreLimitType)
                {
                    case CoreLimitType.CoreCount:
                        sb.AppendLine($"    * 核心数量限制: {Config.CpuLimit.CoreCount}个核心");
                        break;
                    case CoreLimitType.CoreNumber:
                        sb.AppendLine($"    * 指定核心编号: {Config.CpuLimit.CoreNumber}");
                        break;
                }
            }

            // 显示内存限制配置
            if (Config.MemoryLimit.IsEnabled)
            {
                sb.AppendLine("  - 内存限制: 已启用");
                sb.AppendLine($"    * 内存限制值: {Config.MemoryLimit.MemoryUsageLimit}MB");
                sb.AppendLine($"    * 超限操作: {(Config.MemoryLimit.OveruseAction == MemoryOveruseAction.TerminateProcess ? "终止进程" : "转移到分页文件")}");
                sb.AppendLine($"    * 内存监控: {(Config.MemoryLimit.EnableMonitoring ? "已启用" : "未启用")}");
                if (Config.MemoryLimit.EnableMonitoring)
                {
                    sb.AppendLine($"    * 自动内存整理: {(Config.MemoryLimit.EnableAutoTrim ? "已启用" : "未启用")}");
                    sb.AppendLine($"    * 超限自动终止: {(Config.MemoryLimit.TerminateOnExceed ? "已启用" : "未启用")}");
                }
            }

            // 显示磁盘限制配置
            if (Config.DiskLimit.IsEnabled)
            {
                sb.AppendLine("  - 磁盘限制: 已启用");
                sb.AppendLine($"    * 读写速率限制: {Config.DiskLimit.ReadWriteRateLimit}MB/s");
            }

            // 显示网络限制配置
            if (Config.NetworkLimit.IsEnabled)
            {
                sb.AppendLine("  - 网络限制: 已启用");
                sb.AppendLine($"    * 最大速率限制: {Config.NetworkLimit.MaxRateLimit / 1024}MB/s");
                if (Config.NetworkLimit.SpecifyTransferPriority)
                {
                    sb.AppendLine($"    * 传输优先级: {Config.NetworkLimit.PriorityValue}");
                }
            }

            // 显示进程优先级配置
            if (Config.ProcessPriority.IsEnabled)
            {
                sb.AppendLine("  - 进程优先级: 已启用");
                string priorityName;
                switch (Config.ProcessPriority.PriorityValue)
                {
                    case 0: priorityName = "空闲(Idle)"; break;
                    case 1: priorityName = "低于正常(BelowNormal)"; break;
                    case 2: priorityName = "正常(Normal)"; break;
                    case 3: priorityName = "高于正常(AboveNormal)"; break;
                    case 4: priorityName = "高(High)"; break;
                    case 5: priorityName = "实时(RealTime)"; break;
                    default: priorityName = "未知"; break;
                }
                sb.AppendLine($"    * 优先级: {priorityName}");
            }

            // 显示匹配的进程
            sb.AppendLine("  - 匹配进程:");
            if (Processes.Count == 0)
            {
                sb.AppendLine("    * 无匹配进程");
            }
            else
            {
                foreach (var process in Processes)
                {
                    try
                    {
                        string processPath = "未知";
                        try
                        {
                            processPath = process.MainModule?.FileName ?? "未知";
                        }
                        catch { }

                        sb.AppendLine($"    * {process.ProcessName} (ID: {process.Id})");
                        sb.AppendLine($"      路径: {processPath}");
                        sb.AppendLine($"      CPU使用率: {process.TotalProcessorTime}");
                        sb.AppendLine($"      内存使用: {process.WorkingSet64 / 1024 / 1024}MB");

                        try
                        {
                            sb.AppendLine($"      当前优先级: {process.PriorityClass}");
                        }
                        catch { }

                        sb.AppendLine();
                    }
                    catch { }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 注销所有进程的监控
            foreach (var process in Processes)
            {
                try
                {
                    // 注销内存监控
                    MemoryThrottler.UnregisterProcess(process.Id);
                    
                    // 这里可以添加注销其他监控的代码，如磁盘监控等
                }
                catch
                {
                    // 忽略注销过程中的错误
                }
            }
            
            // 释放作业对象
            if (_jobHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_jobHandle);
                _jobHandle = IntPtr.Zero;
                _isJobInitialized = false;
            }

            // 清理进程列表
            ClearProcesses();
        }
    }
}