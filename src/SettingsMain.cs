using System;
using System.Windows.Forms;

/// <summary>
/// 设置入口：打开配置界面，保存后显示确认提示并退出（不执行启动）。
/// </summary>
class SettingsMain
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        LauncherCore.RunConfig(false);
    }
}
