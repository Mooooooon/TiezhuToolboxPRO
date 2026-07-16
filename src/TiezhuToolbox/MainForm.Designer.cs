namespace TiezhuToolbox;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private Panel topPanel;
    private ComboBox comboWindows;
    private Button btnRefresh;
    private Button btnCapture;
    private Button btnOpenFolder;
    private Button btnRecognize;
    private PictureBox pictureBox;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel toolStripStatusLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        if (disposing)
        {
            pictureBox?.Image?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.topPanel = new Panel();
        this.comboWindows = new ComboBox();
        this.btnRefresh = new Button();
        this.btnCapture = new Button();
        this.btnOpenFolder = new Button();
        this.btnRecognize = new Button();
        this.pictureBox = new PictureBox();
        this.statusStrip = new StatusStrip();
        this.toolStripStatusLabel = new ToolStripStatusLabel();
        this.topPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
        this.statusStrip.SuspendLayout();
        this.SuspendLayout();
        // 
        // topPanel
        // 
        this.topPanel.Controls.Add(this.comboWindows);
        this.topPanel.Controls.Add(this.btnRecognize);
        this.topPanel.Controls.Add(this.btnOpenFolder);
        this.topPanel.Controls.Add(this.btnCapture);
        this.topPanel.Controls.Add(this.btnRefresh);
        this.topPanel.Dock = DockStyle.Top;
        this.topPanel.Location = new Point(0, 0);
        this.topPanel.Name = "topPanel";
        this.topPanel.Size = new Size(1200, 48);
        this.topPanel.TabIndex = 0;
        // 
        // comboWindows
        // 
        this.comboWindows.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.comboWindows.DropDownStyle = ComboBoxStyle.DropDownList;
        this.comboWindows.FormattingEnabled = true;
        this.comboWindows.Location = new Point(12, 12);
        this.comboWindows.Name = "comboWindows";
        this.comboWindows.Size = new Size(850, 23);
        this.comboWindows.TabIndex = 0;
        // 
        // btnRefresh
        // 
        this.btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnRefresh.Location = new Point(874, 11);
        this.btnRefresh.Name = "btnRefresh";
        this.btnRefresh.Size = new Size(70, 25);
        this.btnRefresh.TabIndex = 1;
        this.btnRefresh.Text = "刷新";
        this.btnRefresh.UseVisualStyleBackColor = true;
        this.btnRefresh.Click += new EventHandler(this.btnRefresh_Click);
        // 
        // btnCapture
        // 
        this.btnCapture.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnCapture.Location = new Point(950, 11);
        this.btnCapture.Name = "btnCapture";
        this.btnCapture.Size = new Size(70, 25);
        this.btnCapture.TabIndex = 2;
        this.btnCapture.Text = "截图";
        this.btnCapture.UseVisualStyleBackColor = true;
        this.btnCapture.Click += new EventHandler(this.btnCapture_Click);
        // 
        // btnOpenFolder
        // 
        this.btnOpenFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnOpenFolder.Location = new Point(1026, 11);
        this.btnOpenFolder.Name = "btnOpenFolder";
        this.btnOpenFolder.Size = new Size(70, 25);
        this.btnOpenFolder.TabIndex = 3;
        this.btnOpenFolder.Text = "目录";
        this.btnOpenFolder.UseVisualStyleBackColor = true;
        this.btnOpenFolder.Click += new EventHandler(this.btnOpenFolder_Click);
        // 
        // btnRecognize
        // 
        this.btnRecognize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnRecognize.Location = new Point(1102, 11);
        this.btnRecognize.Name = "btnRecognize";
        this.btnRecognize.Size = new Size(70, 25);
        this.btnRecognize.TabIndex = 4;
        this.btnRecognize.Text = "识别";
        this.btnRecognize.UseVisualStyleBackColor = true;
        this.btnRecognize.Click += new EventHandler(this.btnRecognize_Click);
        // 
        // pictureBox
        // 
        this.pictureBox.Dock = DockStyle.Fill;
        this.pictureBox.Location = new Point(0, 48);
        this.pictureBox.Name = "pictureBox";
        this.pictureBox.Size = new Size(1200, 454);
        this.pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        this.pictureBox.TabIndex = 1;
        this.pictureBox.TabStop = false;
        // 
        // statusStrip
        // 
        this.statusStrip.Items.AddRange(new ToolStripItem[] { this.toolStripStatusLabel });
        this.statusStrip.Location = new Point(0, 502);
        this.statusStrip.Name = "statusStrip";
        this.statusStrip.Size = new Size(1200, 22);
        this.statusStrip.TabIndex = 2;
        this.statusStrip.Text = "statusStrip";
        // 
        // toolStripStatusLabel
        // 
        this.toolStripStatusLabel.Name = "toolStripStatusLabel";
        this.toolStripStatusLabel.Size = new Size(39, 17);
        this.toolStripStatusLabel.Text = "就绪";
        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new SizeF(7F, 17F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1200, 524);
        this.Controls.Add(this.pictureBox);
        this.Controls.Add(this.topPanel);
        this.Controls.Add(this.statusStrip);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "第七史诗打铁助手 - 截图工具";
        this.Load += new EventHandler(this.MainForm_Load);
        this.topPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
        this.statusStrip.ResumeLayout(false);
        this.statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
