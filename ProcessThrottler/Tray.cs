using System;
using System.Windows.Forms;

namespace ProcessThrottler
{
    /// <summary>
    /// 托盘图标管理类
    /// </summary>
    public class Tray : IDisposable
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private Form parentForm;

        public Tray(Form form)
        {
            parentForm = form;
            InitializeTray();
        }

        private void InitializeTray()
        {
            // 创建上下文菜单
            contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("显示主窗口", null, ShowMainWindow);
            contextMenu.Items.Add("-"); // 分隔符
            contextMenu.Items.Add("退出", null, ExitApplication);

            // 初始化托盘图标
            notifyIcon = new NotifyIcon
            {
                Text = "进程资源限制器",
                Visible = true,
                Icon = parentForm.Icon, // 使用主程序的图标
                ContextMenuStrip = contextMenu
            };

            // 双击托盘图标显示主窗口
            notifyIcon.DoubleClick += (s, e) => ShowMainWindow(s, e);
        }

        /// <summary>
        /// 显示气泡提示
        /// </summary>
        public void ShowBalloonTip(string title, string text, ToolTipIcon icon, int timeout = 3000)
        {
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = text;
            notifyIcon.BalloonTipIcon = icon;
            notifyIcon.ShowBalloonTip(timeout);
        }

        /// <summary>
        /// 显示主窗口
        /// </summary>
        private void ShowMainWindow(object sender, EventArgs e)
        {
            if (parentForm != null)
            {
                parentForm.Show();
                parentForm.WindowState = FormWindowState.Normal;
                parentForm.Activate();
            }
        }

        /// <summary>
        /// 退出应用程序
        /// </summary>
        private void ExitApplication(object sender, EventArgs e)
        {
            // 先隐藏托盘图标
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
            }

            // 退出应用
            Application.Exit();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                notifyIcon = null;
            }

            if (contextMenu != null)
            {
                contextMenu.Dispose();
                contextMenu = null;
            }
        }
    }
}
