using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProcessThrottler
{
    public class Core : IDisposable
    {
        private readonly System.Threading.Timer _timer;
        private List<Process> _lastProcesses;
        private bool _isFirstRun = true;
        private List<ProcessConfig> _processConfigs;
        private bool _isConfigInitialized = false;
        private readonly List<ProcessGroup> _processGroups = new List<ProcessGroup>();

        
        /// 获取或设置配置是否已初始化
        
        public bool IsConfigInitialized
        {
            get { return _isConfigInitialized; }
            set { _isConfigInitialized = value; }
        }

        
        /// 进程组变化事件
        
        public event EventHandler<List<ProcessGroup>> ProcessGroupsChanged;

        
        /// 进程变化通知事件
        
        public event EventHandler<string> ProcessNotification;

        
        /// 创建Core实例，如果提供了配置则自动初始化
        
        /// <param name="processConfigs">进程配置列表，如果为null则使用ConfigManager</param>
        public Core(List<ProcessConfig>? processConfigs = null)
        {
            _lastProcesses = new List<Process>();

            // 创建定时器，但不立即启动
            _timer = new System.Threading.Timer(CheckProcesses, null, Timeout.Infinite, Timeout.Infinite);

            // 从ConfigManager获取配置
            var configs = ConfigManager.Instance.GetProcessConfigs();
            if (configs != null && configs.Count > 0)
            {
                Console.WriteLine($"Core构造函数：从ConfigManager获取到{configs.Count}个配置项，立即初始化");
                UpdateProcessConfigs(configs);
            }
            else
            {
                Console.WriteLine("Core构造函数：未从ConfigManager获取到配置项，等待显式调用UpdateProcessConfigs");
            }

            // 订阅ConfigManager的配置变更事件
            ConfigManager.Instance.ConfigChanged += OnConfigChanged;

            // 订阅ConfigManager的配置应用事件
            ConfigManager.Instance.ConfigApplyRequested += OnConfigApplyRequested;

            // 无论配置是否初始化，直接启动监控
            _isConfigInitialized = true;
            Console.WriteLine("Core构造函数：直接启动监控");
            Start();
        }

        
        /// 配置变更事件处理
        
        private void OnConfigChanged(object sender, List<ProcessConfig> configs)
        {
            Console.WriteLine($"Core：收到配置变更通知，有{configs.Count}个配置项");
            UpdateProcessConfigs(configs);
        }

        
        /// 配置应用请求事件处理
        
        private void OnConfigApplyRequested(object sender, EventArgs e)
        {
            Console.WriteLine("Core：收到配置应用请求");

            // 重新从ConfigManager获取最新配置
            var configs = ConfigManager.Instance.GetProcessConfigs();
            UpdateProcessConfigs(configs);

            // 立即重新检查进程和应用限制
            CheckProcesses(null);
        }

        
        /// 获取当前进程组列表
        
        public List<ProcessGroup> GetProcessGroups()
        {
            return _processGroups;
        }

        
        /// 更新进程配置，首次调用时会启动监控循环
        
        public void UpdateProcessConfigs(List<ProcessConfig> processConfigs)
        {
            if (processConfigs == null || processConfigs.Count == 0)
            {
                Console.WriteLine("UpdateProcessConfigs: 接收到空配置，忽略");
                return;
            }

            Console.WriteLine($"UpdateProcessConfigs: 接收到{processConfigs.Count}个配置项");
            _processConfigs = processConfigs;

            // 获取最新的进程列表
            if (_lastProcesses.Count == 0)
            {
                _lastProcesses = GetProcesses();
                _isFirstRun = false;
                Console.WriteLine($"UpdateProcessConfigs: 初始化进程列表，获取到{_lastProcesses.Count}个进程");
            }

            // 重新构建进程组
            UpdateProcessGroups();

            Console.WriteLine("UpdateProcessConfigs: 配置已更新");
        }

        
        /// 更新进程组
        
        private void UpdateProcessGroups()
        {
            // 释放现有进程组资源
            foreach (var group in _processGroups)
            {
                group.Dispose();
            }

            // 清空当前进程组
            _processGroups.Clear();

            // 当前没有配置或进程，无需处理
            if (_processConfigs == null || _processConfigs.Count == 0 || _lastProcesses == null || _lastProcesses.Count == 0)
                return;

            // 为每个配置创建进程组
            foreach (var config in _processConfigs)
            {
                var group = new ProcessGroup(config);

                // 只处理启用的配置
                if (config.IsEnabled)
                {
                    bool hasAddedProcess = false;
                    
                    // 查找所有匹配的进程
                    foreach (var path in config.Paths)
                    {
                        var matchingProcesses = FindProcessesByPath(_lastProcesses, path.FilePath);
                        foreach (var process in matchingProcesses)
                        {
                            // 使用AddProcess方法添加进程到组并分配到作业对象
                            group.AddProcess(process);
                            hasAddedProcess = true;
                        }
                    }
                    
                    // 如果有进程被添加到组中，标记该组为新组
                    if (hasAddedProcess)
                    {
                        Console.WriteLine($"配置组 '{group.Config.Name}' 有匹配的进程，标记为新组");
                        group.IsNew = true;
                    }
                }

                _processGroups.Add(group);
            }

            // 触发进程组变化事件
            ProcessGroupsChanged?.Invoke(this, _processGroups);

            // 输出调试信息
            LogProcessGroups();
        }

        
        /// 记录进程组信息到调试输出
        
        private void LogProcessGroups()
        {
            Console.WriteLine("==================== 进程组信息 ====================");
            foreach (var group in _processGroups)
            {
                Console.WriteLine($"配置组: {group.Config.Name} (启用状态: {group.Config.IsEnabled})");
                if (group.Processes.Count == 0)
                {
                    Console.WriteLine("  - 无匹配进程");
                }
                else
                {
                    foreach (var process in group.Processes)
                    {
                        try
                        {
                            Console.WriteLine($"  - {process.ProcessName} (ID: {process.Id})");
                        }
                        catch { }
                    }
                }
            }
            Console.WriteLine("===================================================");
        }

        
        /// 启动进程监控
        
        public void Start()
        {
            _timer.Change(0, 300);
            Console.WriteLine("进程监控已启动");
        }

        
        /// 停止进程监控
        
        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            Console.WriteLine("进程监控已停止");
        }

        
        /// 获取当前进程快照
        
        private List<Process> GetProcesses()
        {
            return Process.GetProcesses().ToList();
        }

        
        /// <summary>
        /// 检查进程变化
        /// </summary>
        private void CheckProcesses(object state)
        {
            try
            {
                var currentProcesses = GetProcesses();

                // 第一次运行时只保存进程列表，不比较
                if (_isFirstRun)
                {
                    _lastProcesses = currentProcesses;
                    _isFirstRun = false;

                    // 初始化进程组
                    UpdateProcessGroups();

                    // 显示初始进程组信息
                    ShowProcessGroupsNotification("初始进程组信息");
                    return;
                }

                // 获取新增的进程
                var newProcesses = currentProcesses
                    .Where(current => !_lastProcesses
                        .Any(last => last.Id == current.Id))
                    .ToList();

                // 获取已结束的进程
                var endedProcesses = _lastProcesses
                    .Where(last => !currentProcesses
                        .Any(current => current.Id == last.Id))
                    .ToList();

                // 如果有进程变化
                if (newProcesses.Count > 0 || endedProcesses.Count > 0)
                {
                    // 更新上一次的进程列表
                    _lastProcesses = currentProcesses;

                    // 更新进程组
                    UpdateProcessGroups();

                    // 显示进程变化和进程组信息
                    StringBuilder message = new StringBuilder();

                    // 添加进程变化信息
                    if (newProcesses.Count > 0)
                    {
                        message.AppendLine("【新增进程】");
                        foreach (var process in newProcesses)
                        {
                            try
                            {
                                message.AppendLine($"- {process.ProcessName} (ID: {process.Id})");
                            }
                            catch { }
                        }
                        message.AppendLine();
                    }

                    if (endedProcesses.Count > 0)
                    {
                        message.AppendLine("【结束进程】");
                        foreach (var process in endedProcesses)
                        {
                            try
                            {
                                message.AppendLine($"- {process.ProcessName} (ID: {process.Id})");
                            }
                            catch { }
                        }
                        message.AppendLine();
                    }

                    ShowProcessGroupsNotification(message.ToString());
                    
                    // 只有在进程有变化时才应用进程限制
                    ApplyProcessLimits();
                }
                else
                {
                    // 如果没有进程变动，不需要执行任何操作
                    Console.WriteLine("没有进程变动，跳过应用限制操作");
                }
            }
            catch (Exception ex)
            {
                // 记录异常但不中断监控
                Console.WriteLine($"进程监控异常: {ex.Message}");
            }
        }

        
        /// <summary>
        /// 应用进程限制
        /// </summary>
        private void ApplyProcessLimits()
        {
            // 检查是否有任何新的进程组
            bool hasAnyNewGroup = _processGroups.Any(g => g.IsNew);
            
            if (!hasAnyNewGroup)
            {
                Console.WriteLine("没有发现新匹配的进程组，跳过应用限制操作");
                return;
            }
            
            Console.WriteLine("发现新匹配的进程组，正在应用限制...");
            
            // 对每个进程组应用限制，但只处理标记为新的组
            foreach (var group in _processGroups)
            {
                // 只处理启用的配置组且标记为新的组
                if (group.Config.IsEnabled && group.Processes.Count > 0 && group.IsNew)
                {
                    Console.WriteLine($"为配置组 '{group.Config.Name}' 应用限制");
                    // 调用进程组的ApplyLimits方法
                    group.ApplyLimits();
                }
            }
        }

        // 根据路径查找进程
        private List<Process> FindProcessesByPath(List<Process> processes, string filePath)
        {
            var result = new List<Process>();

            foreach (var process in processes)
            {
                try{
                    if (process.MainModule?.FileName?.ToLower() == filePath.ToLower())
                        result.Add(process);
                }
                catch{
                    // 访问某些系统进程可能会抛出异常，忽略这些进程
                }
            }

            return result;
        }

        
        /// 显示进程组信息到弹窗
        
        private void ShowProcessGroupsNotification(string preMessage = "")
        {
            StringBuilder message = new StringBuilder();

            // 添加前置消息
            if (!string.IsNullOrEmpty(preMessage))
            {
                message.AppendLine(preMessage);
                message.AppendLine();
            }

            // 添加进程组信息
            message.AppendLine("==================== 进程组详情 ====================");
            message.AppendLine();

            if (_processGroups.Count == 0)
            {
                message.AppendLine("当前没有进程组。");
            }
            else
            {
                foreach (var group in _processGroups)
                {
                    message.Append(group.GetDetailInfo());
                    message.AppendLine();
                }
            }

            message.AppendLine("===================================================");

            // 触发通知事件
            ProcessNotification?.Invoke(this, message.ToString());

            // 输出到调试日志
            Console.WriteLine(message.ToString());
        }

        
        /// 释放资源
        
        public void Dispose()
        {
            _timer?.Dispose();

            // 释放所有进程组资源
            foreach (var group in _processGroups)
                group.Dispose();
            

            // 清理进程组
            _processGroups.Clear();

            // 清理进程列表
            foreach (var process in _lastProcesses)
            {
                try
                {
                    process.Dispose();
                }
                catch { }
            }
            _lastProcesses.Clear();
        }
    }
}