using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Collections.Generic;

namespace ProcessThrottler
{
    /// <summary>
    /// 网络限制器类，提供限制进程网络带宽使用的静态方法
    /// </summary>
    public static class NetworkThrottler
    {
        /// <summary>
        /// 应用网络限制到进程组
        /// </summary>
        /// <param name="processGroup">要应用限制的进程组</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(ProcessGroup processGroup)
        {
            if (processGroup == null || !processGroup.Config.NetworkLimit.IsEnabled || 
                processGroup.Processes.Count == 0)
                return false;

            // 确认作业对象有效
            IntPtr jobHandle = processGroup.JobHandle;
            if (jobHandle == IntPtr.Zero)
            {
                Console.WriteLine($"应用网络限制失败：进程组 '{processGroup.Config.Name}' 没有有效的作业对象句柄");
                return false;
            }

            try
            {
                // 设置网络速率限制 
                var netRateInfo = new JOBOBJECT_NET_RATE_CONTROL_INFORMATION
                {
                    // 启用网络速率控制
                    ControlFlags = JOB_OBJECT_NET_RATE_CONTROL_FLAGS.JOB_OBJECT_NET_RATE_CONTROL_ENABLE,
                    // 设置最大带宽，单位为字节/秒
                    MaxBandwidth = (ulong)processGroup.Config.NetworkLimit.MaxRateLimit * 1024 // 转换为字节/秒
                };

                // 如果指定了传输优先级
                if (processGroup.Config.NetworkLimit.SpecifyTransferPriority)
                {
                    // 启用DSCP标记
                    netRateInfo.ControlFlags |= JOB_OBJECT_NET_RATE_CONTROL_FLAGS.JOB_OBJECT_NET_RATE_CONTROL_DSCP_TAG;
                    // 设置DSCP值
                    netRateInfo.DscpTag = (byte)Math.Min(63, Math.Max(0, processGroup.Config.NetworkLimit.PriorityValue));
                }

                // 设置作业对象的网络速率控制信息
                int length = Marshal.SizeOf(typeof(JOBOBJECT_NET_RATE_CONTROL_INFORMATION));
                IntPtr netRateInfoPtr = Marshal.AllocHGlobal(length);
                Marshal.StructureToPtr(netRateInfo, netRateInfoPtr, false);

                bool success = NativeMethods.SetInformationJobObject(
                    jobHandle,
                    JobObjectInfoTypeExtensions.NetRateControlInformation,
                    netRateInfoPtr,
                    (uint)length);

                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"设置网络速率限制失败: {error}");
                    
                    // 错误代码为1150表示此功能在当前操作系统版本不受支持
                    if (error == 1150)
                    {
                        Console.WriteLine("当前操作系统版本不支持作业对象网络速率控制，需要Windows 8/Server 2012或更高版本");
                    }
                    Marshal.FreeHGlobal(netRateInfoPtr);
                    return false;
                }
                else
                {
                    Console.WriteLine($"已为进程组 '{processGroup.Config.Name}' 设置最大网络带宽为: {processGroup.Config.NetworkLimit.MaxRateLimit / 1024}MB/秒");
                    if (processGroup.Config.NetworkLimit.SpecifyTransferPriority)
                    {
                        Console.WriteLine($"已为进程组 '{processGroup.Config.Name}' 设置网络优先级(DSCP)为: {netRateInfo.DscpTag}");
                    }
                }

