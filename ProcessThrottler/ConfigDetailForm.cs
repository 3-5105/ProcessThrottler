using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic.Devices;
using System.Linq;

namespace ProcessThrottler
{
    public class ConfigDetailForm : Form
    {
        private ProcessConfig _config;
        private TabControl tabControl;
        private Button btnSave;
        private Button btnCancel;
        private CheckBox chkEnabled;
        private TextBox txtName;
        
        // 路径配置相关控件
        private ListView listViewPaths;
        private Button btnAddPath;
        private Button btnRemovePath;
        
        // CPU限制相关控件
        private CheckBox chkCpuEnabled;
        private ComboBox cmbCpuLimitType;
        private NumericUpDown numRelativeWeight;
        private NumericUpDown numRatePercentage;
        private ComboBox cmbCoreLimitType;
        private NumericUpDown numCoreCount;
        private NumericUpDown numCoreNumber;
        
        // 内存限制相关控件
        private CheckBox chkMemoryEnabled;
        private ComboBox cmbMemoryAction;
        private NumericUpDown numMemoryLimit;
        private CheckBox chkMemoryMonitoring;
        private CheckBox chkAutoTrim;
        private CheckBox chkTerminateOnExceed;
        
        // 磁盘限制相关控件
        private CheckBox chkDiskEnabled;
        private NumericUpDown numDiskRate;
        
        // 网络限制相关控件
        private CheckBox chkNetworkEnabled;
        private NumericUpDown numNetworkRate;
        private CheckBox chkPriorityEnabled;
        private NumericUpDown numPriority;
        
        // 进程优先级相关控件
        private CheckBox chkProcessPriorityEnabled;
        private NumericUpDown numProcessPriority;

        public ConfigDetailForm(ProcessConfig config)
        {
            _config = config;
            
            InitializeComponents();
            LoadConfigData();
        }

