using System;
using System.Windows.Forms;

namespace ProcessThrottler
{
    public partial class LoadingForm : Form
    {
        public LoadingForm(string message = "正在处理中，请稍候...")
        {
            InitializeComponent();
            SetMessage(message);
        }

        // 直接在Designer文件中生成界面，这里不再需要InitializeComponents
        
        public void SetMessage(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetMessage(message)));
                return;
            }

            foreach (Control control in this.Controls)
            {
                if (control is Label label)
                {
                    label.Text = message;
                    break;
                }
            }
        }

        public static void ShowLoading(Form parent, Action action, string message = "正在处理中，请稍候...")
        {
            using (LoadingForm loadingForm = new LoadingForm(message))
            {
                loadingForm.Owner = parent;
                
                // 创建标签
                Label messageLabel = new Label();
                messageLabel.AutoSize = true;
                messageLabel.Location = new System.Drawing.Point(25, 20);
                messageLabel.Name = "messageLabel";
                messageLabel.Size = new System.Drawing.Size(350, 20);
                messageLabel.Text = message;
                messageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                
                // 创建进度条
                ProgressBar progressBar = new ProgressBar();
                progressBar.Style = ProgressBarStyle.Marquee;
                progressBar.MarqueeAnimationSpeed = 30;
                progressBar.Size = new System.Drawing.Size(350, 23);
                progressBar.Location = new System.Drawing.Point(25, 50);
                progressBar.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                
                // 添加控件
                loadingForm.Controls.Add(messageLabel);
                loadingForm.Controls.Add(progressBar);
                
                // 创建后台线程执行操作
                System.Threading.Thread thread = new System.Threading.Thread(new System.Threading.ThreadStart(() =>
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        // 操作完成后关闭窗口
                        if (loadingForm.InvokeRequired)
                        {
                            loadingForm.Invoke(new Action(() => loadingForm.Close()));
                        }
                        else
                        {
                            loadingForm.Close();
                        }
                    }
                }));
                
                // 启动后台线程
                thread.Start();
                
                // 显示加载窗口
                loadingForm.ShowDialog();
            }
        }
    }
} 