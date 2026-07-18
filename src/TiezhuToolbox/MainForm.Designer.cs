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
    private Panel infoPanel;
    private TableLayoutPanel infoTable;
    private Label lblLevel;
    private Label lblName;
    private Label lblQuality;
    private Label lblMainStat;
    private Label lblSubStatsTitle;
    private ListBox listSubStats;
    private Label lblSet;
    private Label lblScore;
    private Label lblHeroesTitle;
    private FlowLayoutPanel flowHeroes;

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
        this.infoPanel = new Panel();
        this.infoTable = new TableLayoutPanel();
        this.lblLevel = new Label();
        this.lblName = new Label();
        this.lblQuality = new Label();
        this.lblMainStat = new Label();
        this.lblSubStatsTitle = new Label();
        this.listSubStats = new ListBox();
        this.lblSet = new Label();
        this.lblScore = new Label();
        this.lblHeroesTitle = new Label();
        this.flowHeroes = new FlowLayoutPanel();
        this.topPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
        this.statusStrip.SuspendLayout();
        this.infoPanel.SuspendLayout();
        this.infoTable.SuspendLayout();
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
        // infoPanel
        //
        this.infoPanel.BorderStyle = BorderStyle.FixedSingle;
        this.infoPanel.Controls.Add(this.infoTable);
        this.infoPanel.Dock = DockStyle.Right;
        this.infoPanel.Location = new Point(900, 48);
        this.infoPanel.Name = "infoPanel";
        this.infoPanel.Padding = new Padding(10);
        this.infoPanel.Size = new Size(300, 454);
        this.infoPanel.TabIndex = 3;
        //
        // infoTable
        //
        this.infoTable.ColumnCount = 1;
        this.infoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.infoTable.Controls.Add(this.lblLevel, 0, 0);
        this.infoTable.Controls.Add(this.lblName, 0, 1);
        this.infoTable.Controls.Add(this.lblQuality, 0, 2);
        this.infoTable.Controls.Add(this.lblMainStat, 0, 3);
        this.infoTable.Controls.Add(this.lblSubStatsTitle, 0, 4);
        this.infoTable.Controls.Add(this.listSubStats, 0, 5);
        this.infoTable.Controls.Add(this.lblSet, 0, 6);
        this.infoTable.Controls.Add(this.lblScore, 0, 7);
        this.infoTable.Controls.Add(this.lblHeroesTitle, 0, 8);
        this.infoTable.Controls.Add(this.flowHeroes, 0, 9);
        this.infoTable.Dock = DockStyle.Fill;
        this.infoTable.Location = new Point(10, 10);
        this.infoTable.Name = "infoTable";
        this.infoTable.RowCount = 10;
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.infoTable.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        this.infoTable.Size = new Size(278, 432);
        this.infoTable.TabIndex = 0;
        //
        // lblLevel
        //
        this.lblLevel.AutoSize = true;
        this.lblLevel.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
        this.lblLevel.Location = new Point(3, 0);
        this.lblLevel.Name = "lblLevel";
        this.lblLevel.Padding = new Padding(0, 4, 0, 4);
        this.lblLevel.TabIndex = 0;
        this.lblLevel.Text = "等级 -  强化 -";
        //
        // lblName
        //
        this.lblName.AutoSize = true;
        this.lblName.Location = new Point(3, 0);
        this.lblName.Name = "lblName";
        this.lblName.Padding = new Padding(0, 2, 0, 2);
        this.lblName.TabIndex = 1;
        this.lblName.Text = "装备名称：-";
        //
        // lblQuality
        //
        this.lblQuality.AutoSize = true;
        this.lblQuality.Location = new Point(3, 0);
        this.lblQuality.Name = "lblQuality";
        this.lblQuality.Padding = new Padding(0, 2, 0, 2);
        this.lblQuality.TabIndex = 2;
        this.lblQuality.Text = "装备品质：-";
        //
        // lblMainStat
        //
        this.lblMainStat.AutoSize = true;
        this.lblMainStat.Location = new Point(3, 0);
        this.lblMainStat.Name = "lblMainStat";
        this.lblMainStat.Padding = new Padding(0, 2, 0, 2);
        this.lblMainStat.TabIndex = 3;
        this.lblMainStat.Text = "主属性：-";
        //
        // lblSubStatsTitle
        //
        this.lblSubStatsTitle.AutoSize = true;
        this.lblSubStatsTitle.Location = new Point(3, 0);
        this.lblSubStatsTitle.Name = "lblSubStatsTitle";
        this.lblSubStatsTitle.Padding = new Padding(0, 8, 0, 2);
        this.lblSubStatsTitle.TabIndex = 4;
        this.lblSubStatsTitle.Text = "副属性：";
        //
        // listSubStats
        //
        this.listSubStats.Dock = DockStyle.Fill;
        this.listSubStats.FormattingEnabled = true;
        this.listSubStats.ItemHeight = 17;
        this.listSubStats.Location = new Point(3, 100);
        this.listSubStats.MinimumSize = new Size(4, 80);
        this.listSubStats.Name = "listSubStats";
        this.listSubStats.Size = new Size(272, 200);
        this.listSubStats.TabIndex = 5;
        //
        // lblSet
        //
        this.lblSet.AutoSize = true;
        this.lblSet.Location = new Point(3, 0);
        this.lblSet.Name = "lblSet";
        this.lblSet.Padding = new Padding(0, 8, 0, 2);
        this.lblSet.TabIndex = 6;
        this.lblSet.Text = "套装：-";
        //
        // lblScore
        //
        this.lblScore.AutoSize = true;
        this.lblScore.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
        this.lblScore.Location = new Point(3, 0);
        this.lblScore.Name = "lblScore";
        this.lblScore.Padding = new Padding(0, 4, 0, 4);
        this.lblScore.TabIndex = 7;
        this.lblScore.Text = "装备分数：-";
        //
        // lblHeroesTitle
        //
        this.lblHeroesTitle.AutoSize = true;
        this.lblHeroesTitle.Location = new Point(3, 0);
        this.lblHeroesTitle.Name = "lblHeroesTitle";
        this.lblHeroesTitle.Padding = new Padding(0, 8, 0, 2);
        this.lblHeroesTitle.TabIndex = 8;
        this.lblHeroesTitle.Text = "适用角色：-";
        //
        // flowHeroes
        //
        this.flowHeroes.AutoScroll = true;
        this.flowHeroes.Dock = DockStyle.Fill;
        this.flowHeroes.FlowDirection = FlowDirection.LeftToRight;
        this.flowHeroes.Location = new Point(3, 100);
        this.flowHeroes.Name = "flowHeroes";
        this.flowHeroes.Size = new Size(272, 120);
        this.flowHeroes.TabIndex = 9;
        this.flowHeroes.WrapContents = true;
        //
        // MainForm
        //
        this.AutoScaleDimensions = new SizeF(7F, 17F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1200, 524);
        this.Controls.Add(this.pictureBox);
        this.Controls.Add(this.infoPanel);
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
        this.infoPanel.ResumeLayout(false);
        this.infoTable.ResumeLayout(false);
        this.infoTable.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
