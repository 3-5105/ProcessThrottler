using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Text.Json;

namespace ProcessThrottler
{
    public partial class Form1 : Form
    {
        private List<ProcessConfig> processConfigs;
        
        /// <summary>
        /// 指示配置是否已完全初始化
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsConfigInitialized { get; private set; } = false;
        
        public Form1()
        {
            InitializeComponent();
            
            // 设置窗口最小大小
            this.MinimumSize = new System.Drawing.Size(800, 450);
            
            // 获取ConfigManager的配置
            processConfigs = ConfigManager.Instance.GetProcessConfigs();
            
            // 标记配置已初始化完成
            IsConfigInitialized = ConfigManager.Instance.IsInitialized;
            
            // 加载配置到界面
            LoadConfigs();
            
            btnAdd.Click += BtnAdd_Click;
            btnDelete.Click += BtnDelete_Click;
            btnSaveConfig.Click += BtnSaveConfig_Click;
            btnApplyConfig.Click += BtnApplyConfig_Click;
            
            // 订阅ConfigManager的配置变更事件
            ConfigManager.Instance.ConfigChanged += OnConfigChanged;
        }
        
        /// <summary>
        /// 配置变更事件处理
        /// </summary>
        private void OnConfigChanged(object sender, List<ProcessConfig> configs)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnConfigChanged(sender, configs)));
                return;
            }
            
            processConfigs = configs;
            LoadConfigs();
        }
        
        /// <summary>
        /// 获取进程配置列表
        /// </summary>
        public List<ProcessConfig> GetProcessConfigs()
        {
            return processConfigs;
        }
    
        // 处理窗口消息
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }

        private void LoadConfigs()
        {
            listViewProcesses.Items.Clear();
            foreach (var config in processConfigs)
            {
                ListViewItem item = new ListViewItem(config.Name);
                item.SubItems.Add(config.IsEnabled ? "启用" : "禁用");
                item.Tag = config;
                listViewProcesses.Items.Add(item);
            }
        }

        private void listViewProcesses_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ListViewHitTestInfo info = listViewProcesses.HitTest(e.X, e.Y);
                if (info.Item != null)
                {
                    EditConfig((ProcessConfig)info.Item.Tag);
                }
            }
        }
        
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            ProcessConfig newConfig = new ProcessConfig
            {
                Name = $"新配置 {processConfigs.Count + 1}",
                IsEnabled = false
            };
            
            if (EditConfig(newConfig))
            {
                processConfigs.Add(newConfig);
                LoadConfigs();
            }
        }
        
        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (listViewProcesses.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = listViewProcesses.SelectedItems[0];
                ProcessConfig config = (ProcessConfig)selectedItem.Tag;
                
                if (MessageBox.Show($"确定要删除 \"{config.Name}\" 吗?", "确认删除", 
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    processConfigs.Remove(config);
                    LoadConfigs();
                }
            }
            else
            {
                MessageBox.Show("请先选择要删除的配置项", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private bool EditConfig(ProcessConfig config)
        {
            using (ConfigDetailForm detailForm = new ConfigDetailForm(config))
            {
                if (detailForm.ShowDialog() == DialogResult.OK)
                {
                    LoadConfigs();
                    return true;
                }
            }
            return false;
        }
        
        private void BtnSaveConfig_Click(object sender, EventArgs e)
        {
            // 使用LoadingForm显示进度条，同时保存配置
            LoadingForm.ShowLoading(this, () => SaveConfigsToDisk(false), "正在保存配置...");
        }
        
        private void BtnApplyConfig_Click(object sender, EventArgs e)
        {
            // 使用LoadingForm显示进度条，同时应用配置
            LoadingForm.ShowLoading(this, () => {
                // 先保存配置到磁盘
                SaveConfigsToDisk(false);
                
                // 应用当前配置到系统
                ApplyConfigsToSystem(false);
                
                // 操作完成后在主线程上显示消息
                this.Invoke(new Action(() => {
                    MessageBox.Show("配置已应用到系统", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            }, "正在应用配置...");
        }
        
        private void ApplyConfigsToSystem(bool showMessage = true)
        {
            // 触发配置应用事件而不是直接调用Core
            ConfigManager.Instance.ApplyConfigs();
            
            if (showMessage)
            {
                MessageBox.Show("配置已应用到系统", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private void SaveConfigsToDisk(bool showMessage = true)
        {
            // 使用ConfigManager保存配置
            ConfigManager.Instance.UpdateProcessConfigs(processConfigs);
            bool success = ConfigManager.Instance.SaveConfigsToDisk();
            
            if (showMessage)
            {
                if (success)
                {
                    MessageBox.Show("配置已成功保存到磁盘", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("保存配置失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
