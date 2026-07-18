namespace TiezhuToolbox;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private Panel topPanel;
    private ComboBox comboDevices;
    private TextBox txtAddress;
    private Button btnConnect;
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
        this.comboDevices = new ComboBox();
        this.txtAddress = new TextBox();
        this.btnConnect = new Button();
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
        this.topPanel.Controls.Add(this.comboDevices);
        this.topPanel.Controls.Add(this.txtAddress);
        this.topPanel.Controls.Add(this.btnConnect);
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
        // comboDevices
        //
        this.comboDevices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.comboDevices.DropDownStyle = ComboBoxStyle.DropDownList;
        this.comboDevices.FormattingEnabled = true;
        this.comboDevices.Location = new Point(12, 12);
        this.comboDevices.Name = "comboDevices";
        this.comboDevices.Size = new Size(530, 23);
        this.comboDevices.TabIndex = 0;
        //
        // txtAddress
        //
        this.txtAddress.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.txtAddress.Location = new Point(550, 12);
        this.txtAddress.Name = "txtAddress";
        this.txtAddress.Size = new Size(162, 23);
        this.txtAddress.TabIndex = 1;
        this.txtAddress.Text = "127.0.0.1:16384";
        //
        // btnConnect
        //
        this.btnConnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnConnect.Location = new Point(720, 11);
        this.btnConnect.Name = "btnConnect";
        this.btnConnect.Size = new Size(70, 25);
        this.btnConnect.TabIndex = 2;
        this.btnConnect.Text = "连接";
        this.btnConnect.UseVisualStyleBackColor = true;
        this.btnConnect.Click += new EventHandler(this.btnConnect_Click);
        //
        // btnRefresh
        //
        this.btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnRefresh.Location = new Point(796, 11);
        this.btnRefresh.Name = "btnRefresh";
        this.btnRefresh.Size = new Size(70, 25);
        this.btnRefresh.TabIndex = 3;
        this.btnRefresh.Text = "刷新";
        this.btnRefresh.UseVisualStyleBackColor = true;
        this.btnRefresh.Click += new EventHandler(this.btnRefresh_Click);
        //
        // btnCapture
        //
        this.btnCapture.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnCapture.Location = new Point(872, 11);
        this.btnCapture.Name = "btnCapture";
        this.btnCapture.Size = new Size(70, 25);
        this.btnCapture.TabIndex = 4;
        this.btnCapture.Text = "截图";
        this.btnCapture.UseVisualStyleBackColor = true;
        this.btnCapture.Click += new EventHandler(this.btnCapture_Click);
        //
        // btnOpenFolder
        //
        this.btnOpenFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnOpenFolder.Location = new Point(948, 11);
        this.btnOpenFolder.Name = "btnOpenFolder";
        this.btnOpenFolder.Size = new Size(70, 25);
        this.btnOpenFolder.TabIndex = 5;
        this.btnOpenFolder.Text = "目录";
        this.btnOpenFolder.UseVisualStyleBackColor = true;
        this.btnOpenFolder.Click += new EventHandler(this.btnOpenFolder_Click);
        //
        // btnRecognize
        //
        this.btnRecognize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnRecognize.Location = new Point(1024, 11);
        this.btnRecognize.Name = "btnRecognize";
        this.btnRecognize.Size = new Size(70, 25);
        this.btnRecognize.TabIndex = 6;
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
        this.Text = "第七史诗打铁助手 - ADB 截图";
        this.Load += new EventHandler(this.MainForm_Load);
        this.topPanel.ResumeLayout(false);
        this.topPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
        this.statusStrip.ResumeLayout(false);
        this.statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
