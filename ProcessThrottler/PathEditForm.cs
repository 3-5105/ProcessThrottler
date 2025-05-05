using System;
using System.Windows.Forms;
using System.Linq;

namespace ProcessThrottler
{
    public class PathEditForm : Form
    {
        private TextBox txtFilePath;
        private TextBox txtParameters;
        private Button btnBrowse;
        private Button btnOK;
        private Button btnCancel;
        
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public PathConfig PathConfig { get; private set; }
        
        public PathEditForm() : this(new PathConfig()) { }
        
        public PathEditForm(PathConfig pathConfig)
        {
            InitializeComponents();
            PathConfig = pathConfig;
            
            // 如果是编辑模式，填充现有数据
            if (!string.IsNullOrEmpty(pathConfig.FilePath))
            {
                txtFilePath.Text = pathConfig.FilePath;
                txtParameters.Text = string.Join(" ", pathConfig.Parameters);
            }
        }
        
        private void InitializeComponents()
        {
            this.Text = "编辑路径";
            this.Size = new System.Drawing.Size(500, 200);
            this.MinimumSize = new System.Drawing.Size(500, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            
            Label lblFilePath = new Label
            {
                Text = "执行文件路径:",
                Location = new System.Drawing.Point(10, 20),
                AutoSize = true
            };
            
            txtFilePath = new TextBox
            {
                Location = new System.Drawing.Point(110, 17),
                Size = new System.Drawing.Size(290, 23)
            };
            
            btnBrowse = new Button
            {
                Text = "浏览...",
                Location = new System.Drawing.Point(410, 16),
                Size = new System.Drawing.Size(60, 23)
            };
            btnBrowse.Click += BtnBrowse_Click;
            
            Label lblParameters = new Label
            {
                Text = "命令行参数:",
                Location = new System.Drawing.Point(10, 60),
                AutoSize = true
            };
            
            txtParameters = new TextBox
            {
                Location = new System.Drawing.Point(110, 57),
                Size = new System.Drawing.Size(360, 23)
            };
            
            btnOK = new Button
            {
                Text = "确定",
                Location = new System.Drawing.Point(320, 120),
                Size = new System.Drawing.Size(75, 23),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;
            
            btnCancel = new Button
            {
                Text = "取消",
                Location = new System.Drawing.Point(405, 120),
                Size = new System.Drawing.Size(75, 23),
                DialogResult = DialogResult.Cancel
            };
            
            this.Controls.Add(lblFilePath);
            this.Controls.Add(txtFilePath);
            this.Controls.Add(btnBrowse);
            this.Controls.Add(lblParameters);
            this.Controls.Add(txtParameters);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
            
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
        
        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                openFileDialog.Title = "选择执行文件";
                
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = openFileDialog.FileName;
                }
            }
        }
        
        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFilePath.Text))
            {
                MessageBox.Show("请输入有效的文件路径", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
                return;
            }
            
            PathConfig.FilePath = txtFilePath.Text;
            PathConfig.Parameters = txtParameters.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
} 