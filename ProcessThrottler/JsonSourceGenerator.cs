using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ProcessThrottler;

namespace ProcessThrottler
{
    // 标记所有需要序列化的类型
    [JsonSerializable(typeof(List<ProcessConfig>))]
    [JsonSerializable(typeof(ProcessConfig))]
    [JsonSerializable(typeof(PathConfig))]
    [JsonSerializable(typeof(CpuLimit))]
    [JsonSerializable(typeof(MemoryLimit))]
    [JsonSerializable(typeof(DiskLimit))]
    [JsonSerializable(typeof(NetworkLimit))]
    [JsonSerializable(typeof(ProcessPriority))]
    
    // 设置序列化选项
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        GenerationMode = JsonSourceGenerationMode.Default)]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {
        // 源生成器会自动完成其余部分
    }
} 