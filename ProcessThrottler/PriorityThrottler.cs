using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ProcessThrottler
{
    /// <summary>
    /// 进程优先级限制器，提供限制进程优先级的静态方法
    /// </summary>
    public static class PriorityThrottler
    {
        /// <summary>
        /// 应用优先级限制到进程组
        /// </summary>
        /// <param name="processGroup">要应用限制的进程组</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(ProcessGroup processGroup)
        {
            if (processGroup == null || !processGroup.Config.ProcessPriority.IsEnabled || 
                processGroup.Processes.Count == 0)
                return false;

            bool success = true;

            // 循环应用优先级限制到每个进程
            foreach (var process in processGroup.Processes)
            {
                try
                {
                    string processName = process.ProcessName;
                    int processId = process.Id;

                    // 转换优先级值为ProcessPriorityClass枚举
                    ProcessPriorityClass priorityClass = GetPriorityClass(processGroup.Config.ProcessPriority.PriorityValue);
                    
                    // 设置进程优先级
                    process.PriorityClass = priorityClass;
                    
                    Console.WriteLine($"已为进程 {processName} (ID: {processId}) 设置优先级为: {priorityClass}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"对进程 {process.ProcessName} (ID: {process.Id}) 应用优先级限制时出错: {ex.Message}");
                    success = false;
                }
            }

            if (success)
                Console.WriteLine($"成功为进程组 '{processGroup.Config.Name}' 应用优先级限制");
            else
                Console.WriteLine($"部分进程组 '{processGroup.Config.Name}' 的优先级限制应用失败");

            return success;
        }

        /// <summary>
        /// 应用优先级限制到指定进程
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="process">进程对象</param>
        /// <param name="processName">进程名称（用于日志）</param>
        /// <param name="processId">进程ID</param>
        /// <param name="priorityConfig">优先级配置</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(IntPtr processHandle, Process process, string processName, int processId, ProcessPriority priorityConfig)
        {
            if (process == null || !priorityConfig.IsEnabled)
                return false;

            try
            {
                // 转换优先级值为ProcessPriorityClass枚举
                ProcessPriorityClass priorityClass = GetPriorityClass(priorityConfig.PriorityValue);
                
                // 设置进程优先级
                process.PriorityClass = priorityClass;
                
                Console.WriteLine($"已为进程 {processName} (ID: {processId}) 设置优先级为: {priorityClass}");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用优先级限制时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将配置中的优先级值转换为ProcessPriorityClass枚举
        /// </summary>
        /// <param name="priorityValue">优先级值(0-5)</param>
        /// <returns>对应的ProcessPriorityClass枚举值</returns>
        private static ProcessPriorityClass GetPriorityClass(int priorityValue)
        {
            switch (priorityValue)
            {
                case 0: return ProcessPriorityClass.Idle;
                case 1: return ProcessPriorityClass.BelowNormal;
                case 2: return ProcessPriorityClass.Normal;
                case 3: return ProcessPriorityClass.AboveNormal;
                case 4: return ProcessPriorityClass.High;
                case 5: return ProcessPriorityClass.RealTime;
                default: return ProcessPriorityClass.Normal;
            }
        }
    }
} 