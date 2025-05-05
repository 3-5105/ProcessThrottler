using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;

namespace ProcessThrottler
{
    /// <summary>
    /// 配置管理器类，统一管理程序配置
    /// </summary>
    public class ConfigManager
    {
        // 单例实例
        private static ConfigManager _instance;
        private static readonly object _lock = new object();
        
        // 配置文件路径
        private readonly string _configFilePath;
        
        // 进程配置列表
        private List<ProcessConfig> _processConfigs;
        
        // 配置初始化状态
        private bool _isInitialized = false;
        
        // 配置变更事件
        public event EventHandler<List<ProcessConfig>> ConfigChanged;
        
        /// <summary>
        /// 配置应用请求事件 - 当需要应用配置时触发
        /// </summary>
        public event EventHandler ConfigApplyRequested;
        
        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigManager();
                        }
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// 获取配置是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// 获取进程配置列表
        /// </summary>
        public List<ProcessConfig> ProcessConfigs => _processConfigs;
        
        /// <summary>
        /// 私有构造函数，确保单例模式
        /// </summary>
        private ConfigManager()
        {
            // 获取应用程序所在目录
            
            // 设置配置文件为自身文件夹下的config.json
            _configFilePath = Path.Combine( System.Environment.CurrentDirectory, "config.json");
            Console.WriteLine($"ConfigManager: 配置文件路径: {_configFilePath}");
           
            // 尝试加载配置
            _processConfigs = LoadConfigsFromDisk();
            
            // 如果加载失败或没有现有配置，创建默认配置
            if (_processConfigs == null || _processConfigs.Count == 0)
            {
                _processConfigs = CreateDefaultConfigs();
                SaveConfigsToDisk();
            }
            
            // 标记配置已初始化
            _isInitialized = true;
        }
        
        /// <summary>
        /// 获取进程配置列表
        /// </summary>
        public List<ProcessConfig> GetProcessConfigs()
        {
            return _processConfigs;
        }
        
        /// <summary>
        /// 更新进程配置列表
        /// </summary>
        public void UpdateProcessConfigs(List<ProcessConfig> configs)
        {
            if (configs == null || configs.Count == 0)
            {
                Console.WriteLine("ConfigManager: 忽略空配置更新");
                return;
            }
            
            _processConfigs = configs;
            _isInitialized = true;
            
            // 通知配置已变更
            ConfigChanged?.Invoke(this, _processConfigs);
            
            Console.WriteLine($"ConfigManager: 已更新{configs.Count}个配置项");
        }
        
        /// <summary>
        /// 请求应用当前配置
        /// </summary>
        public void ApplyConfigs()
        {
            // 触发配置应用请求事件
            ConfigApplyRequested?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// 从磁盘加载配置
        /// </summary>
        private List<ProcessConfig> LoadConfigsFromDisk()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string jsonString = File.ReadAllText(_configFilePath);
                    // 使用源生成器进行反序列化
                    var configs = JsonSerializer.Deserialize(jsonString, AppJsonSerializerContext.Default.ListProcessConfig);
                    Console.WriteLine($"已从 {_configFilePath} 加载 {configs.Count} 条配置");
                    return configs;
                }
                else
                {
                    Console.WriteLine($"ConfigManager: 配置文件不存在: {_configFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConfigManager: 加载配置失败: {ex.Message}");
                // 如果发生错误，尝试写入日志
                try 
                {
                    string logPath = Path.Combine(Path.GetDirectoryName(_configFilePath), "error.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] 加载配置失败: {ex.Message}\r\n");
                } 
                catch 
                {
                    // 忽略日志写入失败
                }
            }
            
            return new List<ProcessConfig>();
        }
        
        /// <summary>
        /// 初始化默认配置
        /// </summary>
        private List<ProcessConfig> CreateDefaultConfigs()
        {
            var defaultConfigs = new List<ProcessConfig>
            {
                new ProcessConfig
                {
                    Name = "默认配置",
                    IsEnabled = false,
                    Paths = new List<PathConfig>
                    {
                        new PathConfig { FilePath = "C:\\Windows\\System32\\notepad.exe" }
                    },
                    ProcessPriority = new ProcessPriority
                    {
                        IsEnabled = true,
                        PriorityValue = 1 // BelowNormal
                    }
                }
            };
            
            Console.WriteLine("已创建默认配置");
            return defaultConfigs;
        }
        
        /// <summary>
        /// 保存配置到磁盘
        /// </summary>
        public bool SaveConfigsToDisk()
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // 使用源生成器进行序列化
                string jsonString = JsonSerializer.Serialize(_processConfigs, AppJsonSerializerContext.Default.ListProcessConfig);
                
                // 安全写入（先写临时文件，成功后替换）
                string tempFile = _configFilePath + ".tmp";
                File.WriteAllText(tempFile, jsonString);
                
                if (File.Exists(_configFilePath))
                {
                    // 创建备份
                    string backupFile = _configFilePath + ".bak";
                    if (File.Exists(backupFile))
                    {
                        File.Delete(backupFile);
                    }
                    File.Move(_configFilePath, backupFile);
                }
                
                File.Move(tempFile, _configFilePath);
                
                Console.WriteLine("ConfigManager: 配置已成功保存到磁盘");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ConfigManager: 保存配置失败: {ex.Message}");
                
                // 记录错误到日志
                try
                {
                    string logPath = Path.Combine(Path.GetDirectoryName(_configFilePath), "error.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] 保存配置失败: {ex.Message}\r\n");
                }
                catch
                {
                    // 忽略日志创建失败
                }
                
                return false;
            }
        }
    }
} 