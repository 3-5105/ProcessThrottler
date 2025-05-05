using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProcessThrottler
{
    /// <summary>
    /// CPU限制器类，提供限制进程的CPU使用率和核心使用情况的静态方法
    /// </summary>
    public static class CPUThrottler
    {
        /// <summary>
        /// 应用CPU限制到进程组
        /// </summary>
        /// <param name="processGroup">要应用限制的进程组</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(ProcessGroup processGroup)
        {
            if (processGroup == null || !processGroup.Config.CpuLimit.IsEnabled ||
                processGroup.Processes.Count == 0)
                return false;

            // 确认作业对象有效
            IntPtr jobHandle = processGroup.JobHandle;
            if (jobHandle == IntPtr.Zero)
            {
                Console.WriteLine($"应用CPU限制失败：进程组 '{processGroup.Config.Name}' 没有有效的作业对象句柄");
                Console.WriteLine($"详细信息：进程组中的进程数量={processGroup.Processes.Count}，检查作业对象是否已正确初始化");
                Console.WriteLine($"建议：请检查作业对象初始化过程中是否出现错误，可能需要以管理员权限运行程序");
                return false;
            }

            bool success = true;

            // 应用CPU速率限制到作业对象
            success &= ApplyCpuRateLimit(jobHandle, processGroup.Config.Name, processGroup.Config.CpuLimit);
            Console.WriteLine($"为进程组 '{processGroup.Config.Name}' 应用CPU速率限制: {success}");

            // 为每个进程应用CPU核心限制
            foreach (var process in processGroup.Processes)
            {
                try
                {
                    string processName = process.ProcessName;
                    int processId = process.Id;
                    success &= ApplyCpuCoreLimit(process.Handle, processName, processId, processGroup.Config.CpuLimit);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"对进程 {process.ProcessName} (ID: {process.Id}) 应用CPU核心限制时出错: {ex.Message}");
                    success = false;
                }
            }

            if (success)
                Console.WriteLine($"成功为进程组 '{processGroup.Config.Name}' 应用CPU限制");
            else
                Console.WriteLine($"为进程组 '{processGroup.Config.Name}' 应用CPU限制失败");

            return success;
        }

        /// <summary>
        /// 应用CPU限制到指定进程
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="processName">进程名称（用于日志）</param>
        /// <param name="processId">进程ID</param>
        /// <param name="cpuLimit">CPU限制配置</param>
        /// <param name="jobHandle">作业对象句柄，如果为IntPtr.Zero则会创建新的作业对象</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(IntPtr processHandle, string processName, int processId, CpuLimit cpuLimit, IntPtr jobHandle)
        {
            if (processHandle == IntPtr.Zero || cpuLimit == null || !cpuLimit.IsEnabled)
                return false;

            // 确认作业对象有效
            if (jobHandle == IntPtr.Zero)
            {
                Console.WriteLine($"应用CPU限制失败：无效的作业对象句柄");
                return false;
            }

            bool success = true;

            // 应用CPU速率限制
            success &= ApplyCpuRateLimit(jobHandle, processName, cpuLimit);
            Console.WriteLine($"ApplyCpuRateLimit: {success}");

            // 应用CPU核心限制
            success &= ApplyCpuCoreLimit(processHandle, processName, processId, cpuLimit);
            Console.WriteLine($"ApplyCpuCoreLimit: {success}");
            if (success)
                Console.WriteLine($"进程 {processName} (ID: {processId}) 的CPU限制已成功应用");
            else
                Console.WriteLine($"进程 {processName} (ID: {processId}) 的CPU限制应用失败");
            return success;
        }

        /// <summary>
        /// 应用CPU速率限制
        /// </summary>
        /// <param name="jobHandle">作业对象句柄</param>
        /// <param name="processName">进程名称（用于日志）</param>
        /// <param name="cpuLimit">CPU限制配置</param>
        /// <returns>操作是否成功</returns>
        private static bool ApplyCpuRateLimit(IntPtr jobHandle, string processName, CpuLimit cpuLimit)
        {
            if (jobHandle == IntPtr.Zero)
            {
                Console.WriteLine("ApplyCpuRateLimit: 无效的作业对象句柄");
                return false;
            }

            if (cpuLimit.LimitType == CpuLimitType.None)
            {
                Console.WriteLine("ApplyCpuRateLimit: 不应用CPU速率限制 (LimitType=None)");
                return true;
            }

            try
            {
                // 设置CPU限制
                var cpuRateInfo = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION();

                switch (cpuLimit.LimitType)
                {
                    case CpuLimitType.RelativeWeight:
                        cpuRateInfo.ControlFlags =
                         (uint)JOB_OBJECT_CPU_RATE_CONTROL.JOB_OBJECT_CPU_RATE_CONTROL_WEIGHT_BASED
                         | (uint)JOB_OBJECT_CPU_RATE_CONTROL.JOB_OBJECT_CPU_RATE_CONTROL_ENABLE;
                        // 相对权重取值为1-9，映射到Windows API需要的1-9
                        cpuRateInfo.RateControl.Weight = (uint)Math.Max(1, Math.Min(9, cpuLimit.RelativeWeight));
                        Console.WriteLine($"为进程 {processName} 设置CPU相对权重为: {cpuRateInfo.RateControl.Weight}");
                        break;

                    case CpuLimitType.AbsoluteRate:
                        cpuRateInfo.ControlFlags =
                         (uint)JOB_OBJECT_CPU_RATE_CONTROL.JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP
                         | (uint)JOB_OBJECT_CPU_RATE_CONTROL.JOB_OBJECT_CPU_RATE_CONTROL_ENABLE;
                        // 将百分比转换为CpuRate值（百分比乘以100）
                        // 确保RatePercentage在有效范围内（1-100）
                        int ratePercentage = Math.Max(1, Math.Min(100, cpuLimit.RatePercentage));
                        Console.WriteLine($"cpuLimit.RatePercentage: {ratePercentage}");
                        cpuRateInfo.RateControl.CpuRate = (uint)(ratePercentage * 100);
                        Console.WriteLine($"为进程 {processName} 设置CPU绝对限制为: {ratePercentage}% (API值: {ratePercentage * 100})");
                        break;
                }

                int length = Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION));
                IntPtr cpuRateInfoPtr = Marshal.AllocHGlobal(length);
                Marshal.StructureToPtr(cpuRateInfo, cpuRateInfoPtr, false);
                bool success = NativeMethods.SetInformationJobObject(jobHandle,
                    JobObjectInfoType.CpuRateControlInformation,
                    cpuRateInfoPtr, (uint)length);

                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"jobHandle: {jobHandle}  cpuRateInfoPtr: {cpuRateInfoPtr}  length: {length}");
                Console.WriteLine($"设置CPU速率限制: success={success}, error={error}");

                Marshal.FreeHGlobal(cpuRateInfoPtr);
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用CPU速率限制时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 应用CPU核心限制
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="processName">进程名称（用于日志）</param>
        /// <param name="processId">进程ID</param>
        /// <param name="cpuLimit">CPU限制配置</param>
        /// <returns>操作是否成功</returns>
        private static bool ApplyCpuCoreLimit(IntPtr processHandle, string processName, int processId, CpuLimit cpuLimit)
        {
            if (processHandle == IntPtr.Zero || cpuLimit.CoreLimitType == CoreLimitType.None)
                return true;

            try
            {
                UIntPtr affinityMask = UIntPtr.Zero;
                int processorCount = Environment.ProcessorCount;

                switch (cpuLimit.CoreLimitType)
                {
                    case CoreLimitType.CoreCount:
                        // 核心数量限制：使用指定数量的核心
                        int coreCount = Math.Max(1, Math.Min(processorCount, cpuLimit.CoreCount));
                        affinityMask = GetAffinityMaskForCoreCount(coreCount);
                        Console.WriteLine($"为进程 {processName} 设置使用 {coreCount} 个CPU核心");
                        break;

                    case CoreLimitType.CoreNumber:
                        // 核心编号限制：使用指定编号的核心
                        int coreNumber = Math.Max(0, Math.Min(processorCount - 1, cpuLimit.CoreNumber));
                        affinityMask = new UIntPtr(1UL << coreNumber);
                        Console.WriteLine($"为进程 {processName} 设置使用CPU核心 #{coreNumber}");
                        break;
                }

                if (affinityMask != UIntPtr.Zero) // 设置进程亲和性掩码
                    return SetProcessAffinityMask(processHandle, processName, processId, affinityMask);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用CPU核心限制时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据核心数量获取亲和性掩码
        /// </summary>
        /// <param name="coreCount">核心数量</param>
        /// <returns>亲和性掩码</returns>
        private static UIntPtr GetAffinityMaskForCoreCount(int coreCount)
        {
            // 创建一个掩码，使用连续的coreCount个核心
            ulong mask = 0;
            for (int i = 0; i < coreCount; i++)
            {
                mask |= (1UL << i);
            }
            return new UIntPtr(mask);
        }

        /// <summary>
        /// 设置进程的亲和性掩码
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="processName">进程名称（用于日志）</param>
        /// <param name="processId">进程ID</param>
        /// <param name="affinityMask">亲和性掩码</param>
        /// <returns>操作是否成功</returns>
        private static bool SetProcessAffinityMask(IntPtr processHandle, string processName, int processId, UIntPtr affinityMask)
        {
            try
            {
                // 设置进程亲和性掩码
                bool success = NativeMethods.SetProcessAffinityMask(processHandle, affinityMask);
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    string errorMessage = new System.ComponentModel.Win32Exception(error).Message;
                    Console.WriteLine($"设置进程 {processName} (ID: {processId}) 亲和性掩码失败: 错误代码={error}, 错误信息={errorMessage}");
                    Console.WriteLine($"亲和性掩码值: 0x{affinityMask.ToUInt64():X}");
                }
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置进程亲和性掩码时出错: {ex.Message}");
                Console.WriteLine($"异常类型: {ex.GetType().Name}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 为进程的每个线程设置亲和性掩码
        /// </summary>
        /// <param name="process">目标进程</param>
        /// <param name="affinityMask">亲和性掩码</param>
        /// <returns>操作是否成功</returns>
        public static bool SetThreadsAffinityMask(Process process, UIntPtr affinityMask)
        {
            try
            {
                if (process == null)
                    return false;

                bool allSucceeded = true;
                foreach (ProcessThread thread in process.Threads)
                {
                    IntPtr threadHandle = NativeMethods.OpenThread(
                        ThreadAccess.THREAD_SET_INFORMATION | ThreadAccess.THREAD_QUERY_INFORMATION,
                        false, (uint)thread.Id);

                    if (threadHandle != IntPtr.Zero)
                    {
                        try
                        {
                            UIntPtr previousMask = NativeMethods.SetThreadAffinityMask(threadHandle, affinityMask);
                            if (previousMask == UIntPtr.Zero)
                            {
                                Console.WriteLine($"设置线程 {thread.Id} 的亲和性掩码失败: {Marshal.GetLastWin32Error()}");
                                allSucceeded = false;
                            }
                        }
                        finally
                        {
                            NativeMethods.CloseHandle(threadHandle);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"打开线程 {thread.Id} 失败: {Marshal.GetLastWin32Error()}");
                        allSucceeded = false;
                    }
                }
                return allSucceeded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置线程亲和性掩码时出错: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 提供CPU限制相关的本机方法
    /// </summary>
    internal static partial class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern bool SetProcessAffinityMask(IntPtr hProcess, UIntPtr dwProcessAffinityMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);
    }
}