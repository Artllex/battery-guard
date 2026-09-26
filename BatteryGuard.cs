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

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            if (ShouldAlert(60, true, true) || !ShouldAlert(61, true, true) ||
                !ShouldAlert(80, true, true) || ShouldAlert(255, true, true) ||
                ShouldAlert(80, false, true) || ShouldAlert(80, true, false)) return 1;
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test.txt"), "PASS: 60, 61, 80, unknown, no battery, API failure");
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
            using (Guard context = new Guard()) Application.Run(context);
        }
        return 0;
    }
}

internal sealed class Guard : ApplicationContext
{
    private readonly NotifyIcon icon;
    private readonly System.Windows.Forms.Timer timer;
    private DateTime lastAlert = DateTime.MinValue;
    private readonly ToolStripMenuItem startup;
    private string StartupFile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "BatteryGuard60.vbs"); } }

    internal Guard()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Sprawdź teraz", null, delegate { Check(true); });
        startup = new ToolStripMenuItem("Uruchamiaj po zalogowaniu");
        startup.Checked = File.Exists(StartupFile);
        startup.Click += delegate { ToggleStartup(); };
        menu.Items.Add(startup);
        menu.Items.Add("Zakończ", null, delegate { ExitThread(); });
        using (Stream resource = typeof(Guard).Assembly.GetManifestResourceStream("BatteryGuard.ico"))
        using (Icon artwork = new Icon(resource, new Size(32, 32)))
            icon = new NotifyIcon { Icon = (Icon)artwork.Clone(), Text = "BatteryGuard — limit 60%", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += delegate { Check(true); };
        timer = new System.Windows.Forms.Timer { Interval = 15000 };
        timer.Tick += delegate { Check(false); };
        timer.Start();
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
            if (manual) Show("BatteryGuard", "Nie udało się odczytać poziomu baterii.", ToolTipIcon.Info);
            return;
        }
        int percent = power.BatteryLifePercent;
        icon.Text = "BatteryGuard — bateria " + percent + "% / limit 60%";
        if (Program.ShouldAlert(percent, battery, known))
        {
            if (manual || DateTime.UtcNow - lastAlert >= TimeSpan.FromMinutes(10))
            {
                Show("Bateria przekroczyła 60%", "Poziom baterii: " + percent + "%. Sprawdź limit ładowania w G-Helper i ASUS lub odłącz zasilacz.", ToolTipIcon.Warning);
                lastAlert = DateTime.UtcNow;
            }
        }
        else
        {
            lastAlert = DateTime.MinValue;
            if (manual) Show("Poziom baterii", "Bateria: " + percent + "%. Limit 60% nie został przekroczony.", ToolTipIcon.Info);
        }
    }

    private void Show(string title, string text, ToolTipIcon kind)
    {
        icon.ShowBalloonTip(10000, title, text, kind);
        if (kind == ToolTipIcon.Warning) System.Media.SystemSounds.Exclamation.Play();
    }

    private void ToggleStartup()
    {
        try
        {
            if (File.Exists(StartupFile)) File.Delete(StartupFile);
            else
            {
                string exe = Application.ExecutablePath;
                File.WriteAllText(StartupFile, "CreateObject(\"WScript.Shell\").Run " +
                    "Chr(34) & \"" + exe.Replace("\"", "\"\"") + "\" & Chr(34), 0, False\r\n");
            }
            startup.Checked = File.Exists(StartupFile);
        }
        catch (Exception ex) { MessageBox.Show("Nie udało się zmienić autostartu: " + ex.Message, "BatteryGuard"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Stop(); timer.Dispose(); icon.Visible = false; icon.ContextMenuStrip.Dispose(); icon.Icon.Dispose(); icon.Dispose(); }
        base.Dispose(disposing);
    }
}
