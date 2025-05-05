using System;
using System.Collections.Generic;

namespace ProcessThrottler
{
    public class ProcessConfig
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        public List<PathConfig> Paths { get; set; } = new List<PathConfig>();
        public CpuLimit CpuLimit { get; set; } = new CpuLimit();
        public MemoryLimit MemoryLimit { get; set; } = new MemoryLimit();
        public DiskLimit DiskLimit { get; set; } = new DiskLimit();
        public NetworkLimit NetworkLimit { get; set; } = new NetworkLimit();
        public ProcessPriority ProcessPriority { get; set; } = new ProcessPriority();
    }

    public class PathConfig
    {
        public string FilePath { get; set; }
        public List<string> Parameters { get; set; } = new List<string>();
    }

    public class CpuLimit
    {
        public bool IsEnabled { get; set; }
        
        public CpuLimitType LimitType { get; set; }
        public int RelativeWeight { get; set; }
        public int RatePercentage { get; set; }
        
        public CoreLimitType CoreLimitType { get; set; }
        public int CoreCount { get; set; }
        public int CoreNumber { get; set; }
    }

    public enum CpuLimitType
    {
        None,
        RelativeWeight,
        AbsoluteRate
    }

    public enum CoreLimitType
    {
        None,
        CoreCount,
        CoreNumber
    }

    public class MemoryLimit
    {
        public bool IsEnabled { get; set; }
        public MemoryOveruseAction OveruseAction { get; set; }
        public int MemoryUsageLimit { get; set; }
        public bool EnableMonitoring { get; set; } = true;
        public bool EnableAutoTrim { get; set; } = false;
        public bool TerminateOnExceed { get; set; } = false;
    }

    public enum MemoryOveruseAction
    {
        TransferToPageFile,
        TerminateProcess
    }

    public class DiskLimit
    {
        public bool IsEnabled { get; set; }
        public int ReadWriteRateLimit { get; set; }
    }

    public class NetworkLimit
    {
        public bool IsEnabled { get; set; }
        public int MaxRateLimit { get; set; }
        public bool SpecifyTransferPriority { get; set; }
        public int PriorityValue { get; set; }
    }

    public class ProcessPriority
    {
        public bool IsEnabled { get; set; }
        public int PriorityValue { get; set; }
    }
} 