        private void InitializeComponents()
        {
            this.Text = "配置详细信息";
            this.Size = new System.Drawing.Size(600, 550);
            this.MinimumSize = new System.Drawing.Size(600, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            
            Label lblName = new Label
            {
                Text = "配置名称:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };
            this.Controls.Add(lblName);
            
            txtName = new TextBox
            {
                Location = new System.Drawing.Point(100, 17),
                Size = new System.Drawing.Size(200, 23)
            };
            this.Controls.Add(txtName);

            chkEnabled = new CheckBox
            {
                Text = "启用此配置",
                Location = new System.Drawing.Point(320, 17),
                AutoSize = true
            };
            chkEnabled.CheckedChanged += ChkEnabled_CheckedChanged;
            this.Controls.Add(chkEnabled);

            tabControl = new TabControl
            {
                Location = new System.Drawing.Point(20, 50),
                Size = new System.Drawing.Size(550, 420)
            };
            
            tabControl.TabPages.Add(CreatePathsTab());
            tabControl.TabPages.Add(CreateCpuTab());
            tabControl.TabPages.Add(CreateMemoryTab());
            tabControl.TabPages.Add(CreateDiskTab());
            tabControl.TabPages.Add(CreateNetworkTab());
            tabControl.TabPages.Add(CreateProcessPriorityTab());
            
            this.Controls.Add(tabControl);

            btnSave = new Button
            {
                Text = "保存",
                Location = new System.Drawing.Point(310, 480),
                Size = new System.Drawing.Size(75, 23),
                DialogResult = DialogResult.OK
            };
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new System.Drawing.Point(490, 480),
                Size = new System.Drawing.Size(75, 23),
                DialogResult = DialogResult.Cancel
            };
            
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private TabPage CreatePathsTab()
        {
            TabPage pathsTab = new TabPage("路径配置");
            
            listViewPaths = new ListView
            {
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(520, 320),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            
            listViewPaths.Columns.Add("执行文件路径", 300);
            listViewPaths.Columns.Add("参数", 200);
            listViewPaths.MouseDoubleClick += ListViewPaths_MouseDoubleClick;
            
            btnAddPath = new Button
            {
                Text = "添加路径",
                Location = new System.Drawing.Point(10, 340),
                Size = new System.Drawing.Size(80, 23)
            };
            btnAddPath.Click += BtnAddPath_Click;
            
            btnRemovePath = new Button
            {
                Text = "删除路径",
                Location = new System.Drawing.Point(100, 340),
                Size = new System.Drawing.Size(80, 23)
            };
            btnRemovePath.Click += BtnRemovePath_Click;
            
            pathsTab.Controls.Add(listViewPaths);
            pathsTab.Controls.Add(btnAddPath);
            pathsTab.Controls.Add(btnRemovePath);
            
            return pathsTab;
        }

        private TabPage CreateCpuTab()
        {
            TabPage cpuTab = new TabPage("CPU限制");
            
            chkCpuEnabled = new CheckBox
            {
                Text = "启用CPU限制",
                Location = new System.Drawing.Point(10, 10),
                AutoSize = true
            };
            chkCpuEnabled.CheckedChanged += ChkCpuEnabled_CheckedChanged;
            
            // CPU限制类型设置
            GroupBox grpCpuLimit = new GroupBox
            {
                Text = "CPU使用率限制",
                Location = new System.Drawing.Point(10, 40),
                Size = new System.Drawing.Size(520, 120)
            };
            
            Label lblCpuLimitType = new Label
            {
                Text = "限制类型:",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true
            };
            
            cmbCpuLimitType = new ComboBox
            {
                Location = new System.Drawing.Point(100, 22),
                Size = new System.Drawing.Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCpuLimitType.Items.AddRange(new object[] { "不限制", "相对权重限制", "绝对速率限制" });
            cmbCpuLimitType.SelectedIndexChanged += CmbCpuLimitType_SelectedIndexChanged;
            
            Label lblRelativeWeight = new Label
            {
                Text = "相对权重:",
                Location = new System.Drawing.Point(10, 55),
                AutoSize = true
            };
            
            numRelativeWeight = new NumericUpDown
            {
                Location = new System.Drawing.Point(100, 53),
                Size = new System.Drawing.Size(80, 23),
                Minimum = 1,
                Maximum = 9,
                Value = 5
            };
            
            Label lblRelativeWeightInfo = new Label
            {
                Text = "(1最高，9最低)",
                Location = new System.Drawing.Point(190, 55),
                AutoSize = true,
                Font = new System.Drawing.Font(this.Font.FontFamily, this.Font.Size * 0.9f)
            };
            
            Label lblRatePercentage = new Label
            {
                Text = "速率百分比:",
                Location = new System.Drawing.Point(10, 85),
                AutoSize = true
            };
            
            numRatePercentage = new NumericUpDown
            {
                Location = new System.Drawing.Point(100, 83),
                Size = new System.Drawing.Size(80, 23),
                Minimum = 1,
                Maximum = 100,
                Value = 50
            };
            
            grpCpuLimit.Controls.Add(lblCpuLimitType);
            grpCpuLimit.Controls.Add(cmbCpuLimitType);
            grpCpuLimit.Controls.Add(lblRelativeWeight);
            grpCpuLimit.Controls.Add(numRelativeWeight);
            grpCpuLimit.Controls.Add(lblRelativeWeightInfo);
            grpCpuLimit.Controls.Add(lblRatePercentage);
            grpCpuLimit.Controls.Add(numRatePercentage);
            
            // CPU核心限制设置
            GroupBox grpCoreLimit = new GroupBox
            {
                Text = "CPU核心限制",
                Location = new System.Drawing.Point(10, 170),
                Size = new System.Drawing.Size(520, 120)
            };
            
            Label lblCoreLimitType = new Label
            {
                Text = "限制类型:",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true
            };
            
            cmbCoreLimitType = new ComboBox
            {
                Location = new System.Drawing.Point(100, 22),
                Size = new System.Drawing.Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCoreLimitType.Items.AddRange(new object[] { "不限制", "核心数量限制", "核心编号限制" });
            cmbCoreLimitType.SelectedIndexChanged += CmbCoreLimitType_SelectedIndexChanged;
            
            Label lblCoreCount = new Label
            {
                Text = "核心数量:",
                Location = new System.Drawing.Point(10, 55),
                AutoSize = true
            };
            
            numCoreCount = new NumericUpDown
            {
                Location = new System.Drawing.Point(100, 53),
                Size = new System.Drawing.Size(80, 23),
                Minimum = 1,
                Maximum = Environment.ProcessorCount,
                Value = 1
            };
            
            Label lblCoreNumber = new Label
            {
                Text = "核心编号:",
                Location = new System.Drawing.Point(10, 85),
                AutoSize = true
            };
            
            numCoreNumber = new NumericUpDown
            {
                Location = new System.Drawing.Point(100, 83),
                Size = new System.Drawing.Size(80, 23),
                Minimum = 0,
                Maximum = Environment.ProcessorCount - 1,
                Value = 0
            };
            
            grpCoreLimit.Controls.Add(lblCoreLimitType);
            grpCoreLimit.Controls.Add(cmbCoreLimitType);
            grpCoreLimit.Controls.Add(lblCoreCount);
            grpCoreLimit.Controls.Add(numCoreCount);
            grpCoreLimit.Controls.Add(lblCoreNumber);
            grpCoreLimit.Controls.Add(numCoreNumber);
            
            cpuTab.Controls.Add(chkCpuEnabled);
            cpuTab.Controls.Add(grpCpuLimit);
            cpuTab.Controls.Add(grpCoreLimit);
            
            return cpuTab;
        }

        private TabPage CreateMemoryTab()
        {
            TabPage memoryTab = new TabPage("内存限制");
            
            chkMemoryEnabled = new CheckBox
            {
                Text = "启用内存限制",
                Location = new System.Drawing.Point(10, 10),
                AutoSize = true
            };
            chkMemoryEnabled.CheckedChanged += ChkMemoryEnabled_CheckedChanged;
            
            Label lblMemoryAction = new Label
            {
                Text = "超限操作:",
                Location = new System.Drawing.Point(10, 50),
                AutoSize = true
            };
            
            cmbMemoryAction = new ComboBox
            {
                Location = new System.Drawing.Point(140, 47),
                Size = new System.Drawing.Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMemoryAction.Items.AddRange(new object[] { "转移到分页文件", "结束进程" });
            
            Label lblMemoryLimit = new Label
            {
                Text = "内存限制 (MB):",
                Location = new System.Drawing.Point(10, 90),
                AutoSize = true
            };
            
            ulong totalMemoryMB = GetTotalPhysicalMemory() / (1024 * 1024); // 获取物理内存总量(MB)
            
            numMemoryLimit = new NumericUpDown
            {
                Location = new System.Drawing.Point(140, 88),
                Size = new System.Drawing.Size(120, 23),
                Minimum = 16,
                Maximum = (decimal)Math.Min(totalMemoryMB, decimal.MaxValue), // 确保不超过decimal最大值
                Value = Math.Min(1024, (decimal)(totalMemoryMB / 2)) // 设置为系统内存的一半或1024MB
            };
            
            // 添加内存监控选项
            chkMemoryMonitoring = new CheckBox
            {
                Text = "启用内存监控",
                Location = new System.Drawing.Point(10, 130),
                AutoSize = true
            };
            chkMemoryMonitoring.CheckedChanged += ChkMemoryMonitoring_CheckedChanged;
            
            chkAutoTrim = new CheckBox
            {
                Text = "自动内存整理",
                Location = new System.Drawing.Point(30, 160),
                AutoSize = true,
                Enabled = false // 默认禁用，直到启用内存监控
            };
            
            chkTerminateOnExceed = new CheckBox
            {
                Text = "超限自动终止",
                Location = new System.Drawing.Point(30, 190),
                AutoSize = true,
                Enabled = false // 默认禁用，直到启用内存监控
            };
            
            memoryTab.Controls.Add(chkMemoryEnabled);
            memoryTab.Controls.Add(lblMemoryAction);
            memoryTab.Controls.Add(cmbMemoryAction);
            memoryTab.Controls.Add(lblMemoryLimit);
            memoryTab.Controls.Add(numMemoryLimit);
            memoryTab.Controls.Add(chkMemoryMonitoring);
            memoryTab.Controls.Add(chkAutoTrim);
            memoryTab.Controls.Add(chkTerminateOnExceed);
            
            return memoryTab;
        }

        private void ChkMemoryMonitoring_CheckedChanged(object sender, EventArgs e)
        {
            // 只有在启用内存监控时才启用相关选项
            chkAutoTrim.Enabled = chkMemoryMonitoring.Checked;
            chkTerminateOnExceed.Enabled = chkMemoryMonitoring.Checked;
        }

        // 获取系统物理内存总量(字节)
        private ulong GetTotalPhysicalMemory()
        {
            try
            {
                return new ComputerInfo().TotalPhysicalMemory;
            }
            catch
            {
                return 8UL * 1024 * 1024 * 1024; // 默认8GB
            }
        }

        private TabPage CreateDiskTab()
        {
            TabPage diskTab = new TabPage("磁盘限制");
            
            chkDiskEnabled = new CheckBox
            {
                Text = "启用磁盘限制",
                Location = new System.Drawing.Point(10, 10),
                AutoSize = true
            };
            chkDiskEnabled.CheckedChanged += ChkDiskEnabled_CheckedChanged;
            
            Label lblDiskRate = new Label
            {
                Text = "读写速率限制 (MB/s):",
                Location = new System.Drawing.Point(10, 50),
                AutoSize = true
            };
            
            numDiskRate = new NumericUpDown
            {
                Location = new System.Drawing.Point(160, 48),
                Size = new System.Drawing.Size(120, 23),
                Minimum = 1,
                Maximum = 100000,
                Value = 50
            };
            
            diskTab.Controls.Add(chkDiskEnabled);
            diskTab.Controls.Add(lblDiskRate);
            diskTab.Controls.Add(numDiskRate);
            
            return diskTab;
        }

        private TabPage CreateNetworkTab()
        {
            TabPage networkTab = new TabPage("网络限制");
            
            chkNetworkEnabled = new CheckBox
            {
                Text = "启用网络限制",
                Location = new System.Drawing.Point(10, 10),
                AutoSize = true
            };
            chkNetworkEnabled.CheckedChanged += ChkNetworkEnabled_CheckedChanged;
            
            Label lblNetworkRate = new Label
            {
                Text = "最大速率限制 (MB/s):",
                Location = new System.Drawing.Point(10, 50),
                AutoSize = true
            };
            
            numNetworkRate = new NumericUpDown
            {
                Location = new System.Drawing.Point(160, 48),
                Size = new System.Drawing.Size(120, 23),
                Minimum = 1,
                Maximum = 100000,
                Value = 1
            };
            
            chkPriorityEnabled = new CheckBox
            {
                Text = "指定传输优先级",
                Location = new System.Drawing.Point(10, 90),
                AutoSize = true
            };
            chkPriorityEnabled.CheckedChanged += ChkPriorityEnabled_CheckedChanged;
            
            Label lblPriority = new Label
            {
                Text = "优先级值 (0-63):",
                Location = new System.Drawing.Point(30, 120),
                AutoSize = true
            };
            
            numPriority = new NumericUpDown
            {
                Location = new System.Drawing.Point(160, 118),
                Size = new System.Drawing.Size(80, 23),
                Minimum = 0,
                Maximum = 63,
                Value = 32
            };
            
            Label lblPriorityInfo = new Label
            {
                Text = "(63最高)",
                Location = new System.Drawing.Point(250, 120),
                AutoSize = true,
                Font = new System.Drawing.Font(this.Font.FontFamily, this.Font.Size * 0.9f)
            };
            
            networkTab.Controls.Add(chkNetworkEnabled);
            networkTab.Controls.Add(lblNetworkRate);
            networkTab.Controls.Add(numNetworkRate);
            networkTab.Controls.Add(chkPriorityEnabled);
            networkTab.Controls.Add(lblPriority);
            networkTab.Controls.Add(numPriority);
            networkTab.Controls.Add(lblPriorityInfo); // 添加说明文本
            
            return networkTab;
        }

        private TabPage CreateProcessPriorityTab()
        {
            TabPage priorityTab = new TabPage("进程优先级");
            
            chkProcessPriorityEnabled = new CheckBox
            {
                Text = "启用进程优先级设定",
                Location = new System.Drawing.Point(10, 10),
                AutoSize = true
            };
            chkProcessPriorityEnabled.CheckedChanged += ChkProcessPriorityEnabled_CheckedChanged;
            
            Label lblPriorityValue = new Label
            {
                Text = "优先级值 (0-5):",
                Location = new System.Drawing.Point(10, 50),
                AutoSize = true
            };
            
            numProcessPriority = new NumericUpDown
            {
                Location = new System.Drawing.Point(120, 48),
                Size = new System.Drawing.Size(80, 23),
                Minimum = 0,
                Maximum = 5,
                Value = 2 // 默认为Normal
            };
            
            Label lblPriorityInfo = new Label
            {
                Text = "(0=空闲, 1=低, 2=正常, 3=高于正常, 4=高, 5=实时)",
                Location = new System.Drawing.Point(120, 75),
                AutoSize = true,
                Font = new System.Drawing.Font(this.Font.FontFamily, this.Font.Size * 0.9f)
            };
            
            priorityTab.Controls.Add(chkProcessPriorityEnabled);
            priorityTab.Controls.Add(lblPriorityValue);
            priorityTab.Controls.Add(numProcessPriority);
            priorityTab.Controls.Add(lblPriorityInfo);
            
            return priorityTab;
        }

        private void LoadConfigData()
        {
            // 初始化控件状态
            UpdateControlsState();
            
            // 没有配置对象时返回
            if (_config == null)
                return;
            
            // 加载基本配置
            txtName.Text = _config.Name;
            chkEnabled.Checked = _config.IsEnabled;
            
            // 加载路径配置
            listViewPaths.Items.Clear();
            foreach (var path in _config.Paths)
            {
                ListViewItem item = new ListViewItem(path.FilePath);
                item.SubItems.Add(string.Join(" ", path.Parameters));
                item.Tag = path;
                listViewPaths.Items.Add(item);
            }
            
            // 加载CPU限制配置
            chkCpuEnabled.Checked = _config.CpuLimit.IsEnabled;
            cmbCpuLimitType.SelectedIndex = (int)_config.CpuLimit.LimitType;
            numRelativeWeight.Value = _config.CpuLimit.RelativeWeight > 0 ? _config.CpuLimit.RelativeWeight : 5;
            numRatePercentage.Value = _config.CpuLimit.RatePercentage > 0 ? _config.CpuLimit.RatePercentage : 50;
            
            cmbCoreLimitType.SelectedIndex = (int)_config.CpuLimit.CoreLimitType;
            int coreCount = Environment.ProcessorCount;
            numCoreCount.Maximum = coreCount;
            numCoreCount.Value = _config.CpuLimit.CoreCount > 0 && _config.CpuLimit.CoreCount <= coreCount ? _config.CpuLimit.CoreCount : 1;
            numCoreNumber.Maximum = coreCount - 1;
            numCoreNumber.Value = _config.CpuLimit.CoreNumber >= 0 && _config.CpuLimit.CoreNumber < coreCount ? _config.CpuLimit.CoreNumber : 0;
            
            // 加载内存限制配置
            chkMemoryEnabled.Checked = _config.MemoryLimit.IsEnabled;
            cmbMemoryAction.SelectedIndex = (int)_config.MemoryLimit.OveruseAction;
            numMemoryLimit.Value = _config.MemoryLimit.MemoryUsageLimit > 0 ? _config.MemoryLimit.MemoryUsageLimit : 1024;
            
            // 加载内存监控配置
            chkMemoryMonitoring.Checked = _config.MemoryLimit.EnableMonitoring;
            chkAutoTrim.Checked = _config.MemoryLimit.EnableAutoTrim;
            chkTerminateOnExceed.Checked = _config.MemoryLimit.TerminateOnExceed;
            
            // 加载磁盘限制配置
            chkDiskEnabled.Checked = _config.DiskLimit.IsEnabled;
            numDiskRate.Value = _config.DiskLimit.ReadWriteRateLimit > 0 ? _config.DiskLimit.ReadWriteRateLimit : 50;
            
            // 加载网络限制配置
            chkNetworkEnabled.Checked = _config.NetworkLimit.IsEnabled;
            numNetworkRate.Value = _config.NetworkLimit.MaxRateLimit > 0 ? Math.Max(1, _config.NetworkLimit.MaxRateLimit / 1024) : 1;
            chkPriorityEnabled.Checked = _config.NetworkLimit.SpecifyTransferPriority;
            numPriority.Value = _config.NetworkLimit.PriorityValue > 0 ? _config.NetworkLimit.PriorityValue : 4;
            
            // 加载进程优先级配置
            chkProcessPriorityEnabled.Checked = _config.ProcessPriority.IsEnabled;
            numProcessPriority.Value = _config.ProcessPriority.PriorityValue;
            
            // 更新控件状态
            UpdateControlsState();
        }

        private void UpdateControlsState()
        {
            // 更新主控件状态
            bool isMainEnabled = chkEnabled.Checked;
            
            // 更新CPU限制控件状态
            bool isCpuEnabled = isMainEnabled && chkCpuEnabled.Checked;
            cmbCpuLimitType.Enabled = isCpuEnabled;
            numRelativeWeight.Enabled = isCpuEnabled && cmbCpuLimitType.SelectedIndex == (int)CpuLimitType.RelativeWeight;
            numRatePercentage.Enabled = isCpuEnabled && cmbCpuLimitType.SelectedIndex == (int)CpuLimitType.AbsoluteRate;
            
            cmbCoreLimitType.Enabled = isCpuEnabled;
            numCoreCount.Enabled = isCpuEnabled && cmbCoreLimitType.SelectedIndex == (int)CoreLimitType.CoreCount;
            numCoreNumber.Enabled = isCpuEnabled && cmbCoreLimitType.SelectedIndex == (int)CoreLimitType.CoreNumber;
            
            // 更新内存限制控件状态
            bool isMemoryEnabled = isMainEnabled && chkMemoryEnabled.Checked;
            cmbMemoryAction.Enabled = isMemoryEnabled;
            numMemoryLimit.Enabled = isMemoryEnabled;
            
            // 更新内存监控控件状态
            chkMemoryMonitoring.Enabled = isMemoryEnabled;
            bool isMonitoringEnabled = isMemoryEnabled && chkMemoryMonitoring.Checked;
            chkAutoTrim.Enabled = isMonitoringEnabled;
            chkTerminateOnExceed.Enabled = isMonitoringEnabled;
            
            // 更新磁盘限制控件状态
            bool isDiskEnabled = isMainEnabled && chkDiskEnabled.Checked;
            numDiskRate.Enabled = isDiskEnabled;
            
            // 更新网络限制控件状态
            bool isNetworkEnabled = isMainEnabled && chkNetworkEnabled.Checked;
            numNetworkRate.Enabled = isNetworkEnabled;
            chkPriorityEnabled.Enabled = isNetworkEnabled;
            numPriority.Enabled = isNetworkEnabled && chkPriorityEnabled.Checked;
            
            // 更新进程优先级控件状态
            bool isPriorityEnabled = isMainEnabled && chkProcessPriorityEnabled.Checked;
            numProcessPriority.Enabled = isPriorityEnabled;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            SaveConfig();
        }
        
        private void SaveConfig()
        {
            if (_config == null)
                return;
                
            _config.Name = txtName.Text;
            _config.IsEnabled = chkEnabled.Checked;
            
            // 保存CPU限制配置
            _config.CpuLimit.IsEnabled = chkCpuEnabled.Checked;
            _config.CpuLimit.LimitType = (CpuLimitType)cmbCpuLimitType.SelectedIndex;
            _config.CpuLimit.RelativeWeight = (int)numRelativeWeight.Value;
            _config.CpuLimit.RatePercentage = (int)numRatePercentage.Value;
            _config.CpuLimit.CoreLimitType = (CoreLimitType)cmbCoreLimitType.SelectedIndex;
            _config.CpuLimit.CoreCount = (int)numCoreCount.Value;
            _config.CpuLimit.CoreNumber = (int)numCoreNumber.Value;
            
            // 保存内存限制配置
            _config.MemoryLimit.IsEnabled = chkMemoryEnabled.Checked;
            _config.MemoryLimit.OveruseAction = (MemoryOveruseAction)cmbMemoryAction.SelectedIndex;
            _config.MemoryLimit.MemoryUsageLimit = (int)numMemoryLimit.Value;
            
            // 保存内存监控配置
            _config.MemoryLimit.EnableMonitoring = chkMemoryMonitoring.Checked;
            _config.MemoryLimit.EnableAutoTrim = chkAutoTrim.Checked;
            _config.MemoryLimit.TerminateOnExceed = chkTerminateOnExceed.Checked;
            
            // 保存磁盘限制配置
            _config.DiskLimit.IsEnabled = chkDiskEnabled.Checked;
            _config.DiskLimit.ReadWriteRateLimit = (int)numDiskRate.Value;
            
            // 保存网络限制配置
            _config.NetworkLimit.IsEnabled = chkNetworkEnabled.Checked;
            _config.NetworkLimit.MaxRateLimit = (int)numNetworkRate.Value * 1024;
            _config.NetworkLimit.SpecifyTransferPriority = chkPriorityEnabled.Checked;
            _config.NetworkLimit.PriorityValue = (int)numPriority.Value;
            
            // 保存进程优先级配置
            _config.ProcessPriority.IsEnabled = chkProcessPriorityEnabled.Checked;
            _config.ProcessPriority.PriorityValue = (int)numProcessPriority.Value;
        }
        
        private void BtnAddPath_Click(object sender, EventArgs e)
        {
            using (PathEditForm pathForm = new PathEditForm())
            {
                if (pathForm.ShowDialog() == DialogResult.OK)
                {
                    PathConfig newPath = pathForm.PathConfig;
                    _config.Paths.Add(newPath);
                    
                    ListViewItem item = new ListViewItem(newPath.FilePath);
                    item.SubItems.Add(string.Join(" ", newPath.Parameters));
                    item.Tag = newPath;
                    listViewPaths.Items.Add(item);
                }
            }
        }
        
        private void BtnRemovePath_Click(object sender, EventArgs e)
        {
            if (listViewPaths.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = listViewPaths.SelectedItems[0];
                PathConfig path = (PathConfig)selectedItem.Tag;
                
                _config.Paths.Remove(path);
                listViewPaths.Items.Remove(selectedItem);
            }
            else
            {
                MessageBox.Show("请先选择要删除的路径", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private void ChkEnabled_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlsState();
        }
        
        private void ChkCpuEnabled_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlsState();
        }
        
        private void CmbCpuLimitType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateControlsState();
        }
        
        private void CmbCoreLimitType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateControlsState();
        }
        
        private void ChkMemoryEnabled_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlsState();
        }
        
        private void ChkDiskEnabled_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlsState();
        }
        
        private void ChkNetworkEnabled_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlsState();
        }
        
        private void ChkPriorityEnabled_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlsState();
        }
        
        private void ChkProcessPriorityEnabled_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlsState();
        }

        private void ListViewPaths_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listViewPaths.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = listViewPaths.SelectedItems[0];
                PathConfig path = (PathConfig)selectedItem.Tag;
                
                using (PathEditForm pathForm = new PathEditForm(path))
                {
                    if (pathForm.ShowDialog() == DialogResult.OK)
                    {
                        // 更新列表项
                        selectedItem.Text = path.FilePath;
                        selectedItem.SubItems[1].Text = string.Join(" ", path.Parameters);
                    }
                }
            }
        }
    }
} 