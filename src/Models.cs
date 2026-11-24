using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AuraFolders
{
    public class AppItemData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TargetPath { get; set; }
        public string CustomIconPath { get; set; }

        [ScriptIgnore]
        public ImageSource CachedIcon { get; set; }

        public AppItemData()
        {
            Id = Guid.NewGuid().ToString("N");
        }
    }

    public class FolderData
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public List<AppItemData> Apps { get; set; }

        public FolderData()
        {
            Id = Guid.NewGuid().ToString("N");
            Title = "New Folder";
            Apps = new List<AppItemData>();
        }
    }

    public class AppConfig
    {
        public bool FirstRunCompleted { get; set; }
        public string ThemePreset { get; set; }
        public double CardOpacity { get; set; }
        public int GridColumns { get; set; }
        public double IdleTileScale { get; set; }
        public int HoverDelayMs { get; set; }
        public bool ShowCountBadge { get; set; }
        public List<FolderData> Folders { get; set; }

        public AppConfig()
        {
            ThemePreset = "Obsidian";
            CardOpacity = 0.94;
            GridColumns = 3;
            IdleTileScale = 1.0;
            HoverDelayMs = 80;
            ShowCountBadge = true;
            Folders = new List<FolderData>();
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrEmpty(ThemePreset)) ThemePreset = "Obsidian";
            if (CardOpacity < 0.4 || CardOpacity > 1.0) CardOpacity = 0.94;
            if (GridColumns < 3 || GridColumns > 4) GridColumns = 3;
            if (IdleTileScale < 0.8 || IdleTileScale > 1.4) IdleTileScale = 1.0;
            if (HoverDelayMs < 20 || HoverDelayMs > 400) HoverDelayMs = 80;
        }
    }

    public static class IconExtractor
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;

        private static readonly Dictionary<string, ImageSource> _cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache()
        {
            _cache.Clear();
        }

        public static ImageSource GetIconForApp(AppItemData item)
        {
            if (item == null) return null;
            if (item.CachedIcon != null) return item.CachedIcon;

            string key = (item.TargetPath ?? item.Name ?? "") + "|" + (item.CustomIconPath ?? "");
            if (_cache.ContainsKey(key))
            {
                item.CachedIcon = _cache[key];
                return item.CachedIcon;
            }

            ImageSource img = ExtractCleanIconInternal(item.TargetPath, item.CustomIconPath);
            if (img != null)
            {
                _cache[key] = img;
                item.CachedIcon = img;
            }
            return img;
        }

        private static ImageSource ExtractCleanIconInternal(string path, string customIconPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(customIconPath) && File.Exists(customIconPath))
                {
                    ImageSource customImg = LoadFromFileOrIcon(customIconPath, 0);
                    if (customImg != null) return customImg;
                }

                if (string.IsNullOrEmpty(path)) return null;

                // 1. If it's a .lnk shortcut, resolve its real target .exe or IconLocation so there is NO ugly Windows shortcut arrow!
                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                {
                    string targetPath;
                    string iconFile;
                    int iconIndex;
                    if (TryResolveLnkShortcut(path, out targetPath, out iconFile, out iconIndex))
                    {
                        if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
                        {
                            ImageSource icoImg = LoadFromFileOrIcon(iconFile, iconIndex);
                            if (icoImg != null) return icoImg;
                        }
                        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                        {
                            ImageSource targetImg = LoadFromFileOrIcon(targetPath, 0);
                            if (targetImg != null) return targetImg;
                        }
                    }
                }

                // 2. If it's a .url Internet Shortcut (Steam / Epic Games), parse IconFile=
                if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                {
                    string iconFile = ParseUrlIconFile(path);
                    if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
                    {
                        ImageSource urlIcon = LoadFromFileOrIcon(iconFile, 0);
                        if (urlIcon != null) return urlIcon;
                    }
                }

                // 3. Direct file extraction
                if (File.Exists(path))
                {
                    ImageSource direct = LoadFromFileOrIcon(path, 0);
                    if (direct != null) return direct;
                }

                if (File.Exists(path) || Directory.Exists(path))
                {
                    SHFILEINFO shinfo = new SHFILEINFO();
                    IntPtr res = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                    if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                    {
                        try
                        {
                            using (Icon ico = Icon.FromHandle(shinfo.hIcon))
                            {
                                return IconToImageSource(ico);
                            }
                        }
                        finally
                        {
                            DestroyIcon(shinfo.hIcon);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool TryResolveLnkShortcut(string lnkPath, out string targetPath, out string iconFile, out int iconIndex)
        {
            targetPath = null;
            iconFile = null;
            iconIndex = 0;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                if (shortcut == null) return false;

                Type scType = shortcut.GetType();
                targetPath = scType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
                string rawIconLoc = scType.InvokeMember("IconLocation", BindingFlags.GetProperty, null, shortcut, null) as string;

                if (!string.IsNullOrEmpty(rawIconLoc))
                {
                    int commaIdx = rawIconLoc.LastIndexOf(',');
                    if (commaIdx > 0)
                    {
                        string candidateFile = rawIconLoc.Substring(0, commaIdx).Trim().Trim('"');
                        string idxStr = rawIconLoc.Substring(commaIdx + 1).Trim();
                        int parsedIdx;
                        if (int.TryParse(idxStr, out parsedIdx))
                        {
                            iconIndex = parsedIdx;
                        }
                        if (!string.IsNullOrEmpty(candidateFile))
                        {
                            iconFile = Environment.ExpandEnvironmentVariables(candidateFile);
                        }
                    }
                    else
                    {
                        string candidateFile = rawIconLoc.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(candidateFile))
                        {
                            iconFile = Environment.ExpandEnvironmentVariables(candidateFile);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(targetPath))
                {
                    targetPath = Environment.ExpandEnvironmentVariables(targetPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ImageSource LoadFromFileOrIcon(string filePath, int iconIndex)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                if (ext == ".ico")
                {
                    using (Icon ico = new Icon(filePath, 48, 48))
                    {
                        return IconToImageSource(ico);
                    }
                }

                // Use ExtractIconEx on .exe / .dll to get clean large icon without shortcut overlay
                IntPtr[] largeIcons = new IntPtr[1];
                IntPtr[] smallIcons = new IntPtr[1];
                uint extracted = ExtractIconEx(filePath, iconIndex, largeIcons, smallIcons, 1);
                if (extracted > 0 && largeIcons[0] != IntPtr.Zero)
                {
                    try
                    {
                        using (Icon ico = Icon.FromHandle(largeIcons[0]))
                        {
                            return IconToImageSource(ico);
                        }
                    }
                    finally
                    {
                        if (largeIcons[0] != IntPtr.Zero) DestroyIcon(largeIcons[0]);
                        if (smallIcons[0] != IntPtr.Zero) DestroyIcon(smallIcons[0]);
                    }
                }
                if (smallIcons[0] != IntPtr.Zero) DestroyIcon(smallIcons[0]);

                if (!filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    using (Icon assoc = Icon.ExtractAssociatedIcon(filePath))
                    {
                        if (assoc != null) return IconToImageSource(assoc);
                    }
                }
            }
            catch { }
            return null;
        }

        private static string ParseUrlIconFile(string urlFilePath)
        {
            try
            {
                string[] lines = File.ReadAllLines(urlFilePath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    {
                        return trimmed.Substring("IconFile=".Length).Trim().Trim('"');
                    }
                }
            }
            catch { }
            return null;
        }

        private static ImageSource IconToImageSource(Icon ico)
        {
            using (Bitmap bmp = ico.ToBitmap())
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
        }
    }

    public static class ConfigManager
    {
        public static string ConfigFilePath
        {
            get
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(baseDir, "folders_config.json");
            }
        }

        public static AppConfig LoadOrCreateDefault(double screenWidth, double screenHeight)
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    AppConfig cfg = ser.Deserialize<AppConfig>(json);
                    if (cfg != null && cfg.Folders != null && cfg.Folders.Count > 0)
                    {
                        cfg.EnsureDefaults();
                        return cfg;
                    }
                }
            }
            catch { }

            AppConfig fresh = ScanDesktopAndBuildDefault(screenWidth, screenHeight);
            Save(fresh);
            return fresh;
        }

        public static void Save(AppConfig config)
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = ser.Serialize(config);
                File.WriteAllText(ConfigFilePath, json, Encoding.UTF8);
            }
            catch { }
        }

        public static AppConfig ScanDesktopAndBuildDefault(double screenWidth, double screenHeight)
        {
            FolderData devFolder = new FolderData { Title = "Dev & Creative" };
            FolderData socialFolder = new FolderData { Title = "Social & Work" };
            FolderData launchersFolder = new FolderData { Title = "Launchers & Tools" };
            FolderData gamesFolder = new FolderData { Title = "Games" };

            List<string> desktopDirs = new List<string>();
            AddIfValidDir(desktopDirs, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            AddIfValidDir(desktopDirs, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                AddIfValidDir(desktopDirs, Path.Combine(userProfile, "Desktop"));
                AddIfValidDir(desktopDirs, Path.Combine(userProfile, "OneDrive", "Desktop"));
            }
            AddIfValidDir(desktopDirs, @"C:\Users\Bagas\Desktop");
            AddIfValidDir(desktopDirs, @"C:\Users\Bagas\OneDrive\Desktop");
            AddIfValidDir(desktopDirs, @"C:\Users\Public\Desktop");

            HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string dir in desktopDirs)
            {
                try
                {
                    string[] files = Directory.GetFiles(dir);
                    foreach (string file in files)
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".lnk" && ext != ".url" && ext != ".exe") continue;

                        string name = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        if (name.IndexOf("AuraFolders", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        if (seenNames.Contains(name)) continue;

                        seenNames.Add(name);

                        AppItemData app = new AppItemData
                        {
                            Name = name,
                            TargetPath = file
                        };

                        string category = CategorizeShortcut(name, file);
                        if (category == "Dev") devFolder.Apps.Add(app);
                        else if (category == "Social") socialFolder.Apps.Add(app);
                        else if (category == "Launchers") launchersFolder.Apps.Add(app);
                        else gamesFolder.Apps.Add(app);
                    }
                }
                catch { }
            }

            List<FolderData> allFolders = new List<FolderData>
            {
                devFolder,
                socialFolder,
                gamesFolder,
                launchersFolder
            };

            double startX = Math.Max(120, (screenWidth / 2.0) - 180);
            double startY = Math.Max(120, (screenHeight / 2.0) - 130);
            double spacingX = 115;

            for (int i = 0; i < allFolders.Count; i++)
            {
                allFolders[i].X = startX + (i * spacingX);
                allFolders[i].Y = startY;
            }

            return new AppConfig
            {
                FirstRunCompleted = true,
                Folders = allFolders
            };
        }

        private static void AddIfValidDir(List<string> list, string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            try
            {
                if (Directory.Exists(dir))
                {
                    string full = Path.GetFullPath(dir);
                    foreach (string existing in list)
                    {
                        if (string.Equals(existing, full, StringComparison.OrdinalIgnoreCase)) return;
                    }
                    list.Add(full);
                }
            }
            catch { }
        }

        public static string CategorizeShortcut(string name, string filePath)
        {
            string lower = (name ?? "").ToLowerInvariant();

            if (!string.IsNullOrEmpty(filePath) && filePath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string content = File.ReadAllText(filePath).ToLowerInvariant();
                    if (content.Contains("steam://rungameid/") || content.Contains("com.epicgames.launcher://apps"))
                    {
                        if (!lower.Contains("wallpaper engine"))
                            return "Games";
                    }
                }
                catch { }
            }

            string[] devKeywords = new[]
            {
                "visual studio", "vs code", "code", "github", "antigravity", "figma",
                "davinci", "resolve", "gemini", "lumi", "lemonade", "roblox studio",
                "blender", "photoshop", "illustrator", "premiere", "after effects",
                "android studio", "intellij", "pycharm", "cursor", "docker", "git",
                "postman", "terminal", "unity", "unreal", "godot"
            };
            foreach (string kw in devKeywords)
            {
                if (lower.Contains(kw)) return "Dev";
            }

            string[] socialKeywords = new[]
            {
                "notion", "line", "zoom", "discord", "telegram", "whatsapp", "slack",
                "teams", "skype", "spotify", "chrome", "brave", "firefox", "edge",
                "opera", "obsidian", "word", "excel", "powerpoint", "outlook", "canva"
            };
            foreach (string kw in socialKeywords)
            {
                if (lower.Contains(kw)) return "Social";
            }

            string[] launcherToolKeywords = new[]
            {
                "steam", "epic games", "hoyoplay", "google play games", "riot client",
                "battle.net", "ubisoft", "ea app", "gog galaxy", "wallpaper engine",
                "logi options", "logitech", "razer", "winrar", "7-zip", "obs",
                "msi afterburner", "geforce", "nvidia", "amd", "task manager", "control panel"
            };
            foreach (string kw in launcherToolKeywords)
            {
                if (lower.Contains(kw)) return "Launchers";
            }

            return "Games";
        }
    }

    public static class DesktopNativeHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint WM_COMMAND = 0x0111;
        private static readonly IntPtr TOGGLE_DESKTOP_ICONS_CMD = new IntPtr(0x7402);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public const int SW_SHOWNOACTIVATE = 4;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint GA_ROOT = 2;

        public static void ToggleNativeDesktopIcons()
        {
            IntPtr defView = GetShellDefView();
            if (defView != IntPtr.Zero)
            {
                SendMessage(defView, WM_COMMAND, TOGGLE_DESKTOP_ICONS_CMD, IntPtr.Zero);
            }
        }

        public static bool AreNativeDesktopIconsVisible()
        {
            IntPtr defView = GetShellDefView();
            if (defView == IntPtr.Zero) return true;
            IntPtr listView = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
            if (listView == IntPtr.Zero) return true;
            return IsWindowVisible(listView);
        }

        private static IntPtr GetShellDefView()
        {
            IntPtr progman = FindWindow("Progman", "Program Manager");
            IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero) return defView;

            EnumWindows((hwnd, lParam) =>
            {
                IntPtr dv = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (dv != IntPtr.Zero)
                {
                    defView = dv;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return defView;
        }

        public static string GetWindowClassName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "";
            StringBuilder sb = new StringBuilder(128);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static bool IsDesktopClass(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return false;
            return cls == "Progman" ||
                   cls == "WorkerW" ||
                   cls == "SHELLDLL_DefView" ||
                   cls == "SysListView32" ||
                   cls.IndexOf("WPEngine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   cls.IndexOf("Wallpaper", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsTransientOverlayWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return true;
            string cls = GetWindowClassName(hwnd);

            if (cls == "Shell_TrayWnd" ||
                cls == "Shell_SecondaryTrayWnd" ||
                cls == "NotifyIconOverflowWindow" ||
                cls == "TopLevelWindowForOverflowXamlIsland" ||
                cls == "Windows.UI.Core.CoreWindow" ||
                cls == "XamlExplorerHostIslandWindow" ||
                cls.IndexOf("ScreenClipping", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cls.IndexOf("Snipping", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            uint exStyle = (uint)GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return true;

            return false;
        }

        public static bool IsForegroundDesktopOrOurApp(HashSet<IntPtr> ourWindows)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return true;
            if (ourWindows != null && ourWindows.Contains(fg)) return true;

            IntPtr root = GetAncestor(fg, GA_ROOT);
            if (root == IntPtr.Zero) root = fg;
            if (ourWindows != null && ourWindows.Contains(root)) return true;

            string cls = GetWindowClassName(fg);
            string rootCls = GetWindowClassName(root);
            if (IsDesktopClass(cls) || IsDesktopClass(rootCls))
                return true;

            return false;
        }

        public static int CheckPointVisibilityState(int x, int y, HashSet<IntPtr> ourWindows, out IntPtr coveringAppOut)
        {
            coveringAppOut = IntPtr.Zero;

            if (IsForegroundDesktopOrOurApp(ourWindows))
                return 1;

            IntPtr hit = WindowFromPoint(new POINT { X = x, Y = y });
            if (hit == IntPtr.Zero) return 1;

            IntPtr root = GetAncestor(hit, GA_ROOT);
            if (root == IntPtr.Zero) root = hit;

            if (ourWindows != null && (ourWindows.Contains(hit) || ourWindows.Contains(root)))
                return 0;

            string hitCls = GetWindowClassName(hit);
            string rootCls = GetWindowClassName(root);

            if (IsDesktopClass(hitCls) || IsDesktopClass(rootCls))
                return 1;

            if (IsTransientOverlayWindow(hit) || IsTransientOverlayWindow(root))
                return 0;

            try
            {
                uint pid;
                GetWindowThreadProcessId(root, out pid);
                if (pid != 0)
                {
                    using (Process proc = Process.GetProcessById((int)pid))
                    {
                        string pName = (proc.ProcessName ?? "").ToLowerInvariant();
                        if (pName.Contains("wallpaper") || pName.Contains("wpengine") || pName.Contains("rainmeter"))
                        {
                            return 1;
                        }
                        if (pName == "explorer" && rootCls != "CabinetWClass" && rootCls != "ExploreWClass")
                        {
                            return 1;
                        }
                    }
                }
            }
            catch { }

            coveringAppOut = root;
            return -1;
        }
    }
}