                Marshal.FreeHGlobal(netRateInfoPtr);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用网络限制到进程组时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 应用网络限制到指定进程
        /// </summary>
        /// <param name="processHandle">进程句柄</param>
        /// <param name="processName">进程名称（用于日志）</param>
        /// <param name="processId">进程ID</param>
        /// <param name="networkLimit">网络限制配置</param>
        /// <param name="jobHandle">作业对象句柄，如果需要应用作业对象级别的限制</param>
        /// <returns>操作是否成功</returns>
        public static bool ApplyLimit(IntPtr processHandle, string processName, int processId, NetworkLimit networkLimit, IntPtr jobHandle)
        {
            if (processHandle == IntPtr.Zero || networkLimit == null || !networkLimit.IsEnabled)
                return false;

            // 确认作业对象有效
            if (jobHandle == IntPtr.Zero)
            {
                Console.WriteLine($"应用网络限制失败：无效的作业对象句柄");
                return false;
            }

            try
            {
                // 设置网络速率限制 
                var netRateInfo = new JOBOBJECT_NET_RATE_CONTROL_INFORMATION
                {
                    // 启用网络速率控制
                    ControlFlags = JOB_OBJECT_NET_RATE_CONTROL_FLAGS.JOB_OBJECT_NET_RATE_CONTROL_ENABLE,
                    // 设置最大带宽，单位为字节/秒
                    MaxBandwidth = (ulong)networkLimit.MaxRateLimit * 1024 // 转换为字节/秒
                };

                // 如果指定了传输优先级
                if (networkLimit.SpecifyTransferPriority)
                {
                    // 启用DSCP标记
                    netRateInfo.ControlFlags |= JOB_OBJECT_NET_RATE_CONTROL_FLAGS.JOB_OBJECT_NET_RATE_CONTROL_DSCP_TAG;
                    // 设置DSCP值
                    netRateInfo.DscpTag = (byte)Math.Min(63, Math.Max(0, networkLimit.PriorityValue));
                }

                // 设置作业对象的网络速率控制信息
                int length = Marshal.SizeOf(typeof(JOBOBJECT_NET_RATE_CONTROL_INFORMATION));
                IntPtr netRateInfoPtr = Marshal.AllocHGlobal(length);
                Marshal.StructureToPtr(netRateInfo, netRateInfoPtr, false);

                bool success = NativeMethods.SetInformationJobObject(
                    jobHandle,
                    JobObjectInfoTypeExtensions.NetRateControlInformation,
                    netRateInfoPtr,
                    (uint)length);

                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"设置网络速率限制失败: {error}");
                    
                    // 错误代码为1150表示此功能在当前操作系统版本不受支持
                    if (error == 1150)
                    {
                        Console.WriteLine("当前操作系统版本不支持作业对象网络速率控制，需要Windows 8/Server 2012或更高版本");
                    }
                }
                else
                {
                    Console.WriteLine($"已为进程 {processName} 设置最大网络带宽为: {networkLimit.MaxRateLimit / 1024}MB/秒");
                    if (networkLimit.SpecifyTransferPriority)
                    {
                        Console.WriteLine($"已为进程 {processName} 设置网络优先级(DSCP)为: {netRateInfo.DscpTag}");
                    }
                }

                Marshal.FreeHGlobal(netRateInfoPtr);
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用网络限制时出错: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 作业对象网络限制标志
    /// </summary>
    [Flags]
    internal enum JOB_OBJECT_NET_RATE_CONTROL_FLAGS : uint
    {
        JOB_OBJECT_NET_RATE_CONTROL_ENABLE = 0x1,
        JOB_OBJECT_NET_RATE_CONTROL_MAX_BANDWIDTH = 0x2,
        JOB_OBJECT_NET_RATE_CONTROL_DSCP_TAG = 0x4
    }

    /// <summary>
    /// 作业对象网络速率控制信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JOBOBJECT_NET_RATE_CONTROL_INFORMATION
    {
        /// <summary>
        /// 控制标志，指定启用哪些网络限制
        /// </summary>
        public JOB_OBJECT_NET_RATE_CONTROL_FLAGS ControlFlags;
        
        /// <summary>
        /// 最大带宽限制，字节/秒
        /// </summary>
        public ulong MaxBandwidth;
        
        /// <summary>
        /// DSCP值，用于网络QoS优先级标记(0-63)
        /// </summary>
        public byte DscpTag;
        
        /// <summary>
        /// 保留字段
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Reserved;
    }

    /// <summary>
    /// TCP连接信息
    /// </summary>
    internal class TcpConnectionInfo
    {
        public string LocalAddress { get; set; }
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; }
        public int RemotePort { get; set; }
        public int ProcessId { get; set; }
    }
    
    /// <summary>
    /// QoS版本结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct QOS_VERSION
    {
        public byte MajorVersion;
        public byte MinorVersion;
    }
    
    /// <summary>
    /// QoS流ID结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct QOS_FLOWID
    {
        public uint FlowId;
    }
    
    /// <summary>
    /// QoS流量速率结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct QOS_FLOW_RATE
    {
        public uint Flags;
        public uint OutgoingRate;       // 字节/秒
        public uint OutgoingRateOvertime; // 突发流量允许值(字节/秒)
    }
    
    /// <summary>
    /// QoS流量控制标志
    /// </summary>
    internal enum QOS_SHAPING : uint
    {
        QOSShapeOnly = 0x00000001,
        QOSShapeAndMark = 0x00000002,
        QOSShapeModeBestEffort = 0x00000010,
        QOSShapeModeCostBased = 0x00000020
    }
    
    /// <summary>
    /// 网络QoS相关的本机方法
    /// </summary>
    internal static partial class NativeMethods
    {
        [DllImport("qwave.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QOSCreateHandle(
            [In] ref QOS_VERSION version,
            [Out] out IntPtr qosHandle);
            
        [DllImport("qwave.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QOSCloseHandle(
            [In] IntPtr qosHandle);
            
        [DllImport("qwave.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QOSAddSocketToFlow(
            [In] IntPtr qosHandle,
            [In] IntPtr socket,
            [In] IntPtr destAddr,
            [In] uint flowRate,
            [In, Out] ref QOS_FLOWID flowId,
            [In] uint flags);
            
        [DllImport("qwave.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QOSSetFlow(
            [In] IntPtr qosHandle,
            [In] ref QOS_FLOWID flowId,
            [In] uint operation,
            [In] uint size,
            [In] IntPtr buffer,
            [In] uint flags,
            [In] IntPtr reserved);
    }
} 