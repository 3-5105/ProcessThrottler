using System;
using System.Windows.Forms;
using System.Diagnostics; // 添加Process命名空间

namespace ProcessThrottler
{
    internal static class Program
    {
        // 保持托盘图标管理器的引用
        private static Tray _trayIcon;
        // 保持Core实例的引用
        private static Core _core;
        
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 启用高DPI模式
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 初始化应用程序配置
            ApplicationConfiguration.Initialize();
            
            try
            {
                // 创建窗体但不显示（配置在构造函数中加载）
                Form1 mainForm = new Form1();
                
                // 确保配置已初始化
                if (!mainForm.IsConfigInitialized)
                {
                    throw new InvalidOperationException("配置未能正确初始化");
                }
                
                // 初始化进程监控器 - 会自动从ConfigManager获取配置
                _core = new Core();
            
                
                // 初始化托盘图标管理器
                _trayIcon = new Tray(mainForm);
                
                // 设置窗体关闭时只是隐藏而不是真正关闭
                mainForm.FormClosing += (sender, e) => 
                {
                    if (e.CloseReason == CloseReason.UserClosing)
                    {
                        e.Cancel = true;
                        ((Form)sender).Hide();
                    }
                };
                
                // 运行应用程序但不显示主窗体
                Application.Run(new ApplicationContext());
                
                // 在应用程序退出前释放资源
                _core.Dispose();
                _trayIcon.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用程序初始化失败: {ex.Message}", "启动错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 启动调试命令行窗口
        /// </summary>
        private static void LaunchDebugConsole()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/k title ProcessThrottler调试控制台",
                UseShellExecute = true,
                CreateNoWindow = false
            };
            
            Process.Start(startInfo);
        }
    }
}