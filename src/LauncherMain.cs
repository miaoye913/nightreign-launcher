using System;
using System.IO;
using System.Windows.Forms;

/// <summary>
/// 启动器入口：
///   无参数     → 直接启动两个程序（已在运行的跳过），成功后自动退出
///   首次运行   → 配置文件不存在时自动打开设置界面，保存后接着启动
///   --config   → 打开设置界面
/// </summary>
class LauncherMain
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 命令行参数：打开设置界面
        foreach (string a in args)
        {
            if (string.Equals(a, "--config", StringComparison.OrdinalIgnoreCase)
                || string.Equals(a, "-c", StringComparison.OrdinalIgnoreCase)
                || string.Equals(a, "/config", StringComparison.OrdinalIgnoreCase))
            {
                LauncherCore.RunConfig(false);
                return;
            }
        }

        // 首次运行：没有配置文件 → 先让用户选择路径，保存后自动启动
        if (!File.Exists(LauncherCore.ConfigPath))
        {
            LauncherCore.RunConfig(true);
            return;
        }

        LauncherCore.Launch();
    }
}
