# nightreign-launcher

一键同时启动 **ELDEN RING: NIGHTREIGN** 的两个辅助程序：

1. 游戏启动器 `nrsc_launcher.exe`
2. [Nightreign Overlay Helper](https://github.com/nicohman/nightreign-overlay-helper) 之类的 overlay 辅助工具

双击一次，两个程序同时打开；已在运行的自动跳过，不会重复启动。

## 功能

- 🚀 **一键双开** — 双击同时拉起两个程序（间隔 2 秒，避免资源抢占）
- 🛡️ **防重复启动** — 自动检测进程，已在运行的直接跳过；全部在运行时会提示
- ⚙️ **图形化配置** — 设置窗口选择两个程序的路径，带"浏览"文件对话框
- 🔇 **静默运行** — 无黑框窗口，启动成功后自动退出，仅出错/需确认时弹窗
- 🎨 **可自定义图标** — 构建时可从游戏本体提取原始多尺寸图标（256/128/64/48/32/16）
- 📦 **零依赖** — 只用 Windows 自带的 .NET Framework 4 编译器，无需安装 SDK

## 下载与构建

需要 Windows 10/11（自带 .NET Framework 4 与 `csc.exe`）。

```powershell
git clone https://github.com/<你的用户名>/nightreign-launcher.git
cd nightreign-launcher
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

输出到 `bin\`：

| 文件 | 用途 |
|---|---|
| `LaunchNightreign.exe` | 启动器，双击运行 |
| `LaunchNightreignSettings.exe` | 设置窗口，选择两个程序的路径 |

构建参数（可选）：

```powershell
# 构建时从游戏本体提取图标（app.ico 不存在时）
.\build.ps1 -ExtractFrom "D:\ELDEN RING NIGHTREIGN\Game\nightreign.exe"

# 直接指定图标文件
.\build.ps1 -Icon "C:\path\to\app.ico"

# 构建并运行配置读写单元测试
.\build.ps1 -Test
```

不提供图标也能正常构建，只是使用系统默认图标。

## 使用

1. 把 `bin\` 里两个 exe 放到任意目录（建议放在游戏目录下）
2. **双击 `LaunchNightreign.exe`**
   - 首次运行（还没有 `config.ini`）会自动弹出设置窗口，选好路径点"保存"后立即启动
   - 之后再双击就直接静默启动了
3. 想改路径：双击 `LaunchNightreignSettings.exe`（或运行 `LaunchNightreign.exe --config`）

## 配置文件

程序目录下的 `config.ini`（UTF-8），设置窗口保存的就是它：

```ini
# nightreign-launcher 配置文件（可用 LaunchNightreignSettings.exe 修改）
launcher=D:\ELDEN RING NIGHTREIGN\Game\nrsc_launcher.exe
helper=D:\ELDEN RING NIGHTREIGN\nightreign-overlay-helper-v0.10.5\nightreign-overlay-helper.exe
```

也可以直接手改这个文件。两个键的值都是完整的 exe 路径；进程检测的名称自动取文件名（不含 `.exe`）。

## 项目结构

```
nightreign-launcher/
├── src/
│   ├── LauncherCore.cs      # 核心：配置读写 + 启动逻辑 + 配置窗口
│   ├── LauncherMain.cs      # 启动器入口（双击运行的那个）
│   └── SettingsMain.cs      # 设置窗口入口
├── tests/
│   └── ConfigTest.cs        # 配置读写单元测试（build.ps1 -Test）
├── tools/
│   └── ExtractIcon.cs       # 从 exe 的 PE 资源中提取多尺寸图标
├── build.ps1                # 一键构建脚本
├── LICENSE                  # MIT
├── .gitignore
└── README.md
```

## 工作原理

- **启动**：`Process.Start` 以目标程序自身目录为工作目录拉起，`UseShellExecute` 走系统 Shell
- **防重复**：`Process.GetProcessesByName(文件名)` 检测，已存在则跳过
- **图标**：直接解析 PE 资源表（`RT_GROUP_ICON` → `RT_ICON`），提取全部尺寸重组为 `.ico`

## License

MIT
