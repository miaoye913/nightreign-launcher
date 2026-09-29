using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

/// <summary>
/// 启动器核心：配置文件读写、启动逻辑（含防重复启动）、图形化配置界面。
/// 配置保存在程序目录下的 config.ini，两个入口（启动器/设置）共用。
/// </summary>
class LauncherCore
{
    // 默认目标（相对本程序所在目录解析，新用户无需改代码即可用）
    static readonly string DefaultLauncher = @"Game\nrsc_launcher.exe";
    static readonly string DefaultHelper = @"nightreign-overlay-helper-v0.10.5\nightreign-overlay-helper.exe";

    /// <summary>配置文件路径（程序目录下的 config.ini）</summary>
    public static string ConfigPath
    {
        get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini"); }
    }

    /// <summary>
    /// 读取配置；文件缺失或字段缺失时回落到默认值。
    /// 返回 [0]=启动器路径 [1]=overlay helper 路径。
    /// </summary>
    public static string[] LoadConfig()
    {
        string launcher = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultLauncher);
        string helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultHelper);

        if (File.Exists(ConfigPath))
        {
            try
            {
                foreach (string raw in File.ReadAllLines(ConfigPath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim().Trim('"');
                    if (val.Length == 0) continue;
                    if (key == "launcher") launcher = val;
                    if (key == "helper") helper = val;
                }
            }
            catch
            {
                // 配置损坏时忽略，使用默认值
            }
        }
        return new string[] { launcher, helper };
    }

    /// <summary>保存配置到 config.ini（UTF-8）</summary>
    public static void SaveConfig(string launcher, string helper)
    {
        string dir = Path.GetDirectoryName(ConfigPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(ConfigPath,
            "# nightreign-launcher 配置文件（可用 LaunchNightreignSettings.exe 修改）\r\n" +
            "launcher=" + launcher + "\r\n" +
            "helper=" + helper + "\r\n");
    }

    /// <summary>
    /// 启动两个程序：文件缺失则提示；已在运行的跳过；全部在运行则提示。
    /// 任一情况下本方法返回后程序均自动退出（由消息框除外）。
    /// </summary>
    public static void Launch()
    {
        string[] paths = LoadConfig();
        // 进程名 = 配置文件名去掉 .exe（与配置路径联动，支持任意程序）
        string[] names = new string[]
        {
            Path.GetFileNameWithoutExtension(paths[0]),
            Path.GetFileNameWithoutExtension(paths[1])
        };

        // 文件存在性检查
        string missingList = "";
        for (int i = 0; i < paths.Length; i++)
        {
            if (!File.Exists(paths[i])) missingList += paths[i] + "\r\n";
        }
        if (missingList.Length > 0)
        {
            MessageBox.Show(
                "找不到以下程序，未启动任何内容：\r\n\r\n" + missingList +
                "\r\n可用 LaunchNightreignSettings.exe 重新选择路径。",
                "nightreign-launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool allRunning = true;
        for (int i = 0; i < paths.Length; i++)
        {
            // 已在运行则跳过，避免二次启动
            if (IsRunning(names[i])) continue;

            allRunning = false;
            try
            {
                Process p = StartWithWorkDir(paths[i]);
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动 " + Path.GetFileName(paths[i]) + " 失败：\r\n" + ex.Message,
                    "nightreign-launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // 两个程序之间留出间隔，避免抢占资源
            if (i == 0) System.Threading.Thread.Sleep(2000);
        }

        if (allRunning)
        {
            MessageBox.Show("两个程序都已在运行，无需重复启动。",
                "nightreign-launcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>打开配置界面。launchAfterSave=true 表示保存后接着启动（首次运行用）</summary>
    public static void RunConfig(bool launchAfterSave)
    {
        string[] paths = LoadConfig();
        using (ConfigForm form = new ConfigForm(paths[0], paths[1]))
        {
            if (form.ShowDialog() == DialogResult.OK)
            {
                SaveConfig(form.LauncherPath, form.HelperPath);
                if (launchAfterSave)
                {
                    Launch();
                }
                else
                {
                    MessageBox.Show("配置已保存：\r\n" + ConfigPath,
                        "nightreign-launcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }

    /// <summary>检测指定进程名是否已有实例在运行</summary>
    static bool IsRunning(string processName)
    {
        Process[] procs = Process.GetProcessesByName(processName);
        bool running = procs.Length > 0;
        foreach (Process p in procs) { p.Dispose(); }
        return running;
    }

    /// <summary>以目标程序所在目录为工作目录启动，避免相对路径失效</summary>
    static Process StartWithWorkDir(string exePath)
    {
        ProcessStartInfo psi = new ProcessStartInfo(exePath);
        psi.WorkingDirectory = Path.GetDirectoryName(exePath);
        psi.UseShellExecute = true;
        return Process.Start(psi);
    }
}

/// <summary>
/// 配置界面：两个文本框选择程序路径，带“浏览”按钮与保存校验。
/// </summary>
class ConfigForm : Form
{
    TextBox txtLauncher;
    TextBox txtHelper;
    Button btnBrowse1;
    Button btnBrowse2;
    Button btnOk;

    /// <summary>启动器路径（用户所选）</summary>
    public string LauncherPath { get { return txtLauncher.Text.Trim(); } }

    /// <summary>Overlay helper 路径（用户所选）</summary>
    public string HelperPath { get { return txtHelper.Text.Trim(); } }

    public ConfigForm(string launcherPath, string helperPath)
    {
        Text = "nightreign-launcher 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 210);
        ShowInTaskbar = true;

        Label lbl1 = new Label();
        lbl1.Text = "游戏启动器（nrsc_launcher.exe）：";
        lbl1.Location = new Point(15, 15);
        lbl1.AutoSize = true;

        txtLauncher = new TextBox();
        txtLauncher.Text = launcherPath;
        txtLauncher.Location = new Point(15, 38);
        txtLauncher.Size = new Size(525, 23);
        txtLauncher.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        btnBrowse1 = new Button();
        btnBrowse1.Text = "浏览...";
        btnBrowse1.Location = new Point(550, 36);
        btnBrowse1.Size = new Size(75, 27);
        btnBrowse1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowse1.Click += Browse1;

        Label lbl2 = new Label();
        lbl2.Text = "Overlay 辅助工具（nightreign-overlay-helper.exe）：";
        lbl2.Location = new Point(15, 72);
        lbl2.AutoSize = true;

        txtHelper = new TextBox();
        txtHelper.Text = helperPath;
        txtHelper.Location = new Point(15, 95);
        txtHelper.Size = new Size(525, 23);
        txtHelper.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        btnBrowse2 = new Button();
        btnBrowse2.Text = "浏览...";
        btnBrowse2.Location = new Point(550, 93);
        btnBrowse2.Size = new Size(75, 27);
        btnBrowse2.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowse2.Click += Browse2;

        Label lblHint = new Label();
        lblHint.Text = "配置保存在程序目录的 config.ini；修改后再次双击启动器即可生效。";
        lblHint.Location = new Point(15, 135);
        lblHint.AutoSize = true;
        lblHint.ForeColor = Color.Gray;

        btnOk = new Button();
        btnOk.Text = "保存";
        btnOk.Location = new Point(470, 165);
        btnOk.Size = new Size(75, 27);
        btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        Button btnCancel = new Button();
        btnCancel.Text = "取消";
        btnCancel.Location = new Point(550, 165);
        btnCancel.Size = new Size(75, 27);
        btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnCancel.DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[]
        {
            lbl1, txtLauncher, btnBrowse1,
            lbl2, txtHelper, btnBrowse2,
            lblHint, btnOk, btnCancel
        });

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        btnOk.Click += OkClick;
    }

    void Browse1(object sender, EventArgs e) { BrowseFor(txtLauncher); }

    void Browse2(object sender, EventArgs e) { BrowseFor(txtHelper); }

    /// <summary>打开文件选择对话框，把选中的 exe 路径填入目标文本框</summary>
    void BrowseFor(TextBox target)
    {
        using (OpenFileDialog dlg = new OpenFileDialog())
        {
            dlg.Title = "选择程序文件";
            dlg.Filter = "程序文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
            try
            {
                string dir = Path.GetDirectoryName(target.Text);
                if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
            }
            catch { }
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                target.Text = dlg.FileName;
            }
        }
    }

    /// <summary>保存前校验：路径非空；文件不存在时二次确认</summary>
    void OkClick(object sender, EventArgs e)
    {
        if (LauncherPath.Length == 0 || HelperPath.Length == 0)
        {
            MessageBox.Show("请为两个程序都选择路径。", "nightreign-launcher",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string notFound = "";
        if (!File.Exists(LauncherPath)) notFound += LauncherPath + "\r\n";
        if (!File.Exists(HelperPath)) notFound += HelperPath + "\r\n";
        if (notFound.Length > 0)
        {
            DialogResult r = MessageBox.Show(
                "以下文件不存在：\r\n\r\n" + notFound + "\r\n仍要保存吗？",
                "nightreign-launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
