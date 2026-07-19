namespace TiezhuToolbox;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private Panel topPanel;
    private Panel topBorder;
    private ComboBox comboDevices;
    private TextBox txtAddress;
    private Button btnConnect;
    private Button btnRefresh;
    private Button btnOpenFolder;
    private Button btnToggleShot;
    private Button btnCaptureRecognize;
    private TableLayoutPanel mainTable;
    private Panel equipCard;
    private TableLayoutPanel equipTable;
    private Label lblEquipmentTitle;
    private Label lblMeta;
    private Panel scorePanel;
    private Label lblScoreValue;
    private Label lblScoreCaption;
    private Label lblMainStat;
    private Label lblSubStatsTitle;
    private ListBox listSubStats;
    private Label lblSet;
    private FlowLayoutPanel advicePanel;
    private Label lblAdviceBadge;
    private Label lblAdviceDetail;
    private FlowLayoutPanel thresholdPanel;
    private Label lblThLeft;
    private NumericUpDown numLeftThreshold;
    private Label lblThRight;
    private NumericUpDown numRightThreshold;
    private FlowLayoutPanel recognitionSettingsPanel;
    private Label lblRecognitionHotKey;
    private ComboBox comboRecognitionHotKey;
    private CheckBox chkContinuousRecognition;
    private Label lblRecognitionInterval;
    private NumericUpDown numRecognitionInterval;
    private Label lblIntervalUnit;
    private System.Windows.Forms.Timer continuousRecognitionTimer;
    private Panel heroesPanel;
    private FlowLayoutPanel heroesHeader;
    private Label lblHeroesTitle;
    private Label lblHeroesHint;
    private FlowLayoutPanel flowHeroes;
    private Label lblHeroesEmpty;
    private Panel pnlScreenshot;
    private Panel shotBorder;
    private Panel shotHeader;
    private Label lblShotTitle;
    private Button btnCollapseShot;
    private PictureBox pictureBox;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel toolStripStatusLabel;
    private ToolTip toolTip;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        if (disposing)
        {
            pictureBox?.Image?.Dispose();
            _ocrEngine?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.topPanel = new Panel();
        this.topBorder = new Panel();
        this.comboDevices = new ComboBox();
        this.txtAddress = new TextBox();
        this.btnConnect = new Button();
        this.btnRefresh = new Button();
        this.btnOpenFolder = new Button();
        this.btnToggleShot = new Button();
        this.btnCaptureRecognize = new Button();
        this.mainTable = new TableLayoutPanel();
        this.equipCard = new Panel();
        this.equipTable = new TableLayoutPanel();
        this.lblEquipmentTitle = new Label();
        this.lblMeta = new Label();
        this.scorePanel = new Panel();
        this.lblScoreValue = new Label();
        this.lblScoreCaption = new Label();
        this.lblMainStat = new Label();
        this.lblSubStatsTitle = new Label();
        this.listSubStats = new ListBox();
        this.lblSet = new Label();
        this.advicePanel = new FlowLayoutPanel();
        this.lblAdviceBadge = new Label();
        this.lblAdviceDetail = new Label();
        this.thresholdPanel = new FlowLayoutPanel();
        this.lblThLeft = new Label();
        this.numLeftThreshold = new NumericUpDown();
        this.lblThRight = new Label();
        this.numRightThreshold = new NumericUpDown();
        this.recognitionSettingsPanel = new FlowLayoutPanel();
        this.lblRecognitionHotKey = new Label();
        this.comboRecognitionHotKey = new ComboBox();
        this.chkContinuousRecognition = new CheckBox();
        this.lblRecognitionInterval = new Label();
        this.numRecognitionInterval = new NumericUpDown();
        this.lblIntervalUnit = new Label();
        this.continuousRecognitionTimer = new System.Windows.Forms.Timer(this.components);
        this.heroesPanel = new Panel();
        this.heroesHeader = new FlowLayoutPanel();
        this.lblHeroesTitle = new Label();
        this.lblHeroesHint = new Label();
        this.flowHeroes = new FlowLayoutPanel();
        this.lblHeroesEmpty = new Label();
        this.pnlScreenshot = new Panel();
        this.shotBorder = new Panel();
        this.shotHeader = new Panel();
        this.lblShotTitle = new Label();
        this.btnCollapseShot = new Button();
        this.pictureBox = new PictureBox();
        this.statusStrip = new StatusStrip();
        this.toolStripStatusLabel = new ToolStripStatusLabel();
        this.toolTip = new ToolTip(this.components);
        this.topPanel.SuspendLayout();
        this.mainTable.SuspendLayout();
        this.equipCard.SuspendLayout();
        this.equipTable.SuspendLayout();
        this.scorePanel.SuspendLayout();
        this.heroesPanel.SuspendLayout();
        this.heroesHeader.SuspendLayout();
        this.flowHeroes.SuspendLayout();
        this.pnlScreenshot.SuspendLayout();
        this.shotHeader.SuspendLayout();
        this.advicePanel.SuspendLayout();
        this.thresholdPanel.SuspendLayout();
        this.recognitionSettingsPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.numLeftThreshold)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.numRightThreshold)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.numRecognitionInterval)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
        this.statusStrip.SuspendLayout();
        this.SuspendLayout();
        //
        // topPanel
        //
        this.topPanel.BackColor = Color.White;
        this.topPanel.Controls.Add(this.comboDevices);
        this.topPanel.Controls.Add(this.txtAddress);
        this.topPanel.Controls.Add(this.btnConnect);
        this.topPanel.Controls.Add(this.btnRefresh);
        this.topPanel.Controls.Add(this.btnOpenFolder);
        this.topPanel.Controls.Add(this.btnToggleShot);
        this.topPanel.Controls.Add(this.btnCaptureRecognize);
        this.topPanel.Controls.Add(this.topBorder);
        this.topPanel.Dock = DockStyle.Top;
        this.topPanel.Location = new Point(0, 0);
        this.topPanel.Name = "topPanel";
        this.topPanel.Size = new Size(1000, 64);
        this.topPanel.TabIndex = 0;
        //
        // topBorder
        //
        this.topBorder.BackColor = Color.FromArgb(232, 234, 237);
        this.topBorder.Dock = DockStyle.Bottom;
        this.topBorder.Location = new Point(0, 63);
        this.topBorder.Name = "topBorder";
        this.topBorder.Size = new Size(1000, 1);
        this.topBorder.TabIndex = 7;
        //
        // comboDevices
        //
        this.comboDevices.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.comboDevices.DropDownStyle = ComboBoxStyle.DropDownList;
        this.comboDevices.FlatStyle = FlatStyle.Flat;
        this.comboDevices.FormattingEnabled = true;
        this.comboDevices.Location = new Point(12, 18);
        this.comboDevices.Name = "comboDevices";
        this.comboDevices.Size = new Size(348, 25);
        this.comboDevices.TabIndex = 0;
        //
        // txtAddress
        //
        this.txtAddress.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.txtAddress.BorderStyle = BorderStyle.FixedSingle;
        this.txtAddress.Location = new Point(368, 18);
        this.txtAddress.Name = "txtAddress";
        this.txtAddress.Size = new Size(148, 23);
        this.txtAddress.TabIndex = 1;
        this.txtAddress.Text = "127.0.0.1:16384";
        //
        // btnConnect
        //
        this.btnConnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnConnect.BackColor = Color.White;
        this.btnConnect.FlatAppearance.BorderColor = Color.FromArgb(218, 220, 224);
        this.btnConnect.FlatAppearance.MouseDownBackColor = Color.FromArgb(232, 234, 237);
        this.btnConnect.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 243, 244);
        this.btnConnect.FlatStyle = FlatStyle.Flat;
        this.btnConnect.ForeColor = Color.FromArgb(32, 33, 36);
        this.btnConnect.Location = new Point(524, 14);
        this.btnConnect.Name = "btnConnect";
        this.btnConnect.Size = new Size(76, 34);
        this.btnConnect.TabIndex = 2;
        this.btnConnect.Text = "连接";
        this.btnConnect.UseVisualStyleBackColor = false;
        this.btnConnect.Click += new EventHandler(this.btnConnect_Click);
        //
        // btnRefresh
        //
        this.btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnRefresh.BackColor = Color.White;
        this.btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(218, 220, 224);
        this.btnRefresh.FlatAppearance.MouseDownBackColor = Color.FromArgb(232, 234, 237);
        this.btnRefresh.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 243, 244);
        this.btnRefresh.FlatStyle = FlatStyle.Flat;
        this.btnRefresh.ForeColor = Color.FromArgb(32, 33, 36);
        this.btnRefresh.Location = new Point(608, 14);
        this.btnRefresh.Name = "btnRefresh";
        this.btnRefresh.Size = new Size(76, 34);
        this.btnRefresh.TabIndex = 3;
        this.btnRefresh.Text = "刷新";
        this.btnRefresh.UseVisualStyleBackColor = false;
        this.btnRefresh.Click += new EventHandler(this.btnRefresh_Click);
        //
        // btnOpenFolder
        //
        this.btnOpenFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnOpenFolder.BackColor = Color.White;
        this.btnOpenFolder.FlatAppearance.BorderColor = Color.FromArgb(218, 220, 224);
        this.btnOpenFolder.FlatAppearance.MouseDownBackColor = Color.FromArgb(232, 234, 237);
        this.btnOpenFolder.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 243, 244);
        this.btnOpenFolder.FlatStyle = FlatStyle.Flat;
        this.btnOpenFolder.ForeColor = Color.FromArgb(32, 33, 36);
        this.btnOpenFolder.Location = new Point(692, 14);
        this.btnOpenFolder.Name = "btnOpenFolder";
        this.btnOpenFolder.Size = new Size(76, 34);
        this.btnOpenFolder.TabIndex = 4;
        this.btnOpenFolder.Text = "目录";
        this.btnOpenFolder.UseVisualStyleBackColor = false;
        this.btnOpenFolder.Click += new EventHandler(this.btnOpenFolder_Click);
        //
        // btnToggleShot
        //
        this.btnToggleShot.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnToggleShot.BackColor = Color.White;
        this.btnToggleShot.FlatAppearance.BorderColor = Color.FromArgb(218, 220, 224);
        this.btnToggleShot.FlatAppearance.MouseDownBackColor = Color.FromArgb(232, 234, 237);
        this.btnToggleShot.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 243, 244);
        this.btnToggleShot.FlatStyle = FlatStyle.Flat;
        this.btnToggleShot.ForeColor = Color.FromArgb(32, 33, 36);
        this.btnToggleShot.Location = new Point(776, 14);
        this.btnToggleShot.Name = "btnToggleShot";
        this.btnToggleShot.Size = new Size(92, 34);
        this.btnToggleShot.TabIndex = 5;
        this.btnToggleShot.Text = "查看截图";
        this.btnToggleShot.UseVisualStyleBackColor = false;
        this.btnToggleShot.Click += new EventHandler(this.btnToggleScreenshot_Click);
        //
        // btnCaptureRecognize
        //
        this.btnCaptureRecognize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnCaptureRecognize.BackColor = Color.FromArgb(26, 115, 232);
        this.btnCaptureRecognize.FlatAppearance.BorderSize = 0;
        this.btnCaptureRecognize.FlatAppearance.MouseDownBackColor = Color.FromArgb(24, 90, 188);
        this.btnCaptureRecognize.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 103, 210);
        this.btnCaptureRecognize.FlatStyle = FlatStyle.Flat;
        this.btnCaptureRecognize.Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold);
        this.btnCaptureRecognize.ForeColor = Color.White;
        this.btnCaptureRecognize.Location = new Point(876, 14);
        this.btnCaptureRecognize.Name = "btnCaptureRecognize";
        this.btnCaptureRecognize.Size = new Size(112, 34);
        this.btnCaptureRecognize.TabIndex = 6;
        this.btnCaptureRecognize.Text = "截图识别";
        this.btnCaptureRecognize.UseVisualStyleBackColor = false;
        this.btnCaptureRecognize.Click += new EventHandler(this.btnCaptureRecognize_Click);
        //
        // mainTable
        //
        this.mainTable.BackColor = Color.FromArgb(245, 246, 248);
        this.mainTable.ColumnCount = 2;
        this.mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 372F));
        this.mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.mainTable.Controls.Add(this.equipCard, 0, 0);
        this.mainTable.Controls.Add(this.heroesPanel, 1, 0);
        this.mainTable.Dock = DockStyle.Fill;
        this.mainTable.Location = new Point(0, 64);
        this.mainTable.Name = "mainTable";
        this.mainTable.RowCount = 1;
        this.mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        this.mainTable.Size = new Size(1000, 554);
        this.mainTable.TabIndex = 1;
        //
        // equipCard
        //
        this.equipCard.BackColor = Color.White;
        this.equipCard.Controls.Add(this.equipTable);
        this.equipCard.Dock = DockStyle.Fill;
        this.equipCard.Location = new Point(12, 12);
        this.equipCard.Margin = new Padding(12, 12, 6, 12);
        this.equipCard.Name = "equipCard";
        this.equipCard.Padding = new Padding(18, 16, 18, 16);
        this.equipCard.Size = new Size(354, 530);
        this.equipCard.TabIndex = 0;
        //
        // equipTable
        //
        this.equipTable.ColumnCount = 1;
        this.equipTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.equipTable.Controls.Add(this.lblEquipmentTitle, 0, 0);
        this.equipTable.Controls.Add(this.lblMeta, 0, 1);
        this.equipTable.Controls.Add(this.scorePanel, 0, 2);
        this.equipTable.Controls.Add(this.advicePanel, 0, 3);
        this.equipTable.Controls.Add(this.lblMainStat, 0, 4);
        this.equipTable.Controls.Add(this.lblSubStatsTitle, 0, 5);
        this.equipTable.Controls.Add(this.listSubStats, 0, 6);
        this.equipTable.Controls.Add(this.lblSet, 0, 7);
        this.equipTable.Controls.Add(this.thresholdPanel, 0, 8);
        this.equipTable.Controls.Add(this.recognitionSettingsPanel, 0, 9);
        this.equipTable.Dock = DockStyle.Fill;
        this.equipTable.Location = new Point(18, 16);
        this.equipTable.Name = "equipTable";
        this.equipTable.RowCount = 10;
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.equipTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.equipTable.Size = new Size(318, 498);
        this.equipTable.TabIndex = 0;
        //
        // lblEquipmentTitle
        //
        this.lblEquipmentTitle.AutoSize = true;
        this.lblEquipmentTitle.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
        this.lblEquipmentTitle.ForeColor = Color.FromArgb(32, 33, 36);
        this.lblEquipmentTitle.Location = new Point(0, 0);
        this.lblEquipmentTitle.Margin = new Padding(0, 0, 0, 2);
        this.lblEquipmentTitle.Name = "lblEquipmentTitle";
        this.lblEquipmentTitle.TabIndex = 0;
        this.lblEquipmentTitle.Text = "装备信息";
        //
        // lblMeta
        //
        this.lblMeta.AutoSize = true;
        this.lblMeta.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblMeta.Location = new Point(0, 30);
        this.lblMeta.Margin = new Padding(0);
        this.lblMeta.Name = "lblMeta";
        this.lblMeta.TabIndex = 1;
        this.lblMeta.Text = "点击「截图识别」获取装备信息";
        //
        // scorePanel
        //
        this.scorePanel.Controls.Add(this.lblScoreValue);
        this.scorePanel.Controls.Add(this.lblScoreCaption);
        this.scorePanel.Dock = DockStyle.Fill;
        this.scorePanel.Location = new Point(0, 63);
        this.scorePanel.Margin = new Padding(0, 14, 0, 4);
        this.scorePanel.Name = "scorePanel";
        this.scorePanel.Size = new Size(318, 74);
        this.scorePanel.TabIndex = 2;
        //
        // lblScoreValue
        //
        this.lblScoreValue.Dock = DockStyle.Fill;
        this.lblScoreValue.Font = new Font("Microsoft YaHei UI", 24F, FontStyle.Bold);
        this.lblScoreValue.ForeColor = Color.FromArgb(26, 115, 232);
        this.lblScoreValue.Location = new Point(0, 18);
        this.lblScoreValue.Name = "lblScoreValue";
        this.lblScoreValue.Size = new Size(318, 56);
        this.lblScoreValue.TabIndex = 0;
        this.lblScoreValue.Text = "-";
        this.lblScoreValue.TextAlign = ContentAlignment.MiddleLeft;
        //
        // lblScoreCaption
        //
        this.lblScoreCaption.Dock = DockStyle.Top;
        this.lblScoreCaption.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblScoreCaption.Location = new Point(0, 0);
        this.lblScoreCaption.Name = "lblScoreCaption";
        this.lblScoreCaption.Size = new Size(318, 18);
        this.lblScoreCaption.TabIndex = 1;
        this.lblScoreCaption.Text = "民间分数";
        //
        // lblMainStat
        //
        this.lblMainStat.AutoSize = true;
        this.lblMainStat.Font = new Font("Microsoft YaHei UI", 10.5F);
        this.lblMainStat.ForeColor = Color.FromArgb(32, 33, 36);
        this.lblMainStat.Location = new Point(0, 151);
        this.lblMainStat.Margin = new Padding(0, 10, 0, 0);
        this.lblMainStat.Name = "lblMainStat";
        this.lblMainStat.TabIndex = 3;
        this.lblMainStat.Text = "主属性：-";
        //
        // lblSubStatsTitle
        //
        this.lblSubStatsTitle.AutoSize = true;
        this.lblSubStatsTitle.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
        this.lblSubStatsTitle.ForeColor = Color.FromArgb(32, 33, 36);
        this.lblSubStatsTitle.Location = new Point(0, 183);
        this.lblSubStatsTitle.Margin = new Padding(0, 12, 0, 4);
        this.lblSubStatsTitle.Name = "lblSubStatsTitle";
        this.lblSubStatsTitle.TabIndex = 4;
        this.lblSubStatsTitle.Text = "副属性";
        //
        // listSubStats
        //
        this.listSubStats.BackColor = Color.White;
        this.listSubStats.BorderStyle = BorderStyle.None;
        this.listSubStats.Dock = DockStyle.Fill;
        this.listSubStats.Font = new Font("Microsoft YaHei UI", 10.5F);
        this.listSubStats.ForeColor = Color.FromArgb(32, 33, 36);
        this.listSubStats.FormattingEnabled = true;
        this.listSubStats.IntegralHeight = false;
        this.listSubStats.ItemHeight = 26;
        this.listSubStats.Location = new Point(0, 207);
        this.listSubStats.Margin = new Padding(0);
        this.listSubStats.Name = "listSubStats";
        this.listSubStats.SelectionMode = SelectionMode.None;
        this.listSubStats.Size = new Size(318, 242);
        this.listSubStats.TabIndex = 5;
        //
        // lblSet
        //
        this.lblSet.AutoSize = true;
        this.lblSet.Font = new Font("Microsoft YaHei UI", 10.5F);
        this.lblSet.ForeColor = Color.FromArgb(32, 33, 36);
        this.lblSet.Location = new Point(0, 461);
        this.lblSet.Margin = new Padding(0, 12, 0, 0);
        this.lblSet.Name = "lblSet";
        this.lblSet.TabIndex = 6;
        this.lblSet.Text = "套装：-";
        //
        // advicePanel
        //
        this.advicePanel.AutoSize = true;
        this.advicePanel.Controls.Add(this.lblAdviceBadge);
        this.advicePanel.Controls.Add(this.lblAdviceDetail);
        this.advicePanel.Dock = DockStyle.Fill;
        this.advicePanel.FlowDirection = FlowDirection.TopDown;
        this.advicePanel.Location = new Point(0, 143);
        this.advicePanel.Margin = new Padding(0, 2, 0, 4);
        this.advicePanel.Name = "advicePanel";
        this.advicePanel.Size = new Size(318, 52);
        this.advicePanel.TabIndex = 7;
        this.advicePanel.WrapContents = false;
        //
        // lblAdviceBadge
        //
        this.lblAdviceBadge.AutoSize = true;
        this.lblAdviceBadge.BackColor = Color.FromArgb(95, 99, 104);
        this.lblAdviceBadge.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
        this.lblAdviceBadge.ForeColor = Color.White;
        this.lblAdviceBadge.Location = new Point(0, 0);
        this.lblAdviceBadge.Margin = new Padding(0);
        this.lblAdviceBadge.Name = "lblAdviceBadge";
        this.lblAdviceBadge.Padding = new Padding(8, 3, 8, 3);
        this.lblAdviceBadge.TabIndex = 0;
        this.lblAdviceBadge.Text = "强化建议";
        //
        // lblAdviceDetail
        //
        this.lblAdviceDetail.AutoSize = true;
        this.lblAdviceDetail.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblAdviceDetail.Location = new Point(0, 26);
        this.lblAdviceDetail.Margin = new Padding(0, 4, 0, 0);
        this.lblAdviceDetail.Name = "lblAdviceDetail";
        this.lblAdviceDetail.TabIndex = 1;
        this.lblAdviceDetail.Text = "识别装备后给出是否继续强化的建议";
        //
        // thresholdPanel
        //
        this.thresholdPanel.AutoSize = true;
        this.thresholdPanel.Controls.Add(this.lblThLeft);
        this.thresholdPanel.Controls.Add(this.numLeftThreshold);
        this.thresholdPanel.Controls.Add(this.lblThRight);
        this.thresholdPanel.Controls.Add(this.numRightThreshold);
        this.thresholdPanel.Dock = DockStyle.Fill;
        this.thresholdPanel.Location = new Point(0, 473);
        this.thresholdPanel.Margin = new Padding(0, 12, 0, 0);
        this.thresholdPanel.Name = "thresholdPanel";
        this.thresholdPanel.Size = new Size(318, 25);
        this.thresholdPanel.TabIndex = 8;
        this.thresholdPanel.WrapContents = false;
        //
        // lblThLeft
        //
        this.lblThLeft.AutoSize = true;
        this.lblThLeft.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblThLeft.Location = new Point(0, 5);
        this.lblThLeft.Margin = new Padding(0, 5, 4, 0);
        this.lblThLeft.Name = "lblThLeft";
        this.lblThLeft.TabIndex = 0;
        this.lblThLeft.Text = "阈值 左三件";
        this.toolTip.SetToolTip(this.lblThLeft, "分数阈值：强化 +3 前副属性分数需达到此值才建议继续，之后每 3 级要求 +6 分，+15 时 +30 分建议重铸");
        //
        // numLeftThreshold
        //
        this.numLeftThreshold.Location = new Point(68, 3);
        this.numLeftThreshold.Margin = new Padding(0, 3, 0, 0);
        this.numLeftThreshold.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
        this.numLeftThreshold.Name = "numLeftThreshold";
        this.numLeftThreshold.Size = new Size(50, 23);
        this.numLeftThreshold.TabIndex = 1;
        this.toolTip.SetToolTip(this.numLeftThreshold, "分数阈值：强化 +3 前副属性分数需达到此值才建议继续，之后每 3 级要求 +6 分，+15 时 +30 分建议重铸");
        this.numLeftThreshold.Value = new decimal(new int[] { 24, 0, 0, 0 });
        this.numLeftThreshold.ValueChanged += new EventHandler(this.numThreshold_ValueChanged);
        //
        // lblThRight
        //
        this.lblThRight.AutoSize = true;
        this.lblThRight.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblThRight.Location = new Point(130, 5);
        this.lblThRight.Margin = new Padding(12, 5, 4, 0);
        this.lblThRight.Name = "lblThRight";
        this.lblThRight.TabIndex = 2;
        this.lblThRight.Text = "右三件";
        this.toolTip.SetToolTip(this.lblThRight, "分数阈值：强化 +3 前副属性分数需达到此值才建议继续，之后每 3 级要求 +6 分，+15 时 +30 分建议重铸");
        //
        // numRightThreshold
        //
        this.numRightThreshold.Location = new Point(178, 3);
        this.numRightThreshold.Margin = new Padding(0, 3, 0, 0);
        this.numRightThreshold.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
        this.numRightThreshold.Name = "numRightThreshold";
        this.numRightThreshold.Size = new Size(50, 23);
        this.numRightThreshold.TabIndex = 3;
        this.toolTip.SetToolTip(this.numRightThreshold, "分数阈值：强化 +3 前副属性分数需达到此值才建议继续，之后每 3 级要求 +6 分，+15 时 +30 分建议重铸");
        this.numRightThreshold.Value = new decimal(new int[] { 24, 0, 0, 0 });
        this.numRightThreshold.ValueChanged += new EventHandler(this.numThreshold_ValueChanged);
        //
        // recognitionSettingsPanel
        //
        this.recognitionSettingsPanel.AutoSize = true;
        this.recognitionSettingsPanel.Controls.Add(this.lblRecognitionHotKey);
        this.recognitionSettingsPanel.Controls.Add(this.comboRecognitionHotKey);
        this.recognitionSettingsPanel.Controls.Add(this.chkContinuousRecognition);
        this.recognitionSettingsPanel.Controls.Add(this.lblRecognitionInterval);
        this.recognitionSettingsPanel.Controls.Add(this.numRecognitionInterval);
        this.recognitionSettingsPanel.Controls.Add(this.lblIntervalUnit);
        this.recognitionSettingsPanel.Dock = DockStyle.Fill;
        this.recognitionSettingsPanel.Location = new Point(0, 504);
        this.recognitionSettingsPanel.Margin = new Padding(0, 6, 0, 0);
        this.recognitionSettingsPanel.Name = "recognitionSettingsPanel";
        this.recognitionSettingsPanel.Size = new Size(318, 25);
        this.recognitionSettingsPanel.TabIndex = 9;
        this.recognitionSettingsPanel.WrapContents = false;
        //
        // lblRecognitionHotKey
        //
        this.lblRecognitionHotKey.AutoSize = true;
        this.lblRecognitionHotKey.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblRecognitionHotKey.Location = new Point(0, 5);
        this.lblRecognitionHotKey.Margin = new Padding(0, 5, 4, 0);
        this.lblRecognitionHotKey.Name = "lblRecognitionHotKey";
        this.lblRecognitionHotKey.TabIndex = 0;
        this.lblRecognitionHotKey.Text = "快捷键";
        this.toolTip.SetToolTip(this.lblRecognitionHotKey, "全局识别快捷键，切换到模拟器窗口后也可使用");
        //
        // comboRecognitionHotKey
        //
        this.comboRecognitionHotKey.DropDownStyle = ComboBoxStyle.DropDownList;
        this.comboRecognitionHotKey.FormattingEnabled = true;
        this.comboRecognitionHotKey.Items.AddRange(new object[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" });
        this.comboRecognitionHotKey.Location = new Point(48, 1);
        this.comboRecognitionHotKey.Margin = new Padding(0, 1, 0, 0);
        this.comboRecognitionHotKey.Name = "comboRecognitionHotKey";
        this.comboRecognitionHotKey.Size = new Size(54, 25);
        this.comboRecognitionHotKey.TabIndex = 1;
        this.comboRecognitionHotKey.SelectedIndex = 1;
        this.comboRecognitionHotKey.SelectedIndexChanged += new EventHandler(this.comboRecognitionHotKey_SelectedIndexChanged);
        this.toolTip.SetToolTip(this.comboRecognitionHotKey, "全局识别快捷键，默认 F2");
        //
        // chkContinuousRecognition
        //
        this.chkContinuousRecognition.AutoSize = true;
        this.chkContinuousRecognition.Location = new Point(110, 3);
        this.chkContinuousRecognition.Margin = new Padding(8, 3, 0, 0);
        this.chkContinuousRecognition.Name = "chkContinuousRecognition";
        this.chkContinuousRecognition.Size = new Size(75, 21);
        this.chkContinuousRecognition.TabIndex = 2;
        this.chkContinuousRecognition.Text = "持续识别";
        this.chkContinuousRecognition.UseVisualStyleBackColor = true;
        this.chkContinuousRecognition.CheckedChanged += new EventHandler(this.chkContinuousRecognition_CheckedChanged);
        this.toolTip.SetToolTip(this.chkContinuousRecognition, "开启后自动截图识别；上一轮完成前不会重复启动");
        //
        // lblRecognitionInterval
        //
        this.lblRecognitionInterval.AutoSize = true;
        this.lblRecognitionInterval.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblRecognitionInterval.Location = new Point(189, 5);
        this.lblRecognitionInterval.Margin = new Padding(4, 5, 3, 0);
        this.lblRecognitionInterval.Name = "lblRecognitionInterval";
        this.lblRecognitionInterval.TabIndex = 3;
        this.lblRecognitionInterval.Text = "间隔";
        //
        // numRecognitionInterval
        //
        this.numRecognitionInterval.DecimalPlaces = 1;
        this.numRecognitionInterval.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
        this.numRecognitionInterval.Location = new Point(224, 2);
        this.numRecognitionInterval.Margin = new Padding(0, 2, 0, 0);
        this.numRecognitionInterval.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
        this.numRecognitionInterval.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
        this.numRecognitionInterval.Name = "numRecognitionInterval";
        this.numRecognitionInterval.Size = new Size(52, 23);
        this.numRecognitionInterval.TabIndex = 4;
        this.numRecognitionInterval.Value = new decimal(new int[] { 1, 0, 0, 65536 });
        this.numRecognitionInterval.ValueChanged += new EventHandler(this.numRecognitionInterval_ValueChanged);
        this.toolTip.SetToolTip(this.numRecognitionInterval, "两轮识别的最短间隔；实际速度受截图和 OCR 耗时限制");
        //
        // lblIntervalUnit
        //
        this.lblIntervalUnit.AutoSize = true;
        this.lblIntervalUnit.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblIntervalUnit.Location = new Point(279, 5);
        this.lblIntervalUnit.Margin = new Padding(3, 5, 0, 0);
        this.lblIntervalUnit.Name = "lblIntervalUnit";
        this.lblIntervalUnit.TabIndex = 5;
        this.lblIntervalUnit.Text = "秒";
        //
        // continuousRecognitionTimer
        //
        this.continuousRecognitionTimer.Interval = 100;
        this.continuousRecognitionTimer.Tick += new EventHandler(this.continuousRecognitionTimer_Tick);
        //
        // heroesPanel
        //
        this.heroesPanel.BackColor = Color.White;
        this.heroesPanel.Controls.Add(this.flowHeroes);
        this.heroesPanel.Controls.Add(this.heroesHeader);
        this.heroesPanel.Dock = DockStyle.Fill;
        this.heroesPanel.Location = new Point(378, 12);
        this.heroesPanel.Margin = new Padding(6, 12, 12, 12);
        this.heroesPanel.Name = "heroesPanel";
        this.heroesPanel.Padding = new Padding(18, 12, 18, 12);
        this.heroesPanel.Size = new Size(610, 530);
        this.heroesPanel.TabIndex = 1;
        //
        // heroesHeader
        //
        this.heroesHeader.Controls.Add(this.lblHeroesTitle);
        this.heroesHeader.Controls.Add(this.lblHeroesHint);
        this.heroesHeader.Dock = DockStyle.Top;
        this.heroesHeader.FlowDirection = FlowDirection.LeftToRight;
        this.heroesHeader.Location = new Point(18, 12);
        this.heroesHeader.Margin = new Padding(0);
        this.heroesHeader.Name = "heroesHeader";
        this.heroesHeader.Size = new Size(574, 34);
        this.heroesHeader.TabIndex = 0;
        this.heroesHeader.WrapContents = false;
        //
        // lblHeroesTitle
        //
        this.lblHeroesTitle.AutoSize = true;
        this.lblHeroesTitle.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
        this.lblHeroesTitle.ForeColor = Color.FromArgb(32, 33, 36);
        this.lblHeroesTitle.Location = new Point(0, 4);
        this.lblHeroesTitle.Margin = new Padding(0, 4, 8, 0);
        this.lblHeroesTitle.Name = "lblHeroesTitle";
        this.lblHeroesTitle.TabIndex = 0;
        this.lblHeroesTitle.Text = "适用角色";
        //
        // lblHeroesHint
        //
        this.lblHeroesHint.AutoSize = true;
        this.lblHeroesHint.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblHeroesHint.Location = new Point(86, 9);
        this.lblHeroesHint.Margin = new Padding(0, 9, 0, 0);
        this.lblHeroesHint.Name = "lblHeroesHint";
        this.lblHeroesHint.TabIndex = 1;
        this.lblHeroesHint.Text = "官方战绩 · 传说分段";
        //
        // flowHeroes
        //
        this.flowHeroes.AutoScroll = true;
        this.flowHeroes.BackColor = Color.White;
        this.flowHeroes.Controls.Add(this.lblHeroesEmpty);
        this.flowHeroes.Dock = DockStyle.Fill;
        this.flowHeroes.FlowDirection = FlowDirection.LeftToRight;
        this.flowHeroes.Location = new Point(18, 46);
        this.flowHeroes.Name = "flowHeroes";
        this.flowHeroes.Padding = new Padding(0, 8, 0, 0);
        this.flowHeroes.Size = new Size(574, 472);
        this.flowHeroes.TabIndex = 1;
        this.flowHeroes.WrapContents = true;
        //
        // lblHeroesEmpty
        //
        this.lblHeroesEmpty.AutoSize = true;
        this.lblHeroesEmpty.ForeColor = Color.FromArgb(95, 99, 104);
        this.lblHeroesEmpty.Location = new Point(2, 8);
        this.lblHeroesEmpty.Margin = new Padding(2, 0, 0, 0);
        this.lblHeroesEmpty.Name = "lblHeroesEmpty";
        this.lblHeroesEmpty.TabIndex = 0;
        this.lblHeroesEmpty.Text = "识别装备后，这里会显示推荐角色";
        //
        // pnlScreenshot
        //
        this.pnlScreenshot.BackColor = Color.White;
        this.pnlScreenshot.Controls.Add(this.pictureBox);
        this.pnlScreenshot.Controls.Add(this.shotHeader);
        this.pnlScreenshot.Controls.Add(this.shotBorder);
        this.pnlScreenshot.Dock = DockStyle.Bottom;
        this.pnlScreenshot.Location = new Point(0, 338);
        this.pnlScreenshot.Name = "pnlScreenshot";
        this.pnlScreenshot.Size = new Size(1000, 280);
        this.pnlScreenshot.TabIndex = 2;
        this.pnlScreenshot.Visible = false;
        //
        // shotBorder
        //
        this.shotBorder.BackColor = Color.FromArgb(232, 234, 237);
        this.shotBorder.Dock = DockStyle.Top;
        this.shotBorder.Location = new Point(0, 0);
        this.shotBorder.Name = "shotBorder";
        this.shotBorder.Size = new Size(1000, 1);
        this.shotBorder.TabIndex = 0;
        //
        // shotHeader
        //
        this.shotHeader.BackColor = Color.White;
        this.shotHeader.Controls.Add(this.lblShotTitle);
        this.shotHeader.Controls.Add(this.btnCollapseShot);
        this.shotHeader.Dock = DockStyle.Top;
        this.shotHeader.Location = new Point(0, 1);
        this.shotHeader.Name = "shotHeader";
        this.shotHeader.Size = new Size(1000, 36);
        this.shotHeader.TabIndex = 1;
        //
        // lblShotTitle
        //
        this.lblShotTitle.AutoSize = true;
        this.lblShotTitle.Font = new Font("Microsoft YaHei UI", 9.75F, FontStyle.Bold);
        this.lblShotTitle.ForeColor = Color.FromArgb(32, 33, 36);
        this.lblShotTitle.Location = new Point(16, 9);
        this.lblShotTitle.Name = "lblShotTitle";
        this.lblShotTitle.TabIndex = 0;
        this.lblShotTitle.Text = "截图预览";
        //
        // btnCollapseShot
        //
        this.btnCollapseShot.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.btnCollapseShot.BackColor = Color.White;
        this.btnCollapseShot.FlatAppearance.BorderColor = Color.FromArgb(218, 220, 224);
        this.btnCollapseShot.FlatAppearance.MouseDownBackColor = Color.FromArgb(232, 234, 237);
        this.btnCollapseShot.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 243, 244);
        this.btnCollapseShot.FlatStyle = FlatStyle.Flat;
        this.btnCollapseShot.ForeColor = Color.FromArgb(32, 33, 36);
        this.btnCollapseShot.Location = new Point(916, 6);
        this.btnCollapseShot.Name = "btnCollapseShot";
        this.btnCollapseShot.Size = new Size(72, 24);
        this.btnCollapseShot.TabIndex = 1;
        this.btnCollapseShot.Text = "收起";
        this.btnCollapseShot.UseVisualStyleBackColor = false;
        this.btnCollapseShot.Click += new EventHandler(this.btnToggleScreenshot_Click);
        //
        // pictureBox
        //
        this.pictureBox.BackColor = Color.FromArgb(32, 33, 36);
        this.pictureBox.Dock = DockStyle.Fill;
        this.pictureBox.Location = new Point(0, 37);
        this.pictureBox.Name = "pictureBox";
        this.pictureBox.Size = new Size(1000, 243);
        this.pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        this.pictureBox.TabIndex = 2;
        this.pictureBox.TabStop = false;
        //
        // statusStrip
        //
        this.statusStrip.Items.AddRange(new ToolStripItem[] { this.toolStripStatusLabel });
        this.statusStrip.Font = new Font("Microsoft YaHei UI", 9.75F);
        this.statusStrip.Location = new Point(0, 618);
        this.statusStrip.Name = "statusStrip";
        this.statusStrip.Size = new Size(1000, 22);
        this.statusStrip.TabIndex = 3;
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
        this.BackColor = Color.FromArgb(245, 246, 248);
        this.ClientSize = new Size(1000, 640);
        this.Controls.Add(this.mainTable);
        this.Controls.Add(this.pnlScreenshot);
        this.Controls.Add(this.topPanel);
        this.Controls.Add(this.statusStrip);
        // 9.75pt 在 96 DPI 下恰好是 13 像素，比 9pt 更适合高分辨率屏幕阅读。
        this.Font = new Font("Microsoft YaHei UI", 9.75F);
        this.MinimumSize = new Size(940, 600);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "第七史诗打铁助手";
        this.Load += new EventHandler(this.MainForm_Load);
        this.topPanel.ResumeLayout(false);
        this.topPanel.PerformLayout();
        this.mainTable.ResumeLayout(false);
        this.equipCard.ResumeLayout(false);
        this.equipTable.ResumeLayout(false);
        this.equipTable.PerformLayout();
        this.scorePanel.ResumeLayout(false);
        this.heroesPanel.ResumeLayout(false);
        this.heroesHeader.ResumeLayout(false);
        this.heroesHeader.PerformLayout();
        this.flowHeroes.ResumeLayout(false);
        this.flowHeroes.PerformLayout();
        this.pnlScreenshot.ResumeLayout(false);
        this.shotHeader.ResumeLayout(false);
        this.shotHeader.PerformLayout();
        this.advicePanel.ResumeLayout(false);
        this.advicePanel.PerformLayout();
        this.thresholdPanel.ResumeLayout(false);
        this.thresholdPanel.PerformLayout();
        this.recognitionSettingsPanel.ResumeLayout(false);
        this.recognitionSettingsPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.numLeftThreshold)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.numRightThreshold)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.numRecognitionInterval)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
        this.statusStrip.ResumeLayout(false);
        this.statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
