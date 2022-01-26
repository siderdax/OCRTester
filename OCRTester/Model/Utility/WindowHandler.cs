using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OCRTester.Model.Utility
{
    public class WindowHandler
    {
        [DllImport("user32.dll")]
        public static extern int FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lclassName, string windowTitle);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern int SendMessage(int hWnd, uint Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern int PostMessage(int hWnd, uint Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hwndParent, StringBuilder lpEnumFunc, int nMaxCount);
        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr window, EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowRect(IntPtr hWnd, ref RECT rect);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int Width, int Height, bool Repaint);

        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        // Delegate to filter which windows to include 
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        // Event
        public delegate void GetWindowTextEvent(object sender, string windowText);
        public event GetWindowTextEvent OnWindowTextReceived;
        public delegate void GetChildNameEvent(object sender, string childName);
        public event GetWindowTextEvent OnChildNameReceived;
        public delegate void CaptureScreenEvent(object sender, Bitmap childName);
        public event CaptureScreenEvent OnScreenCaptured;
        public delegate void CheckScreenEvent(object sender, uint crc, uint cs, Action<bool> callback);
        public event CheckScreenEvent OnScreenChecked;

        public string DirPath { get; private set; }

        private readonly object _dictSync = new object();
        private readonly Dictionary<string, bool> _exitDict;
        private readonly uint[] _crc32Table;

        public void Run(string fileName, Action<bool> resultAction = null) =>
            Run(fileName, -1, -1, -1, -1, null, resultAction);

        public void Run(string fileName, int x, int y, int w, int h, string windowName, Action<bool> resultAction = null)
        {
            bool res = false;

            lock (_dictSync)
            {
                if (_exitDict.ContainsKey(fileName))
                {
                    if (_exitDict[fileName])
                        _exitDict[fileName] = false;
                    else
                        return;
                }
                else
                {
                    _exitDict.Add(fileName, false);
                }
            }

            try
            {
                using (StreamReader reader = File.OpenText(DirPath + fileName))
                {
                    WindowHandleContent content = JsonConvert.DeserializeObject<WindowHandleContent>(reader.ReadToEnd());

                    for (var i = 0; i < content.Items.Length; i++)
                    {
                        if (string.IsNullOrEmpty(windowName) && content.Items[i].Command.Contains("FindWindow"))
                        {
                            content.Items[i].Name = windowName;
                        }

                        if (x >= 0 && y >= 0 && w >= 0 && h >= 0 && content.Items[i].Command.Contains("Screen"))
                        {
                            content.Items[i].Args[0] = $"{x}";
                            content.Items[i].Args[1] = $"{y}";
                            content.Items[i].Args[2] = $"{w}";
                            content.Items[i].Args[3] = $"{h}";
                        }
                    }

                    res = Handle(fileName, IntPtr.Zero, content.Items);
                }
            }
            catch { }

            lock (_dictSync)
            {
                if (!_exitDict[fileName])
                {
                    _exitDict[fileName] = true;
                    resultAction?.Invoke(res);
                }
            }
        }

        public bool HasCommand(string fileName, string command)
        {
            bool res = false;

            try
            {
                using (StreamReader reader = File.OpenText(DirPath + fileName))
                {
                    WindowHandleContent content = JsonConvert.DeserializeObject<WindowHandleContent>(reader.ReadToEnd());
                    res = content.Items.Aggregate(false, (exist, next) => exist || next.Command.Equals(command));
                }
            }
            catch { }

            return res;
        }

        public void Stop(string fileName)
        {
            lock (_dictSync)
            {
                if (_exitDict.ContainsKey(fileName))
                    _exitDict[fileName] = true;
            }
        }

        public bool IsRunning(string fileName)
        {
            return !CheckCancel(fileName);
        }

        private bool Handle(string fileName, IntPtr windowHandle, WindowHandleContent.ContentItem[] items)
        {
            IntPtr handle = windowHandle;

            if (items == null)
                return true;

            foreach (var item in items)
            {
                bool success = false;

                bool getArg(int argIdx, Action<string> callback)
                {
                    bool ret = false;

                    if (item.Args != null && item.Args.Length > argIdx)
                    {
                        callback(item.Args[argIdx]);
                        ret = true;
                    }

                    return ret;
                }

                try
                {
                    Task.Delay(item.Delay).Wait();

                    switch (item.Command)
                    {
                        case "FindWindow":
                            {
                                int tryCount = 1;
                                int delay = 0;
                                string ignoreName = null, ignoreClass = null;

                                getArg(0, (arg) => int.TryParse(item.Args[0], out tryCount));
                                getArg(1, (arg) => int.TryParse(item.Args[1], out delay));
                                getArg(2, (arg) =>
                                {
                                    if (item.Args[2].ToUpper().Contains("CLASS:"))
                                        ignoreClass = item.Args[2].Split(':').Last().Trim();
                                    else
                                        ignoreName = item.Args[2].Split(':').Last().Trim();
                                });

                                for (int i = 0; i < tryCount; i++)
                                {
                                    handle = (IntPtr)FindWindow(item.Class, item.Name);

                                    if (handle != IntPtr.Zero || CheckCancel(fileName))
                                    {
                                        success = !CheckCancel(fileName);
                                        break;
                                    }
                                    else
                                    {
                                        if ((!string.IsNullOrWhiteSpace(ignoreName) || !string.IsNullOrWhiteSpace(ignoreClass))
                                            && FindWindow(ignoreClass, ignoreName) != 0 || CheckCancel(fileName))
                                            break;
                                        else
                                            Task.Delay(delay).Wait();
                                    }
                                }

                                break;
                            }
                        case "FindWindowEx":
                            {
                                int tryCount = 1, delay = 0, skip = 0;
                                string ignoreName = null, ignoreClass = null;
                                IntPtr childHandle = IntPtr.Zero;

                                getArg(0, (arg) => int.TryParse(item.Args[0], out tryCount));
                                getArg(1, (arg) => int.TryParse(item.Args[1], out delay));
                                getArg(2, (arg) => int.TryParse(item.Args[2], out skip));
                                getArg(3, (arg) =>
                                {
                                    if (item.Args[3].ToUpper().Contains("CLASS:"))
                                        ignoreClass = item.Args[3].Split(':').Last().Trim();
                                    else
                                        ignoreName = item.Args[3].Split(':').Last().Trim();
                                });

                                for (int i = 0; i < tryCount; i++)
                                {
                                    childHandle = IntPtr.Zero;
                                    childHandle = FindWindowEx(handle, childHandle, item.Class, item.Name);

                                    for (int j = 0; j < skip; j++)
                                    {
                                        if (childHandle == IntPtr.Zero) break;
                                        childHandle = FindWindowEx(handle, childHandle, item.Class, item.Name);
                                    }

                                    if (childHandle != IntPtr.Zero)
                                    {
                                        Handle(fileName, childHandle, item.ChildItem);
                                        success = !CheckCancel(fileName);
                                        break;
                                    }
                                    else
                                    {
                                        if ((!string.IsNullOrWhiteSpace(ignoreName) || !string.IsNullOrWhiteSpace(ignoreClass))
                                            && FindWindow(ignoreClass, ignoreName) != 0 || CheckCancel(fileName))
                                            break;
                                        else
                                            Task.Delay(delay).Wait();
                                    }
                                }
                            }
                            break;
                        case "SetForegroundWindow":
                            {
                                SetForegroundWindow(handle);
                                success = !CheckCancel(fileName);
                            }
                            break;
                        case "ShowWindow":
                            {
                                int cmd = 1;

                                getArg(0, (arg) =>
                                {
                                    switch (arg)
                                    {
                                        case "HIDE":
                                            cmd = 0;
                                            break;
                                        case "NORMAL":
                                        case "SHOWNORMAL":
                                            cmd = 1;
                                            break;
                                        case "MINIMIZED":
                                        case "SHOWMINIMIZED":
                                            cmd = 2;
                                            break;
                                        case "MAXIMIZED":
                                        case "SHOWMAXIMIZED":
                                        case "MAXIMIZE":
                                            cmd = 3;
                                            break;
                                        case "NOACTIVATE":
                                        case "SHOWNOACTIVATE":
                                            cmd = 4;
                                            break;
                                        case "SHOW":
                                            cmd = 5;
                                            break;
                                        case "MINIMIZE":
                                            cmd = 6;
                                            break;
                                        case "MINNOACTIVE":
                                        case "SHOWMINNOACTIVE":
                                            cmd = 7;
                                            break;
                                        case "NA":
                                        case "SHOWNA":
                                            cmd = 8;
                                            break;
                                        case "RESTORE":
                                            cmd = 9;
                                            break;
                                        case "DEFAULT":
                                        case "SHOWDEFAULT":
                                            cmd = 10;
                                            break;
                                    }
                                });

                                ShowWindowAsync(handle, cmd);
                                success = !CheckCancel(fileName);
                            }
                            break;
                        case "SendMessage":
                            {
                                if (int.TryParse(item.Args[0], out int cmd)
                                    && int.TryParse(item.Args[1], out int w) && int.TryParse(item.Args[2], out int l))
                                {
                                    SendMessage((int)handle, (uint)cmd, w, l);
                                    success = !CheckCancel(fileName);
                                }
                            }
                            break;
                        case "PostMessage":
                            {
                                if (int.TryParse(item.Args[0], out int cmd)
                                    && int.TryParse(item.Args[1], out int w) && int.TryParse(item.Args[2], out int l))
                                {
                                    PostMessage((int)handle, (uint)cmd, w, l);
                                    success = !CheckCancel(fileName);
                                }
                            }
                            break;
                        case "GetWindowText":
                            {
                                int length = GetWindowTextLength(handle) + 1;
                                var windowText = new StringBuilder(length);

                                GetWindowText(handle, windowText, length);
                                OnWindowTextReceived?.Invoke(this, windowText.ToString());
                                success = !CheckCancel(fileName);
                            }
                            break;
                        case "EnumChildWindows":
                            {
                                List<IntPtr> childHandles = new List<IntPtr>();
                                GCHandle gcChildHandlesList = GCHandle.Alloc(childHandles);
                                IntPtr pointerChildHandlesList = GCHandle.ToIntPtr(gcChildHandlesList);
                                int skip = 0, count = -1;
                                bool isChildrenExist = false;

                                getArg(1, (arg) => int.TryParse(item.Args[1], out skip));
                                getArg(2, (arg) => int.TryParse(item.Args[2], out count));

                                EnumChildWindows(handle, (wHnd, lParam) =>
                                {
                                    if (count == 0)
                                    {
                                        return false;
                                    }
                                    else if (skip > 0)
                                    {
                                        skip--;
                                        return !CheckCancel(fileName);
                                    }
                                    else
                                    {
                                        count--;
                                    }

                                    int textLength = GetWindowTextLength(wHnd);
                                    var childText = new StringBuilder(textLength);
                                    GetWindowText(wHnd, childText, 256);

                                    string childString = childText.ToString();

                                    if (!string.IsNullOrEmpty(item.Args[0]))
                                    {
                                        foreach (var arg in item.Args[0].Split('|'))
                                        {
                                            if (string.IsNullOrEmpty(arg) || childString.Contains(arg))
                                            {
                                                isChildrenExist = true;
                                                Handle(fileName, wHnd, item.ChildItem);
                                                OnChildNameReceived?.Invoke(this, childString);
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Handle(fileName, wHnd, item.ChildItem);
                                    }

                                    return !CheckCancel(fileName);
                                }, pointerChildHandlesList);

                                success = isChildrenExist && !CheckCancel(fileName);
                            }
                            break;
                        case "CopyFromScreen":
                            {
                                int x = 0, y = 0, w = 0, h = 0;
                                RECT rect = new RECT();
                                GetWindowRect(handle, ref rect);

                                getArg(0, (arg) => int.TryParse(item.Args[0], out x));
                                getArg(1, (arg) => int.TryParse(item.Args[1], out y));
                                getArg(2, (arg) => int.TryParse(item.Args[2], out w));
                                getArg(3, (arg) => int.TryParse(item.Args[3], out h));

                                if (w <= 0 || h <= 0 || x < 0 || y < 0)
                                    break;

                                Bitmap bm = new Bitmap(w, h);
                                Graphics gp = Graphics.FromImage(bm);

                                gp.CopyFromScreen(rect.left + x, rect.top + y, 0, 0, new Size(bm.Width, bm.Height));

                                OnScreenCaptured?.Invoke(this, bm);
                                success = !CheckCancel(fileName);
                            }
                            break;
                        case "MoveWindow":
                            {
                                int x = 0, y = 0, width, height;

                                RECT rect = new RECT();
                                GetWindowRect(handle, ref rect);

                                width = rect.right - rect.left;
                                height = rect.bottom - rect.top;

                                getArg(0, (arg) => int.TryParse(item.Args[0], out x));
                                getArg(1, (arg) => int.TryParse(item.Args[1], out y));
                                getArg(2, (arg) => int.TryParse(item.Args[2], out width));
                                getArg(3, (arg) => int.TryParse(item.Args[3], out height));

                                MoveWindow(handle, x, y, width, height, true);
                                success = !CheckCancel(fileName);
                            }
                            break;
                        case "CheckScreenCRC32":
                            {
                                int x = 0, y = 0, w = 0, h = 0;
                                uint cs = 0x00000000;
                                RECT rect = new RECT();
                                GetWindowRect(handle, ref rect);

                                getArg(0, (arg) => int.TryParse(item.Args[0], out x));
                                getArg(1, (arg) => int.TryParse(item.Args[1], out y));
                                getArg(2, (arg) => int.TryParse(item.Args[2], out w));
                                getArg(3, (arg) => int.TryParse(item.Args[3], out h));
                                getArg(4, (arg) => uint.TryParse(item.Args[4], out cs));

                                if (w <= 0 || h <= 0 || x < 0 || y < 0)
                                    break;

                                Bitmap bm = new Bitmap(w, h);
                                Graphics gp = Graphics.FromImage(bm);

                                gp.CopyFromScreen(rect.left + x, rect.top + y, 0, 0, new Size(bm.Width, bm.Height));
                                byte[] pixelArray = (byte[])new ImageConverter().ConvertTo(bm, typeof(byte[]));

                                uint crc32 = pixelArray.Aggregate(
                                    (uint)0,
                                    (crc, pxb) => _crc32Table[(crc & 0xFF) ^ pxb] ^ (crc >> 8));

                                bool eventResult = true;

                                if (OnScreenChecked != null)
                                    OnScreenChecked.Invoke(this, crc32, cs, (result) => eventResult = result);

                                success = eventResult && !CheckCancel(fileName);
                            }
                            break;
                    }
                }
                catch
                {
                    break;
                }

                if (!success)
                    return false;
            }

            return true;
        }

        private bool CheckCancel(string fileName)
        {
            lock (_dictSync)
            {
                if (_exitDict.ContainsKey(fileName))
                {
                    return _exitDict[fileName];
                }
                else
                {
                    return true;
                }
            }
        }

        public WindowHandler(string dirPath)
        {
            _exitDict = new Dictionary<string, bool>();
            _crc32Table = Enumerable.Range(0, 256).Select((x) =>
            {
                uint tableData = (uint)x;

                for (int i = 0; i < 8; i++)
                {
                    if ((tableData & 0x00000001) == 1)
                        tableData = 0xEDB88320 ^ (tableData >> 1);
                    else
                        tableData >>= 1;
                }

                return tableData;
            }).ToArray();

            try
            {
                DirectoryInfo di = new DirectoryInfo(dirPath);

                if (!di.Exists)
                    di.Create();

                DirPath = dirPath;

                if (!File.Exists(dirPath + "capture.json"))
                {
                    WindowHandleContent sample = new WindowHandleContent()
                    {
                        Items = new WindowHandleContent.ContentItem[]
                        {
                            new WindowHandleContent.ContentItem()
                            {
                                Name = "",
                                Command = "FindWindow",
                                Args = new string[] { "1", "1000" }
                            },
                            new WindowHandleContent.ContentItem()
                            {
                                Command = "ShowWindow",
                                Args = new string[] { "NORMAL" }
                            },
                            new WindowHandleContent.ContentItem()
                            {
                                Delay = 200,
                                Command = "SetForegroundWindow",
                            },
                            new WindowHandleContent.ContentItem()
                            {
                                Delay = 200,
                                Command = "CopyFromScreen",
                                Args = new string[] { "61", "359", "294", "21" },
                            },
                            new WindowHandleContent.ContentItem()
                            {
                                Command = "CheckScreenCRC32",
                                Args = new string[] { "20", "125", "125", "108" },
                            },
                        }
                    };

                    FileStream fs = new FileStream(dirPath + "capture.json", FileMode.OpenOrCreate);
                    StreamWriter sw = new StreamWriter(fs);
                    sw.Write(JsonConvert.SerializeObject(sample, Formatting.Indented));
                    sw.Close();
                }
            }
            catch { }
        }
    }
}
