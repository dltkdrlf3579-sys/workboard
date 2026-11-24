using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

internal sealed class LoginShortcut
{
    readonly string link, initialized, executable;
    public LoginShortcut(string startupFolder, string settingsFolder, string executablePath)
    {
        link = Path.Combine(startupFolder, "WorkBoard - 업무 목록.lnk");
        initialized = Path.Combine(settingsFolder, "login-configured");
        executable = executablePath;
    }

    public bool Enabled { get { return File.Exists(link); } }

    public void Initialize()
    {
        if (Enabled || !File.Exists(initialized)) SetEnabled(true);
    }

    public void SetEnabled(bool enabled)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(initialized));
        if (enabled) WriteLink();
        if (!File.Exists(initialized))
            using (File.Open(initialized, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read)) { }
        if (!enabled) File.Delete(link);
    }

    void WriteLink()
    {
        var type = Type.GetTypeFromProgID("WScript.Shell", true);
        dynamic shell = Activator.CreateInstance(type);
        try
        {
            dynamic shortcut = shell.CreateShortcut(link);
            try
            {
                if (Enabled && String.Equals((string)shortcut.TargetPath, executable, StringComparison.OrdinalIgnoreCase)
                    && (string)shortcut.Arguments == "--tray") return;
                shortcut.TargetPath = executable;
                shortcut.Arguments = "--tray";
                shortcut.WorkingDirectory = Path.GetDirectoryName(executable);
                shortcut.Description = "로그인할 때 업무 목록을 트레이에서 시작합니다.";
                shortcut.Save();
            }
            finally { Marshal.FinalReleaseComObject((object)shortcut); }
        }
        finally { Marshal.FinalReleaseComObject((object)shell); }
    }
}

internal sealed class TraySession : IDisposable
{
    readonly Board board;
    readonly LoginShortcut startup;
    readonly Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
    readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
    readonly Drawing.Icon icon;
    WindowState restoreState = WindowState.Normal;

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    static Drawing.Icon ListIcon()
    {
        using (var bitmap = new Drawing.Bitmap(32,32))
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        using (var pen = new Drawing.Pen(Drawing.Color.White, 4.0f))
        using (var rim = new Drawing.Pen(Drawing.Color.FromArgb(218,244,227), 1.5f))
        using (var fill = new Drawing.SolidBrush(Drawing.Color.FromArgb(19,112,70)))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var disc = new Drawing.RectangleF(1.5f,1.5f,29,29);
            graphics.FillEllipse(fill, disc);
            graphics.DrawEllipse(rim, disc);
            pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
            graphics.DrawLines(pen, new[]{new Drawing.Point(8,16),new Drawing.Point(14,21),new Drawing.Point(24,10)});
            var handle = bitmap.GetHicon();
            try { using (var borrowed = Drawing.Icon.FromHandle(handle)) return (Drawing.Icon)borrowed.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }

    public TraySession(Board board, LoginShortcut startup)
    {
        this.board = board;
        this.startup = startup;
        icon = ListIcon();
        tray.Icon = icon;
        menu.Items.Add("업무 목록 열기", null, (s,e) => Open());
        var login = new Forms.ToolStripMenuItem("로그인할 때 트레이에서 시작");
        menu.Items.Add(login);
        login.Click += (s,e) => {
            try { startup.SetEnabled(!startup.Enabled); }
            catch (Exception ex) { MessageBox.Show("자동 시작 설정을 변경할 수 없습니다.\n"+ex.Message, "업무 목록"); }
        };
        menu.Opening += (s,e) => login.Checked = startup.Enabled;
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (s,e) => {
            if (!board.IsEnabled || board.OwnedWindows.Cast<Window>().Any(w => w.IsVisible)) Open();
            else board.Close();
        });
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (s,e) => { if (e.Button == Forms.MouseButtons.Left) Open(); };
        board.StateChanged += OnStateChanged;
        board.JobsChanged += UpdateCount;
        UpdateCount(null,EventArgs.Empty);
        tray.Visible = true;
    }

    void UpdateCount(object sender, EventArgs e)
    {
        tray.Text = "업무 목록 · 미완료 " + board.UnfinishedCount + "건";
    }

    void OnStateChanged(object sender, EventArgs e)
    {
        if (board.WindowState == WindowState.Minimized) board.Hide();
        else restoreState = board.WindowState;
    }

    public void Open()
    {
        if (board.WindowState == WindowState.Minimized) board.WindowState = restoreState;
        if (!board.IsVisible) board.Show();
        Window target = board;
        Window child;
        while ((child = target.OwnedWindows.Cast<Window>().LastOrDefault(w => w.IsVisible)) != null)
            target = child;
        if (target.WindowState == WindowState.Minimized) target.WindowState = WindowState.Normal;
        target.Activate();
    }

    public void Dispose()
    {
        board.StateChanged -= OnStateChanged;
        board.JobsChanged -= UpdateCount;
        tray.Visible = false;
        tray.Dispose(); menu.Dispose(); icon.Dispose();
    }
}

internal static class WorkBoardApp
{
    [STAThread]
    public static void Main(string[] args)
    {
        bool background = args.Contains("--tray", StringComparer.OrdinalIgnoreCase);
        bool first;
        using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PersonalWorkBoard_Open_4827"))
        using (var gate = new Mutex(true, @"Local\PersonalWorkBoard_4827", out first))
        {
            if (!first) { if (!background) show.Set(); return; }
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                var board = new Board();
                app.MainWindow = board;
                var startup = new LoginShortcut(Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PersonalWorkBoard"),
                    Assembly.GetExecutingAssembly().Location);
                using (var session = new TraySession(board,startup))
                {
                    var listener = ThreadPool.RegisterWaitForSingleObject(show, (s,timedOut) => {
                        if (!app.Dispatcher.HasShutdownStarted)
                            app.Dispatcher.BeginInvoke(new Action(session.Open));
                    }, null, Timeout.Infinite, false);
                    try
                    {
                        if (!background)
                        {
                            session.Open();
                            try { startup.Initialize(); }
                            catch (Exception ex) { MessageBox.Show(board,"자동 시작을 설정할 수 없습니다.\n"+ex.Message,"업무 목록"); }
                        }
                        app.Run();
                    }
                    finally { listener.Unregister(null); }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.GetBaseException().Message,"업무 목록"); }
        }
    }
}
