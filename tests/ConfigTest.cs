using System;
using System.IO;

// 配置读写单元测试（用 csc -main 或直接同编译，入口为本 Main）
class ConfigTest
{
    static int Main()
    {
        bool ok = true;

        // 1. 无配置文件 → 默认值相对程序目录
        if (File.Exists(LauncherCore.ConfigPath)) File.Delete(LauncherCore.ConfigPath);
        string[] d = LauncherCore.LoadConfig();
        Console.WriteLine("default launcher=" + d[0]);
        Console.WriteLine("default helper  =" + d[1]);
        ok &= d[0].EndsWith(@"Game\nrsc_launcher.exe");
        ok &= d[1].EndsWith(@"nightreign-overlay-helper-v0.10.5\nightreign-overlay-helper.exe");

        // 2. 保存 → 重新读取（含中文与空格路径）
        LauncherCore.SaveConfig(@"C:\Games\launcher test\nrsc_launcher.exe", @"D:\工具\overlay helper.exe");
        string[] r = LauncherCore.LoadConfig();
        Console.WriteLine("reloaded launcher=" + r[0]);
        Console.WriteLine("reloaded helper  =" + r[1]);
        ok &= (r[0] == @"C:\Games\launcher test\nrsc_launcher.exe");
        ok &= (r[1] == @"D:\工具\overlay helper.exe");

        // 3. 注释、未知键、缺字段 → 缺的回落默认值
        File.WriteAllText(LauncherCore.ConfigPath, "# comment\n; comment2\nother=xxx\nlauncher=C:\\only.exe\n");
        string[] r2 = LauncherCore.LoadConfig();
        Console.WriteLine("partial launcher=" + r2[0]);
        Console.WriteLine("partial helper  =" + r2[1]);
        ok &= (r2[0] == @"C:\only.exe");
        ok &= r2[1].EndsWith(@"nightreign-overlay-helper-v0.10.5\nightreign-overlay-helper.exe");

        // 4. 空值忽略（launcher= 空 → 保持默认）
        File.WriteAllText(LauncherCore.ConfigPath, "launcher=\nhelper=D:\\h.exe\n");
        string[] r3 = LauncherCore.LoadConfig();
        ok &= (r3[0].Length > 0);
        ok &= (r3[1] == @"D:\h.exe");

        // 5. 带引号的值
        File.WriteAllText(LauncherCore.ConfigPath, "launcher=\"D:\\a b\\l.exe\"\nhelper=\"D:\\h.exe\"\n");
        string[] r4 = LauncherCore.LoadConfig();
        Console.WriteLine("quoted launcher=" + r4[0]);
        ok &= (r4[0] == @"D:\a b\l.exe");
        ok &= (r4[1] == @"D:\h.exe");

        // 6. 进程名推导（Launch 用文件名去掉 .exe）
        string n1 = Path.GetFileNameWithoutExtension(@"D:\ELDEN RING NIGHTREIGN\Game\nrsc_launcher.exe");
        string n2 = Path.GetFileNameWithoutExtension(@"D:\...\nightreign-overlay-helper.exe");
        Console.WriteLine("proc names: " + n1 + " / " + n2);
        ok &= (n1 == "nrsc_launcher" && n2 == "nightreign-overlay-helper");

        File.Delete(LauncherCore.ConfigPath);
        Console.WriteLine(ok ? "CONFIG-TEST-OK" : "CONFIG-TEST-FAIL");
        return ok ? 0 : 1;
    }
}
