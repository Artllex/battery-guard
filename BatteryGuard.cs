using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PowerStatus
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSystemPowerStatus(out PowerStatus status);

    internal static bool ShouldAlert(int percent, bool hasBattery, bool valid)
    {
        return valid && hasBattery && percent > 60 && percent <= 100;
    }

    internal static int IconState(int percent)
    {
        return percent < 20 ? 0 : (percent > 60 ? 2 : 1);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            if (IconState(0) != 0 || IconState(19) != 0 || IconState(20) != 1 ||
                IconState(60) != 1 || IconState(61) != 2 || IconState(100) != 2) return 1;
            foreach (string name in new[] { "BatteryGuard.ico", "BatteryGuard-normal.ico", "BatteryGuard-high.ico" })
                using (Stream resource = typeof(Guard).Assembly.GetManifestResourceStream(name))
                using (Icon artwork = new Icon(resource, new Size(32, 32)))
                    if (artwork.Width != 32 || artwork.Height != 32) return 1;
            if (ShouldAlert(60, true, true) || !ShouldAlert(61, true, true) ||
                !ShouldAlert(80, true, true) || ShouldAlert(255, true, true) ||
                ShouldAlert(80, false, true) || ShouldAlert(80, true, false)) return 1;
            AlertPolicy policy = new AlertPolicy();
            DateTime now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            if (policy.Observe(60, false, now) || !policy.Observe(61, false, now) ||
                policy.Observe(61, false, now.AddSeconds(15)) ||
                !policy.Observe(62, false, now.AddSeconds(30)) ||
                policy.Observe(61, false, now.AddSeconds(45)) ||
                !policy.Observe(62, false, now.AddMinutes(1)) ||
                policy.Observe(62, false, now.AddMinutes(10)) ||
                !policy.Observe(62, false, now.AddMinutes(11)) ||
                !policy.Observe(62, true, now.AddMinutes(12)) ||
                policy.Observe(60, false, now.AddMinutes(13)) ||
                !policy.Observe(61, false, now.AddMinutes(14)) ||
                !policy.Observe(65, false, now.AddMinutes(15)) ||
                !new AlertPolicy().Observe(80, false, now)) return 1;
            AlertPolicy cooling = new AlertPolicy();
            if (!cooling.Observe(80, false, now) ||
                cooling.Observe(79, false, now.AddMinutes(11)) ||
                cooling.Observe(79, false, now.AddMinutes(22)) ||
                cooling.Observe(78, false, now.AddMinutes(33)) ||
                !cooling.Observe(79, false, now.AddMinutes(34)) ||
                !cooling.Observe(79, false, now.AddMinutes(44))) return 1;
            AlertPolicy low = new AlertPolicy();
            if (low.Observe(10, false, now) || low.Observe(11, false, now.AddMinutes(11)) ||
                low.Observe(9, false, now.AddMinutes(22))) return 1;
            return 0;
        }
        if (args.Length > 0 && args[0] == "--status")
        {
            PowerStatus power;
            bool ok = GetSystemPowerStatus(out power);
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "battery-status.txt"),
                "API success: " + ok + "\r\nBattery percent: " + power.BatteryLifePercent + "\r\nBattery flags: " + power.BatteryFlag + "\r\nAC: " + power.ACLineStatus);
            return ok ? 0 : 1;
        }
        bool created;
        using (Mutex mutex = new Mutex(true, "Local\\BatteryGuard60", out created))
        {
            if (!created) return 0;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (EventWaitHandle shutdown = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\BatteryGuard60Shutdown"))
            using (Guard context = new Guard(shutdown)) Application.Run(context);
        }
        return 0;
    }
}

internal sealed class AlertPolicy
{
    private int? previousPercent;
    private DateTime lastAlert = DateTime.MinValue;
    private bool recovering;

    internal bool Observe(int percent, bool manual, DateTime now)
    {
        if (percent < 0 || percent > 100) return false;
        bool rising = previousPercent.HasValue && percent > previousPercent.Value;
        bool falling = previousPercent.HasValue && percent < previousPercent.Value;
        if (falling) recovering = true;
        if (rising) recovering = false;
        previousPercent = percent;
        if (percent <= 60)
        {
            lastAlert = DateTime.MinValue;
            recovering = false;
            return false;
        }
        if (manual || rising || (!recovering && now - lastAlert >= TimeSpan.FromMinutes(10)))
        {
            lastAlert = now;
            return true;
        }
        return false;
    }
}

