using System;
using System.Runtime.InteropServices;

using System.Diagnostics;

namespace ProcessThrottler
{
    /// <summary>
    /// 作业对象信息类型
    /// </summary>
    internal enum JobObjectInfoType
    {
        BasicLimitInformation = 2,
        BasicUIRestrictions = 4,
        ExtendedLimitInformation = 9,
        CpuRateControlInformation = 15
    }

    /// <summary>
    /// CPU限制标志
    /// </summary>
    [Flags]
    internal enum JOB_OBJECT_CPU_RATE_CONTROL : uint
    {
        JOB_OBJECT_CPU_RATE_CONTROL_ENABLE = 0x1,
        JOB_OBJECT_CPU_RATE_CONTROL_WEIGHT_BASED = 0x2,
        JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP = 0x4,
        JOB_OBJECT_CPU_RATE_CONTROL_NOTIFY = 0x8
    }

    /// <summary>
    /// 作业对象限制标志
    /// </summary>
    [Flags]
    internal enum JOB_OBJECT_LIMIT : uint
    {
        JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100,
        JOB_OBJECT_LIMIT_JOB_MEMORY = 0x00000200,
        JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION = 0x00000400,
        JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800,
        JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK = 0x00001000,
        JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000
    }

    /// <summary>
    /// 作业对象基本限制信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public JOB_OBJECT_LIMIT LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    /// <summary>
    /// IO计数信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct IO_COUNTERS
    {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }

    /// <summary>
    /// 作业对象扩展限制信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    /// <summary>
    /// 作业对象CPU限制信息
    /// </summary>


    [StructLayout(LayoutKind.Sequential)]
    public struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
    {
        public uint ControlFlags;

        [StructLayout(LayoutKind.Explicit)]
        public struct DUMMYUNIONNAME
        {
            [FieldOffset(0)]
            public uint CpuRate;

            [FieldOffset(0)]
            public uint Weight;

            [StructLayout(LayoutKind.Sequential)]
            public struct DUMMYSTRUCTNAME
            {
                public ushort MinRate;
                public ushort MaxRate;
            }

            [FieldOffset(0)]
            public DUMMYSTRUCTNAME MinMaxRate;
        }

        public DUMMYUNIONNAME RateControl;
    }

    /// <summary>
    /// 线程访问权限标志
    /// </summary>
    [Flags]
    internal enum ThreadAccess : uint
    {
        THREAD_TERMINATE = 0x0001,
        THREAD_SUSPEND_RESUME = 0x0002,
        THREAD_GET_CONTEXT = 0x0008,
        THREAD_SET_CONTEXT = 0x0010,
        THREAD_QUERY_INFORMATION = 0x0040,
        THREAD_SET_INFORMATION = 0x0020,
        THREAD_SET_THREAD_TOKEN = 0x0080,
        THREAD_IMPERSONATE = 0x0100,
        THREAD_DIRECT_IMPERSONATION = 0x0200,
        THREAD_ALL_ACCESS = 0x1F03FF
    }

    /// <summary>
    /// 作业对象信息类型扩展
    /// </summary>
    internal static partial class JobObjectInfoTypeExtensions
    {
        /// <summary>
        /// 网络速率控制信息
        /// </summary>
        public const JobObjectInfoType NetRateControlInformation = (JobObjectInfoType)32;
    }

    /// <summary>
    /// 提供与Windows API交互的本机方法
    /// </summary>
    internal static partial class NativeMethods
    {
        // ====== 作业对象相关方法 ======
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateJobObject([In] IntPtr lpJobAttributes, [In] string lpName);

        [DllImport("kernel32.dll")]
        public static extern bool SetInformationJobObject(
            [In] IntPtr hJob,
            [In] JobObjectInfoType infoType,
            [In] IntPtr lpJobObjectInfo,
            [In] uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll")]
        public static extern bool AssignProcessToJobObject([In] IntPtr hJob, [In] IntPtr hProcess);

        // ====== 线程相关方法 ======
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint ResumeThread(IntPtr hThread);

        // ====== 通用系统方法 ======
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}