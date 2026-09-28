using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CadWebView;

/// <summary>
/// 唯一的网页承载控件（Chromium 内核 / WebView2）：面板与无模式窗口共用同一个控件。
/// </summary>
public sealed class WebHostControl : UserControl
{
    private const string RuntimeDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    private readonly CadWebViewConfig _config;
    private readonly WebView2 _webView;
    private bool _disposed;
    private bool _fallbackTried;
    private bool _initialized;
    private string? _pendingUrl;

    public WebHostControl(CadWebViewConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _webView = new WebView2 { Dock = DockStyle.Fill };
        _webView.NavigationCompleted += OnNavigationCompleted;

        Dock = DockStyle.Fill;
        Controls.Add(_webView);

        Initialize(_config.StartUrl);
    }

    /// <summary>当前已加载/正在加载的地址。</summary>
    public string CurrentUrl => _webView.Source?.ToString() ?? string.Empty;

    /// <summary>打开在线 URL 或本地页；相对路径按插件所在目录（含 Web 子目录）解析。</summary>
    public void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            url = _config.StartUrl;
        }

        _fallbackTried = false;

        if (_initialized)
        {
            NavigateCore(url);
        }
        else
        {
            // 内核尚未就绪（首次初始化中，或 runtime 缺失），暂存待就绪后打开
            _pendingUrl = url;
        }
    }

    private async void Initialize(string url)
    {
        _pendingUrl = url;

        WebView2LoaderBootstrap.EnsureLoaded();

        try
        {
            // 必须显式指定用户数据目录：默认位置在宿主 exe 旁（Program Files），标准用户无写权限
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CadWebView",
                "WebView2");

            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(environment);
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            ShowRuntimeMissingPage(ex);
            return;
        }
        catch (Exception ex)
        {
            ShowMessagePage("浏览器内核初始化失败", ex.Message, null);
            return;
        }

        if (_disposed)
        {
            return;
        }

        _initialized = true;
        _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

        var pending = _pendingUrl ?? _config.StartUrl;
        _pendingUrl = null;
        NavigateCore(pending);
    }

    private void NavigateCore(string url)
    {
        var core = _webView.CoreWebView2;
        if (core == null)
        {
            _pendingUrl = url;
            return;
        }

        if (TryResolve(url, out var target))
        {
            core.Navigate(target);
            return;
        }

        ShowBuiltinErrorPage(url, null);
    }

    private static bool TryResolve(string url, out string target)
    {
        target = "about:blank";

        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            target = absolute.ToString();
            return true;
        }

        var baseDir = CadWebViewConfig.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(baseDir, url),
            Path.Combine(baseDir, "Web", url),
            // bundle 布局：资源在家族目录的上一级（Contents/Web），随 bundle 一起分发
            Path.GetFullPath(Path.Combine(baseDir, "..", "Web", url)),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                target = new Uri(candidate).ToString();
                return true;
            }
        }

        return false;
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            return;
        }

        HandleLoadFailure(_webView.Source?.ToString() ?? string.Empty, e.WebErrorStatus);
    }

    private void HandleLoadFailure(string url, CoreWebView2WebErrorStatus status)
    {
        if (!_fallbackTried)
        {
            _fallbackTried = true;

            var fallbackUrl = _config.FallbackUrl;
            if (!string.IsNullOrWhiteSpace(fallbackUrl) &&
                !string.Equals(fallbackUrl, url, StringComparison.OrdinalIgnoreCase))
            {
                NavigateCore(fallbackUrl);
                return;
            }
        }

        ShowBuiltinErrorPage(url, status);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // 不在 CAD 内再开窗口
        e.Handled = true;

        if (!_config.OpenNewWindowInSystemBrowser)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("CadWebView: 转交系统浏览器失败 -> " + ex.Message);
        }
    }

    private void ShowRuntimeMissingPage(Exception ex)
    {
        Debug.WriteLine("CadWebView: WebView2 运行时缺失 -> " + ex.Message);

        ShowMessagePage(
            "未安装 WebView2 运行时",
            "CadWebView 需要 Microsoft Edge WebView2 Runtime（Chromium 内核）才能显示网页。\r\n\r\n" +
            "Windows 10 1809+ / Windows 11 通常已随 Edge 预装。若提示缺失，请安装后重启 CAD。",
            RuntimeDownloadUrl);
    }

    private void ShowMessagePage(string title, string message, string? linkUrl)
    {
        _webView.Visible = false;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(
            new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Text = title + "\r\n\r\n" + message,
            },
            0,
            0);

        if (!string.IsNullOrEmpty(linkUrl))
        {
            var link = new LinkLabel
            {
                AutoSize = true,
                Text = "下载 WebView2 Runtime：" + linkUrl,
            };
            link.LinkClicked += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(linkUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("CadWebView: 打开下载页失败 -> " + ex.Message);
                }
            };

            panel.Controls.Add(link, 0, 1);
        }

        Controls.Add(panel);
        panel.BringToFront();
    }

    private void ShowBuiltinErrorPage(string? url, CoreWebView2WebErrorStatus? status)
    {
        var core = _webView.CoreWebView2;
        if (core == null)
        {
            return;
        }

        var html =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><title>CadWebView</title></head>" +
            "<body style=\"font-family:'Segoe UI','Microsoft YaHei';padding:16px\">" +
            "<h3>页面无法加载</h3><p>" + HtmlEscape(url) + "</p>" +
            (status.HasValue ? "<p>错误：" + status.Value + "</p>" : string.Empty) +
            "</body></html>";

        core.NavigateToString(html);
    }

    private static string HtmlEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value!
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    /// <summary>释放内部 WebView2，防止内核进程与句柄泄漏。</summary>
    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                try
                {
                    _webView.NavigationCompleted -= OnNavigationCompleted;

                    var core = _webView.CoreWebView2;
                    if (core != null)
                    {
                        core.NewWindowRequested -= OnNewWindowRequested;
                    }

                    _webView.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("CadWebView: 释放 WebView2 失败 -> " + ex.Message);
                }
            }
        }

        base.Dispose(disposing);
    }
}