internal sealed class Guard : ApplicationContext
{
    private readonly NotifyIcon icon;
    private readonly Icon[] stateIcons = new Icon[3];
    private int currentIconState = -1;
    private readonly System.Windows.Forms.Timer timer;
    private readonly System.Windows.Forms.Timer shutdownTimer;
    private readonly AlertPolicy alerts = new AlertPolicy();
    private readonly ToolStripMenuItem startup;
    private string StartupFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "BatteryGuard60.vbs"); } }
    private string StartupShortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "BatteryGuard.lnk"); } }

    internal Guard(EventWaitHandle shutdown)
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Sprawdź teraz", null, delegate { Check(true); });
        startup = new ToolStripMenuItem("Uruchamiaj po zalogowaniu");
        startup.Checked = File.Exists(StartupFile) || File.Exists(StartupShortcut);
        startup.Click += delegate { ToggleStartup(); };
        menu.Items.Add(startup);
        menu.Items.Add("Zakończ", null, delegate { ExitThread(); });
        string[] names = { "BatteryGuard.ico", "BatteryGuard-normal.ico", "BatteryGuard-high.ico" };
        for (int i = 0; i < names.Length; i++)
            using (Stream resource = typeof(Guard).Assembly.GetManifestResourceStream(names[i]))
            using (Icon artwork = new Icon(resource, new Size(32, 32)))
                stateIcons[i] = (Icon)artwork.Clone();
        icon = new NotifyIcon { Icon = stateIcons[0], Text = "BatteryGuard — limit 60%", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += delegate { Check(true); };
        timer = new System.Windows.Forms.Timer { Interval = 15000 };
        timer.Tick += delegate { Check(false); };
        timer.Start();
        shutdownTimer = new System.Windows.Forms.Timer { Interval = 250 };
        shutdownTimer.Tick += delegate { if (shutdown.WaitOne(0)) ExitThread(); };
        shutdownTimer.Start();
        Check(false);
    }

    private void Check(bool manual)
    {
        Program.PowerStatus power;
        bool ok = Program.GetSystemPowerStatus(out power);
        bool battery = ok && power.BatteryFlag != 255 && (power.BatteryFlag & 128) == 0;
        bool known = ok && power.BatteryLifePercent <= 100;
        if (!battery || !known)
        {
            icon.Text = "BatteryGuard — brak odczytu baterii";
            if (manual) ShowManual("Nie udało się odczytać poziomu baterii.");
            return;
        }
        int percent = power.BatteryLifePercent;
        int nextIconState = Program.IconState(percent);
        if (currentIconState != nextIconState)
        {
            icon.Icon = stateIcons[nextIconState];
            currentIconState = nextIconState;
        }
        icon.Text = "BatteryGuard — bateria " + percent + "% / limit 60%";
        bool notify = alerts.Observe(percent, manual, DateTime.UtcNow);
        if (manual)
        {
            ShowManual("Poziom baterii: " + percent + "%." +
                (percent > 60 ? " Limit 60% został przekroczony. Sprawdź G-Helper i ASUS lub odłącz zasilacz."
                    : " Limit 60% nie został przekroczony."));
            return;
        }
        if (Program.ShouldAlert(percent, battery, known))
        {
            if (notify)
            {
                Show("Bateria przekroczyła 60%", "Poziom baterii: " + percent + "%. Sprawdź limit ładowania w G-Helper i ASUS lub odłącz zasilacz.", ToolTipIcon.Warning);
            }
        }
    }

    private void ShowManual(string text)
    {
        MessageBox.Show(text, "BatteryGuard — sprawdzenie baterii", MessageBoxButtons.OK,
            MessageBoxIcon.None, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
    }

    private void Show(string title, string text, ToolTipIcon kind)
    {
        icon.ShowBalloonTip(10000, title, text, kind);
    }

    private void ToggleStartup()
    {
        try
        {
            if (File.Exists(StartupFile) || File.Exists(StartupShortcut))
            {
                if (File.Exists(StartupFile)) File.Delete(StartupFile);
                if (File.Exists(StartupShortcut)) File.Delete(StartupShortcut);
            }
            else
            {
                string exe = Application.ExecutablePath;
                File.WriteAllText(StartupFile, "CreateObject(\"WScript.Shell\").Run " +
                    "Chr(34) & \"" + exe.Replace("\"", "\"\"") + "\" & Chr(34), 0, False\r\n");
            }
            startup.Checked = File.Exists(StartupFile) || File.Exists(StartupShortcut);
        }
        catch (Exception ex) { MessageBox.Show("Nie udało się zmienić autostartu: " + ex.Message, "BatteryGuard"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop(); timer.Dispose(); icon.Visible = false;
            shutdownTimer.Stop(); shutdownTimer.Dispose();
            icon.ContextMenuStrip.Dispose(); icon.Dispose();
            foreach (Icon artwork in stateIcons) artwork.Dispose();
        }
        base.Dispose(disposing);
    }
}
