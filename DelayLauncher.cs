using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("延时启动器")]
[assembly: System.Reflection.AssemblyDescription("延迟指定时间打开另一个程序，然后自动退出")]
[assembly: System.Reflection.AssemblyProduct("DelayLauncher")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]

namespace DelayLauncher
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            try { SetProcessDPIAware(); }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show("参数错误：\r\n\r\n" + ex.Message + "\r\n\r\n" + Options.HelpText,
                    "延时启动器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }

            if (options.ShowHelp)
            {
                MessageBox.Show(Options.HelpText, "延时启动器 - 使用说明",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            if (options.Silent)
            {
                if (options.Target.Length == 0)
                {
                    Log("错误：--silent 模式必须用 --target 指定要启动的程序。");
                    return 2;
                }

                try
                {
                    if (options.DelaySeconds > 0)
                    {
                        int milliseconds = (int)Math.Round(options.DelaySeconds * 1000.0);
                        System.Threading.Thread.Sleep(milliseconds);
                    }

                    Launcher.Launch(options.Target, options.Arguments);
                    return 0;
                }
                catch (Exception ex)
                {
                    Log("静默模式启动失败：" + ex);
                    return 1;
                }
            }

            Application.Run(new MainForm(options));
            return 0;
        }

        internal static void Log(string message)
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "DelayLauncher");
                Directory.CreateDirectory(dir);
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    + "  " + message + Environment.NewLine;
                File.AppendAllText(Path.Combine(dir, "launcher.log"), line, Encoding.UTF8);
            }
            catch
            {
                // 日志失败不能影响主流程。
            }
        }
    }

    internal sealed class Options
    {
        public double DelaySeconds = 10.0;
        public string Target = "";
        public string Arguments = "";
        public bool Silent;
        public bool AutoExit = true;
        public bool AutoStart;
        public bool ShowHelp;
        public bool HasDelay;

        public const string HelpText =
            "用法：\r\n" +
            "  DelayLauncher.exe\r\n" +
            "      打开图形界面。\r\n\r\n" +
            "  DelayLauncher.exe --delay 30 --target \"C:\\App\\app.exe\" [--args \"...\"] [--silent] [--noexit]\r\n" +
            "      带参数启动时，会自动填入并立刻开始倒计时。\r\n\r\n" +
            "参数：\r\n" +
            "  --delay, -d     延迟时间，默认 10 秒；可写 30、30s、5m、1h\r\n" +
            "  --target, -t    要启动的程序、文件或网址\r\n" +
            "  --args, -a      传给目标程序的参数\r\n" +
            "  --silent, -s    不显示界面，延时后启动目标并立即退出\r\n" +
            "  --noexit        启动目标后不退出（仅图形界面有效）\r\n" +
            "  --help, -h      显示本帮助\r\n\r\n" +
            "示例：\r\n" +
            "  DelayLauncher.exe --delay 1m --target \"C:\\Windows\\System32\\notepad.exe\"\r\n" +
            "  DelayLauncher.exe -d 15 -t \"C:\\Games\\game.exe\" -a \"-windowed\" -s\r\n\r\n" +
            "静默模式出错时，日志写到：%TEMP%\\DelayLauncher\\launcher.log";

        public static Options Parse(string[] args)
        {
            Options options = new Options();
            if (args == null) return options;

            for (int i = 0; i < args.Length; i++)
            {
                string raw = args[i];
                if (raw == null) continue;
                string token = raw.Trim();
                if (token.Length == 0) continue;

                string key = token;
                string inlineValue = null;

                if (token.StartsWith("--", StringComparison.Ordinal))
                {
                    int equals = token.IndexOf('=');
                    if (equals > 2)
                    {
                        key = token.Substring(0, equals);
                        inlineValue = token.Substring(equals + 1);
                    }
                }
                else if (token.StartsWith("/", StringComparison.Ordinal))
                {
                    int colon = token.IndexOf(':');
                    if (colon > 1)
                    {
                        key = token.Substring(0, colon);
                        inlineValue = token.Substring(colon + 1);
                    }
                }

                string normalized = key.ToLowerInvariant();

                switch (normalized)
                {
                    case "--delay":
                    case "-d":
                    case "/delay":
                        options.DelaySeconds = ParseDelay(inlineValue ?? NextValue(args, ref i, key));
                        options.HasDelay = true;
                        break;

                    case "--target":
                    case "-t":
                    case "/target":
                    case "--file":
                    case "/file":
                    case "--program":
                    case "/program":
                        options.Target = Unquote(inlineValue ?? NextValue(args, ref i, key));
                        break;

                    case "--args":
                    case "-a":
                    case "/args":
                    case "--arguments":
                    case "/arguments":
                        options.Arguments = inlineValue ?? NextValue(args, ref i, key);
                        break;

                    case "--silent":
                    case "-s":
                    case "/silent":
                        options.Silent = true;
                        break;

                    case "--noexit":
                    case "/noexit":
                        options.AutoExit = false;
                        break;

                    case "--help":
                    case "-h":
                    case "-?":
                    case "/?":
                    case "/help":
                        options.ShowHelp = true;
                        break;

                    default:
                        if (token.StartsWith("-", StringComparison.Ordinal)
                            || token.StartsWith("/", StringComparison.Ordinal))
                        {
                            throw new ArgumentException("未知参数：" + key);
                        }

                        if (options.Target.Length == 0)
                        {
                            options.Target = token;
                        }
                        else
                        {
                            options.Arguments = AppendArgument(options.Arguments, token);
                        }
                        break;
                }
            }

            if (options.Target.Length > 0) options.AutoStart = true;
            return options;
        }

        private static string NextValue(string[] args, ref int index, string key)
        {
            if (index + 1 >= args.Length)
                throw new ArgumentException("参数 " + key + " 后面缺少值。");
            index++;
            return args[index];
        }

        private static string AppendArgument(string existing, string value)
        {
            string quoted = value;
            if (value.Length == 0 || value.IndexOf(' ') >= 0 || value.IndexOf('\t') >= 0)
                quoted = "\"" + value.Replace("\"", "\\\"") + "\"";

            return existing.Length == 0 ? quoted : existing + " " + quoted;
        }

        private static string Unquote(string value)
        {
            string text = value == null ? "" : value.Trim();
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
                text = text.Substring(1, text.Length - 2);
            return text;
        }

        private static double ParseDelay(string value)
        {
            string text = value == null ? "" : value.Trim().ToLowerInvariant();
            if (text.Length == 0)
                throw new ArgumentException("延迟时间不能为空。");

            double factor = 1.0;
            string number = text;

            if (text.EndsWith("s", StringComparison.Ordinal))
            {
                factor = 1.0;
                number = text.Substring(0, text.Length - 1);
            }
            else if (text.EndsWith("m", StringComparison.Ordinal))
            {
                factor = 60.0;
                number = text.Substring(0, text.Length - 1);
            }
            else if (text.EndsWith("h", StringComparison.Ordinal))
            {
                factor = 3600.0;
                number = text.Substring(0, text.Length - 1);
            }
            else if (text.EndsWith("d", StringComparison.Ordinal))
            {
                factor = 86400.0;
                number = text.Substring(0, text.Length - 1);
            }

            double parsed;
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                && !double.TryParse(number, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                TimeSpan span;
                if (TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out span))
                {
                    double seconds = span.TotalSeconds;
                    if (seconds < 0 || seconds > 86400)
                        throw new ArgumentException("延迟时间需在 0 到 24 小时之间。");
                    return seconds;
                }

                throw new ArgumentException("无法识别的延迟时间：" + value);
            }

            double total = parsed * factor;
            if (total < 0 || total > 86400)
                throw new ArgumentException("延迟时间需在 0 到 24 小时之间。");
            return total;
        }
    }

    internal static class Launcher
    {
        public static void Launch(string target, string arguments)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("没有指定要启动的程序。");

            string fileName = Unquote(Environment.ExpandEnvironmentVariables(target.Trim()));
            if (fileName.Length == 0)
                throw new ArgumentException("没有指定要启动的程序。");

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments == null ? "" : arguments;
            startInfo.UseShellExecute = true;

            try
            {
                string fullPath = Path.GetFullPath(fileName);
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    string dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        startInfo.WorkingDirectory = dir;
                }
            }
            catch
            {
                // 网址或 Shell 别名没有本地目录，保持默认工作目录。
            }

            Process.Start(startInfo);
            Program.Log("已启动：" + fileName);
        }

        private static string Unquote(string value)
        {
            string text = value == null ? "" : value.Trim();
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
                text = text.Substring(1, text.Length - 2);
            return text;
        }
    }

    internal sealed class UserSettings
    {
        public decimal Delay = 10;
        public int Unit;
        public string Target = "";
        public string Arguments = "";
        public bool AutoExit = true;
        public bool AutoStartLongDelay = true;
    }

    internal static class SettingsStore
    {
        private static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DelayLauncher");
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static UserSettings Load()
        {
            UserSettings settings = new UserSettings();
            try
            {
                if (!File.Exists(SettingsPath)) return settings;

                foreach (string line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    int equals = line.IndexOf('=');
                    if (equals <= 0) continue;

                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();

                    if (key == "delay")
                    {
                        decimal delay;
                        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out delay))
                            settings.Delay = delay;
                    }
                    else if (key == "unit")
                    {
                        int unit;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out unit)
                            && unit >= 0 && unit <= 2)
                            settings.Unit = unit;
                    }
                    else if (key == "target")
                    {
                        settings.Target = FromBase64(value);
                    }
                    else if (key == "args")
                    {
                        settings.Arguments = FromBase64(value);
                    }
                    else if (key == "autoexit")
                    {
                        settings.AutoExit = value == "1";
                    }
                    else if (key == "autostartlong")
                    {
                        settings.AutoStartLongDelay = value != "0";
                    }
                }
            }
            catch
            {
                // 设置损坏时使用默认值。
            }

            return settings;
        }

        public static void Save(decimal delay, int unit, string target, string arguments,
            bool autoExit, bool autoStartLongDelay)
        {
            try
            {
                string path = SettingsPath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("delay=" + delay.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("unit=" + unit.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("target=" + ToBase64(target == null ? "" : target));
                builder.AppendLine("args=" + ToBase64(arguments == null ? "" : arguments));
                builder.AppendLine("autoexit=" + (autoExit ? "1" : "0"));
                builder.AppendLine("autostartlong=" + (autoStartLongDelay ? "1" : "0"));

                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            }
            catch
            {
                // 保存失败不影响程序运行。
            }
        }

        private static string ToBase64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value == null ? "" : value));
        }

        private static string FromBase64(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return "";
            }
        }
    }

    internal sealed class HeaderPanel : Panel
    {
        public HeaderPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (ClientRectangle.Width > 0 && ClientRectangle.Height > 0)
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    ClientRectangle,
                    Color.FromArgb(24, 90, 210),
                    Color.FromArgb(58, 163, 255),
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(brush, ClientRectangle);
                }

                using (SolidBrush glow = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                {
                    e.Graphics.FillEllipse(glow, Width - 90, -52, 160, 160);
                    e.Graphics.FillEllipse(glow, Width - 36, 55, 74, 74);
                }
            }

            base.OnPaint(e);
        }
    }

    internal sealed class ProgressStrip : Control
    {
        private int _value;
        private int _maximum = 1000;

        public ProgressStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw, true);
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                if (value < 1) value = 1;
                _maximum = value;
                if (_value > _maximum) _value = _maximum;
                Invalidate();
            }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int next = value;
                if (next < 0) next = 0;
                if (next > _maximum) next = _maximum;
                if (next != _value)
                {
                    _value = next;
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Width, Height);
            if (track.Width <= 0 || track.Height <= 0) return;

            using (GraphicsPath trackPath = RoundedRect(track, track.Height / 2f))
            using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(229, 231, 235)))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            int fillWidth = (int)Math.Round(Width * (_value / (double)_maximum));
            if (fillWidth > 0)
            {
                Rectangle fill = new Rectangle(0, 0, fillWidth, Height);
                using (GraphicsPath fillPath = RoundedRect(fill, fill.Height / 2f))
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                    fill, Color.FromArgb(59, 130, 246), Color.FromArgb(31, 111, 235),
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillPath(fillBrush, fillPath);
                }
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2f;
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Accent = Color.FromArgb(31, 111, 235);
        private static readonly Color AccentDark = Color.FromArgb(21, 94, 208);
        private static readonly Color Ink = Color.FromArgb(31, 41, 55);
        private static readonly Color Muted = Color.FromArgb(107, 114, 128);
        private static readonly Color Border = Color.FromArgb(209, 213, 219);
        private static readonly Color Success = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger = Color.FromArgb(220, 38, 38);

        private readonly Options _options;

        private NumericUpDown _delay;
        private ComboBox _unit;
        private TextBox _target;
        private TextBox _arguments;
        private CheckBox _autoExit;
        private CheckBox _autoStartLong;
        private Label _status;
        private Label _hint;
        private ProgressStrip _progress;
        private Button _start;
        private Button _cancel;
        private Button _browse;
        private ToolTip _toolTip;

        private Timer _countdownTimer;
        private Timer _closeTimer;
        private DateTime _deadline;
        private int _totalMilliseconds;
        private int _lastUnitIndex;
        private bool _running;

        public MainForm(Options options)
        {
            _options = options ?? new Options();
            BuildUi();
            ApplySettings();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_options.AutoStart)
            {
                BeginInvoke(new MethodInvoker(StartCountdown));
            }
            else if (ShouldAutoStartOnLaunch())
            {
                BeginInvoke(new MethodInvoker(StartCountdown));
            }
            else
            {
                BeginInvoke(new MethodInvoker(delegate { _target.Focus(); }));
            }
        }

        private bool ShouldAutoStartOnLaunch()
        {
            if (!_autoStartLong.Checked) return false;

            string target = _target.Text.Trim();
            if (target.Length == 0) return false;

            double seconds;
            if (!TryGetDelaySeconds(out seconds)) return false;
            if (seconds <= 10.0) return false;

            // 自动开始时不弹窗询问；目标明显无效就留在界面里让用户处理。
            return ValidateTarget(target, false);
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = "延时启动器";
            ClientSize = new Size(578, 472);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            DoubleBuffered = true;

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            HeaderPanel header = new HeaderPanel();
            header.Dock = DockStyle.Top;
            header.Height = 88;
            Controls.Add(header);

            PictureBox clock = new PictureBox();
            clock.Location = new Point(25, 20);
            clock.Size = new Size(48, 48);
            clock.BackColor = Color.Transparent;
            clock.Image = CreateClockImage(48);
            header.Controls.Add(clock);

            Label title = new Label();
            title.AutoSize = true;
            title.BackColor = Color.Transparent;
            title.ForeColor = Color.White;
            title.Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold, GraphicsUnit.Point);
            title.Location = new Point(86, 15);
            title.Text = "延时启动器";
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.AutoSize = true;
            subtitle.BackColor = Color.Transparent;
            subtitle.ForeColor = Color.FromArgb(225, 240, 255);
            subtitle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            subtitle.Location = new Point(88, 51);
            subtitle.Text = "到点自动打开指定程序，随后本程序自动退出";
            header.Controls.Add(subtitle);

            Label version = new Label();
            version.AutoSize = true;
            version.BackColor = Color.Transparent;
            version.ForeColor = Color.FromArgb(185, 220, 255);
            version.Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
            version.Location = new Point(528, 20);
            version.Text = "v1.1";
            header.Controls.Add(version);

            Label delayLabel = MakeLabel("延迟时间", 24, 108, 9.5F, FontStyle.Bold, Ink);
            Controls.Add(delayLabel);

            _delay = new NumericUpDown();
            _delay.Location = new Point(108, 130);
            _delay.Size = new Size(92, 27);
            _delay.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            _delay.Minimum = 0;
            _delay.Maximum = 86400;
            _delay.Value = 10;
            _delay.Increment = 1;
            _delay.ThousandsSeparator = true;
            _delay.TextAlign = HorizontalAlignment.Right;
            _delay.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_delay);

            _unit = new ComboBox();
            _unit.Location = new Point(208, 130);
            _unit.Size = new Size(78, 27);
            _unit.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            _unit.DropDownStyle = ComboBoxStyle.DropDownList;
            _unit.Items.AddRange(new object[] { "秒", "分钟", "小时" });
            _unit.SelectedIndex = 0;
            _unit.SelectedIndexChanged += UnitChanged;
            Controls.Add(_unit);

            _hint = MakeLabel("最长 24 小时", 298, 135, 9F, FontStyle.Regular, Muted);
            Controls.Add(_hint);

            Label targetLabel = MakeLabel("要启动的程序 / 文件", 24, 176, 9.5F, FontStyle.Bold, Ink);
            Controls.Add(targetLabel);

            _target = new TextBox();
            _target.Location = new Point(24, 198);
            _target.Size = new Size(440, 28);
            _target.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            _target.BorderStyle = BorderStyle.FixedSingle;
            _target.AllowDrop = true;
            _target.DragEnter += TargetDragEnter;
            _target.DragDrop += TargetDragDrop;
            SetCueBanner(_target, "例如：C:\\Program Files\\App\\App.exe");
            Controls.Add(_target);

            _browse = new Button();
            _browse.Location = new Point(472, 197);
            _browse.Size = new Size(82, 29);
            _browse.Text = "浏览…";
            _browse.FlatStyle = FlatStyle.System;
            _browse.Cursor = Cursors.Hand;
            _browse.Click += BrowseClick;
            Controls.Add(_browse);

            Label argsLabel = MakeLabel("启动参数（可选）", 24, 240, 9.5F, FontStyle.Bold, Ink);
            Controls.Add(argsLabel);

            _arguments = new TextBox();
            _arguments.Location = new Point(24, 262);
            _arguments.Size = new Size(530, 28);
            _arguments.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            _arguments.BorderStyle = BorderStyle.FixedSingle;
            SetCueBanner(_arguments, "例如：--fullscreen 或 /silent");
            Controls.Add(_arguments);

            _autoExit = new CheckBox();
            _autoExit.AutoSize = true;
            _autoExit.Location = new Point(24, 302);
            _autoExit.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _autoExit.ForeColor = Ink;
            _autoExit.Text = "启动目标程序后，本程序自动退出";
            _autoExit.Checked = true;
            _autoExit.Cursor = Cursors.Hand;
            Controls.Add(_autoExit);

            _autoStartLong = new CheckBox();
            _autoStartLong.AutoSize = true;
            _autoStartLong.Location = new Point(24, 326);
            _autoStartLong.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _autoStartLong.ForeColor = Ink;
            _autoStartLong.Text = "延迟超过 10 秒时，启动即自动倒计时";
            _autoStartLong.Checked = true;
            _autoStartLong.Cursor = Cursors.Hand;
            Controls.Add(_autoStartLong);

            _toolTip = new ToolTip();
            _toolTip.ShowAlways = true;
            _toolTip.SetToolTip(_autoStartLong,
                "勾选后，如果已保存目标程序且延迟超过 10 秒，打开本程序会直接开始倒计时，\r\n"
                + "无需点击“开始倒计时”。取消勾选则始终等待手动点击。");

            _status = new Label();
            _status.Location = new Point(24, 358);
            _status.Size = new Size(530, 34);
            _status.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _status.ForeColor = Muted;
            _status.Text = "选择程序并设置延迟，然后点击“开始倒计时”。";
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.AutoEllipsis = true;
            Controls.Add(_status);

            _progress = new ProgressStrip();
            _progress.Location = new Point(24, 398);
            _progress.Size = new Size(530, 6);
            _progress.Maximum = 1000;
            _progress.Visible = false;
            Controls.Add(_progress);

            _start = new Button();
            _start.Location = new Point(280, 414);
            _start.Size = new Size(150, 38);
            _start.Text = "开始倒计时";
            _start.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            _start.ForeColor = Color.White;
            _start.BackColor = Accent;
            _start.FlatStyle = FlatStyle.Flat;
            _start.FlatAppearance.BorderSize = 0;
            _start.FlatAppearance.MouseOverBackColor = AccentDark;
            _start.FlatAppearance.MouseDownBackColor = Color.FromArgb(16, 74, 166);
            _start.Cursor = Cursors.Hand;
            _start.Click += StartClick;
            Controls.Add(_start);

            _cancel = new Button();
            _cancel.Location = new Point(440, 414);
            _cancel.Size = new Size(114, 38);
            _cancel.Text = "取消";
            _cancel.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            _cancel.ForeColor = Ink;
            _cancel.BackColor = Color.White;
            _cancel.FlatStyle = FlatStyle.Flat;
            _cancel.FlatAppearance.BorderColor = Border;
            _cancel.FlatAppearance.BorderSize = 1;
            _cancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 244, 246);
            _cancel.FlatAppearance.MouseDownBackColor = Color.FromArgb(229, 231, 235);
            _cancel.Cursor = Cursors.Hand;
            _cancel.Enabled = false;
            _cancel.Click += CancelClick;
            Controls.Add(_cancel);

            AcceptButton = _start;
            CancelButton = _cancel;

            _countdownTimer = new Timer();
            _countdownTimer.Interval = 100;
            _countdownTimer.Tick += CountdownTick;

            _closeTimer = new Timer();
            _closeTimer.Interval = 800;
            _closeTimer.Tick += CloseTimerTick;

            FormClosing += MainFormClosing;

            ResumeLayout(false);
        }

        private static Label MakeLabel(string text, int x, int y, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.AutoSize = true;
            label.Location = new Point(x, y);
            label.Font = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Text = text;
            return label;
        }

        private void ApplySettings()
        {
            UserSettings saved = SettingsStore.Load();

            _unit.SelectedIndex = saved.Unit;
            decimal max = _delay.Maximum;
            decimal value = saved.Delay;
            if (value < 0) value = 0;
            if (value > max) value = max;
            _delay.Value = value;

            _target.Text = saved.Target;
            _arguments.Text = saved.Arguments;
            _autoExit.Checked = saved.AutoExit;
            _autoStartLong.Checked = saved.AutoStartLongDelay;

            if (_options.HasDelay)
                ApplyDelaySeconds(_options.DelaySeconds);

            if (_options.Target.Length > 0)
            {
                _target.Text = _options.Target;
                _arguments.Text = _options.Arguments;
                _autoExit.Checked = _options.AutoExit;
            }
        }

        private void ApplyDelaySeconds(double seconds)
        {
            if (seconds >= 3600 && seconds % 3600 == 0)
            {
                _unit.SelectedIndex = 2;
                _delay.Value = ClampDecimal((decimal)(seconds / 3600.0), _delay.Minimum, _delay.Maximum);
            }
            else if (seconds >= 60 && seconds % 60 == 0)
            {
                _unit.SelectedIndex = 1;
                _delay.Value = ClampDecimal((decimal)(seconds / 60.0), _delay.Minimum, _delay.Maximum);
            }
            else
            {
                _unit.SelectedIndex = 0;
                _delay.Value = ClampDecimal((decimal)seconds, _delay.Minimum, _delay.Maximum);
            }
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void UnitChanged(object sender, EventArgs e)
        {
            int newUnit = _unit.SelectedIndex;
            if (newUnit < 0) return;

            decimal oldValue = _delay.Value;
            double oldSeconds = (double)oldValue * UnitMultiplier(_lastUnitIndex);

            if (newUnit == 0)
            {
                _delay.Maximum = 86400;
                _hint.Text = "最长 24 小时";
            }
            else if (newUnit == 1)
            {
                _delay.Maximum = 1440;
                _hint.Text = "最长 24 小时";
            }
            else
            {
                _delay.Maximum = 24;
                _hint.Text = "最长 24 小时";
            }

            double converted = oldSeconds / UnitMultiplier(newUnit);
            decimal rounded = Math.Round((decimal)converted, 0, MidpointRounding.AwayFromZero);
            if (rounded < _delay.Minimum) rounded = _delay.Minimum;
            if (rounded > _delay.Maximum) rounded = _delay.Maximum;
            _delay.Value = rounded;

            _lastUnitIndex = newUnit;
        }

        private static double UnitMultiplier(int unitIndex)
        {
            if (unitIndex == 1) return 60.0;
            if (unitIndex == 2) return 3600.0;
            return 1.0;
        }

        private void BrowseClick(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择要启动的程序";
                dialog.Filter = "程序 (*.exe;*.bat;*.cmd;*.lnk)|*.exe;*.bat;*.cmd;*.lnk|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;

                string current = _target.Text.Trim();
                try
                {
                    if (current.Length > 0)
                    {
                        string dir = Path.GetDirectoryName(current);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            dialog.InitialDirectory = dir;
                    }
                }
                catch
                {
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _target.Text = dialog.FileName;
                    _target.SelectionStart = _target.Text.Length;
                    _status.ForeColor = Muted;
                    _status.Text = "已选择：" + dialog.FileName;
                }
            }
        }

        private void TargetDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void TargetDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            _target.Text = files[0];
            _status.ForeColor = Muted;
            _status.Text = "已选择：" + files[0];
        }

        private void StartClick(object sender, EventArgs e)
        {
            StartCountdown();
        }

        private void StartCountdown()
        {
            if (_running) return;

            double seconds;
            if (!TryGetDelaySeconds(out seconds)) return;

            string target = _target.Text.Trim();
            if (!ValidateTarget(target)) return;

            _target.Select(0, 0);
            _arguments.Select(0, 0);

            SettingsStore.Save(_delay.Value, _unit.SelectedIndex, target,
                _arguments.Text, _autoExit.Checked, _autoStartLong.Checked);

            _running = true;
            _totalMilliseconds = (int)Math.Round(seconds * 1000.0);
            if (_totalMilliseconds < 0) _totalMilliseconds = 0;
            _deadline = DateTime.UtcNow.AddMilliseconds(_totalMilliseconds);

            SetInputsEnabled(false);
            SetStartEnabled(false);
            _cancel.Enabled = true;
            _cancel.Focus();
            _progress.Visible = true;
            _progress.Value = 0;
            _status.ForeColor = Accent;
            _status.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point);

            _countdownTimer.Start();
            UpdateCountdown();
        }

        private bool TryGetDelaySeconds(out double seconds)
        {
            seconds = (double)_delay.Value * UnitMultiplier(_unit.SelectedIndex);
            if (seconds > 86400)
            {
                MessageBox.Show(this, "延迟时间不能超过 24 小时。", "时间过长",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private bool ValidateTarget(string target)
        {
            return ValidateTarget(target, true);
        }

        private bool ValidateTarget(string target, bool interactive)
        {
            if (target.Length == 0)
            {
                if (interactive)
                {
                    MessageBox.Show(this, "请先选择或输入要启动的程序。", "缺少目标程序",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _target.Focus();
                }
                return false;
            }

            string expanded = target;
            try
            {
                expanded = Environment.ExpandEnvironmentVariables(target);
                if (expanded.Length >= 2 && expanded[0] == '"' && expanded[expanded.Length - 1] == '"')
                    expanded = expanded.Substring(1, expanded.Length - 2);
            }
            catch
            {
                expanded = target;
            }

            bool hasScheme = expanded.IndexOf("://", StringComparison.Ordinal) > 0;
            bool looksLikePath = expanded.IndexOf('\\') >= 0 || expanded.IndexOf('/') >= 0;

            if (!hasScheme && looksLikePath && !File.Exists(expanded) && !Directory.Exists(expanded))
            {
                if (!interactive)
                {
                    _status.ForeColor = Danger;
                    _status.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                    _status.Text = "找不到目标程序，未自动开始：" + expanded;
                    return false;
                }

                DialogResult result = MessageBox.Show(this,
                    "找不到这个文件或文件夹：\r\n\r\n" + expanded + "\r\n\r\n仍然尝试启动吗？",
                    "目标不存在", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                return result == DialogResult.Yes;
            }

            return true;
        }

        private void CountdownTick(object sender, EventArgs e)
        {
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            double remaining = (_deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0)
            {
                _countdownTimer.Stop();
                _progress.Value = 1000;
                LaunchNow();
                return;
            }

            int secondsLeft = (int)Math.Ceiling(remaining / 1000.0);
            _status.Text = "将在 " + FormatDuration(secondsLeft) + " 后启动";

            int progress = 1000;
            if (_totalMilliseconds > 0)
                progress = (int)Math.Round((1.0 - remaining / _totalMilliseconds) * 1000.0);
            if (progress < 0) progress = 0;
            if (progress > 1000) progress = 1000;
            _progress.Value = progress;
        }

        private static string FormatDuration(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int hours = seconds / 3600;
            int minutes = (seconds % 3600) / 60;
            int secs = seconds % 60;

            if (hours > 0)
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, secs);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, secs);
        }

        private void LaunchNow()
        {
            string target = _target.Text.Trim();
            string arguments = _arguments.Text;

            try
            {
                Launcher.Launch(target, arguments);
                _running = false;
                _progress.Value = 1000;
                _status.ForeColor = Success;
                _status.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
                _status.Text = "已成功启动：" + ShortName(target);

                if (_autoExit.Checked)
                {
                    _status.Text = "已成功启动，本程序即将退出…";
                    _closeTimer.Start();
                }
                else
                {
                    ResetAfterLaunch();
                }
            }
            catch (Exception ex)
            {
                _running = false;
                _countdownTimer.Stop();
                _progress.Visible = false;
                SetInputsEnabled(true);
                SetStartEnabled(true);
                _cancel.Enabled = false;
                _status.ForeColor = Danger;
                _status.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                _status.Text = "启动失败：" + ex.Message;

                MessageBox.Show(this, "无法启动目标程序：\r\n\r\n" + ex.Message,
                    "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetAfterLaunch()
        {
            SetInputsEnabled(true);
            SetStartEnabled(true);
            _start.Text = "再次开始";
            _cancel.Enabled = false;
            _progress.Visible = false;
            _status.ForeColor = Success;
            _status.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _status.Text = "已启动。可以修改设置后再次开始。";
        }

        private static string ShortName(string target)
        {
            try
            {
                if (target.IndexOf("://", StringComparison.Ordinal) > 0)
                    return target;
                string name = Path.GetFileName(target.TrimEnd('\\', '/'));
                return string.IsNullOrEmpty(name) ? target : name;
            }
            catch
            {
                return target;
            }
        }

        private void CancelClick(object sender, EventArgs e)
        {
            if (!_running) return;

            _running = false;
            _countdownTimer.Stop();
            _progress.Visible = false;
            SetInputsEnabled(true);
            SetStartEnabled(true);
            _cancel.Enabled = false;
            _status.ForeColor = Muted;
            _status.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _status.Text = "已取消倒计时。";
        }

        private void SetInputsEnabled(bool enabled)
        {
            _delay.Enabled = enabled;
            _unit.Enabled = enabled;
            _target.ReadOnly = !enabled;
            _target.TabStop = enabled;
            _arguments.ReadOnly = !enabled;
            _arguments.TabStop = enabled;
            _browse.Enabled = enabled;
            _autoExit.Enabled = enabled;
            _autoStartLong.Enabled = enabled;
        }

        private void SetStartEnabled(bool enabled)
        {
            _start.Enabled = enabled;
            _start.BackColor = enabled ? Accent : Color.FromArgb(229, 231, 235);
            _start.ForeColor = enabled ? Color.White : Color.FromArgb(156, 163, 175);
        }

        private void CloseTimerTick(object sender, EventArgs e)
        {
            _closeTimer.Stop();
            Close();
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_running)
            {
                DialogResult result = MessageBox.Show(this,
                    "倒计时还没有结束，确定要退出吗？",
                    "退出延时启动器", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }

                _running = false;
                _countdownTimer.Stop();
            }

            SettingsStore.Save(_delay.Value, _unit.SelectedIndex,
                _target.Text.Trim(), _arguments.Text, _autoExit.Checked, _autoStartLong.Checked);
        }

        private static Image CreateClockImage(int size)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float scale = size / 48f;

                using (Pen ring = new Pen(Color.White, 3.4f * scale))
                {
                    ring.StartCap = LineCap.Round;
                    ring.EndCap = LineCap.Round;
                    graphics.DrawEllipse(ring,
                        6.2f * scale, 6.2f * scale, 35.6f * scale, 35.6f * scale);
                }

                using (Pen hands = new Pen(Color.White, 3.2f * scale))
                {
                    hands.StartCap = LineCap.Round;
                    hands.EndCap = LineCap.Round;
                    graphics.DrawLine(hands, 24f * scale, 24f * scale, 24f * scale, 13.2f * scale);
                    graphics.DrawLine(hands, 24f * scale, 24f * scale, 32.5f * scale, 29.2f * scale);
                }

                using (SolidBrush dot = new SolidBrush(Color.White))
                {
                    graphics.FillEllipse(dot, 21.8f * scale, 21.8f * scale, 4.4f * scale, 4.4f * scale);
                }
            }
            return bitmap;
        }

        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static void SetCueBanner(TextBox textBox, string text)
        {
            if (textBox.IsHandleCreated)
            {
                SendMessage(textBox.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
            }
            else
            {
                textBox.HandleCreated += delegate
                {
                    SendMessage(textBox.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
                };
            }
        }
    }
}
