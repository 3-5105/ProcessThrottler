namespace ProcessThrottler
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            listViewProcesses = new ListView();
            columnName = new ColumnHeader();
            columnStatus = new ColumnHeader();
            btnAdd = new Button();
            btnDelete = new Button();
            labelInfo = new Label();
            tabControl = new TabControl();
            tabPageConfig = new TabPage();
            btnSaveConfig = new Button();
            btnApplyConfig = new Button();
            tabControl.SuspendLayout();
            tabPageConfig.SuspendLayout();
            SuspendLayout();
            // 
            // listViewProcesses
            // 
            listViewProcesses.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            listViewProcesses.Columns.AddRange(new ColumnHeader[] { columnName, columnStatus });
            listViewProcesses.FullRowSelect = true;
            listViewProcesses.GridLines = true;
            listViewProcesses.Location = new Point(3, 23);
            listViewProcesses.Name = "listViewProcesses";
            listViewProcesses.Size = new Size(662, 328);
            listViewProcesses.TabIndex = 0;
            listViewProcesses.UseCompatibleStateImageBehavior = false;
            listViewProcesses.View = View.Details;
            listViewProcesses.MouseDoubleClick += listViewProcesses_MouseDoubleClick;
            // 
            // columnName
            // 
            columnName.Text = "进程配置名称";
            columnName.Width = 200;
            // 
            // columnStatus
            // 
            columnStatus.Text = "状态";
            columnStatus.Width = 100;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(6, 357);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(82, 33);
            btnAdd.TabIndex = 1;
            btnAdd.Text = "添加配置";
            btnAdd.UseVisualStyleBackColor = true;
            // 
            // btnDelete
            // 
            btnDelete.Location = new Point(94, 357);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(82, 33);
            btnDelete.TabIndex = 2;
            btnDelete.Text = "删除配置";
            btnDelete.UseVisualStyleBackColor = true;
            // 
            // labelInfo
            // 
            labelInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            labelInfo.AutoSize = true;
            labelInfo.Location = new Point(3, 3);
            labelInfo.Name = "labelInfo";
            labelInfo.Size = new Size(224, 17);
            labelInfo.TabIndex = 3;
            labelInfo.Text = "提示：右键双击列表项可以编辑详细配置";
            // 
            // tabControl
            // 
            tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl.Controls.Add(tabPageConfig);
            tabControl.Location = new Point(10, 12);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(679, 426);
            tabControl.TabIndex = 4;
            // 
            // tabPageConfig
            // 
            tabPageConfig.Controls.Add(listViewProcesses);
            tabPageConfig.Controls.Add(btnAdd);
            tabPageConfig.Controls.Add(btnDelete);
            tabPageConfig.Controls.Add(labelInfo);
            tabPageConfig.Controls.Add(btnSaveConfig);
            tabPageConfig.Controls.Add(btnApplyConfig);
            tabPageConfig.Location = new Point(4, 26);
            tabPageConfig.Name = "tabPageConfig";
            tabPageConfig.Padding = new Padding(3);
            tabPageConfig.Size = new Size(671, 396);
            tabPageConfig.TabIndex = 0;
            tabPageConfig.Text = "配置";
            tabPageConfig.UseVisualStyleBackColor = true;
            // 
            // btnSaveConfig
            // 
            btnSaveConfig.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSaveConfig.Location = new Point(486, 357);
            btnSaveConfig.Name = "btnSaveConfig";
            btnSaveConfig.Size = new Size(82, 33);
            btnSaveConfig.TabIndex = 4;
            btnSaveConfig.Text = "保存配置";
            btnSaveConfig.UseVisualStyleBackColor = true;
            // 
            // btnApplyConfig
            // 
            btnApplyConfig.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnApplyConfig.Location = new Point(583, 357);
            btnApplyConfig.Name = "btnApplyConfig";
            btnApplyConfig.Size = new Size(82, 33);
            btnApplyConfig.TabIndex = 5;
            btnApplyConfig.Text = "应用配置";
            btnApplyConfig.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(700, 450);
            Controls.Add(tabControl);
            Name = "Form1";
            Text = "进程限制器";
            tabControl.ResumeLayout(false);
            tabPageConfig.ResumeLayout(false);
            tabPageConfig.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ListView listViewProcesses;
        private System.Windows.Forms.ColumnHeader columnName;
        private System.Windows.Forms.ColumnHeader columnStatus;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Label labelInfo;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPageConfig;
        private System.Windows.Forms.Button btnSaveConfig;
        private System.Windows.Forms.Button btnApplyConfig;
    }
}
