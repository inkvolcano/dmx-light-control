using System.Runtime.InteropServices;
using DmxLicht.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DmxLicht.App;

/// <summary>The app window: shows the web UI in WebView2 (built into Windows 10/11).</summary>
sealed class MainWindow : Form
{
    readonly string url;
    readonly WebView2 view = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(24, 24, 27) };

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public MainWindow(string url)
    {
        this.url = url;
        Text = "DmxLicht";
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        BackColor = Color.FromArgb(24, 24, 27);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        Size = new Size(1360, 860);
        Controls.Add(view);
        Load += async (_, _) => await InitAsync();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { } // dark title bar
    }

    async Task InitAsync()
    {
        try
        {
            string data = Path.Combine(Log.Dir, "WebView2");
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, data);
            await view.EnsureCoreWebView2Async(env);
            view.CoreWebView2.Settings.AreDevToolsEnabled = true;
            view.CoreWebView2.Settings.IsStatusBarEnabled = false;
            view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            view.CoreWebView2.DocumentTitleChanged += (_, _) => Text = view.CoreWebView2.DocumentTitle;
            // links to other sites open in the normal browser
            view.CoreWebView2.NewWindowRequested += (_, a) => { a.Handled = true; Program.OpenInBrowser(a.Uri); };
            view.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            // No WebView2 runtime: fall back to the default browser, keep running in the tray.
            Log.Write("WebView2 niet beschikbaar, open in browser: " + ex.Message);
            Program.OpenInBrowser(url);
            Hide();
            ShowInTaskbar = false;
            var tray = new TrayContext(url);
            tray.ThreadExit += (_, _) => Close();
        }
    }
}

/// <summary>Browser mode: a tray icon to reopen the page or quit.</summary>
sealed class TrayContext : ApplicationContext
{
    readonly NotifyIcon icon;

    public TrayContext(string url)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Openen / Open", null, (_, _) => Program.OpenInBrowser(url));
        menu.Items.Add("Afsluiten / Quit", null, (_, _) => ExitThread());
        icon = new NotifyIcon
        {
            Text = "DmxLicht",
            Icon = SafeIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        icon.DoubleClick += (_, _) => Program.OpenInBrowser(url);
    }

    static Icon SafeIcon()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
        catch { return SystemIcons.Application; }
    }

    protected override void ExitThreadCore()
    {
        icon.Visible = false;
        icon.Dispose();
        base.ExitThreadCore();
    }
}
