using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace CadWebView.Platform;

/// <summary>
/// 平台适配实现：把同一个 <see cref="WebHostControl"/> 挂成可停靠面板或无模式窗口。
/// 面板与窗口各自单例，可同时打开、互不干扰。
/// </summary>
internal sealed class PlatformHost : IPlatformHost
{
    private static readonly Guid PaletteId = new Guid("7E2C4F1A-9B3D-4E5F-8A6C-1D2E3F4A5B6C");

    // PaletteSet 必须用静态字段持有，防止被 GC 回收导致面板消失
    private static AcWin.PaletteSet? _palette;
    private static WebHostControl? _panelControl;
    private static Form? _windowForm;

    public void ShowPanel(CadWebViewConfig config)
    {
        if (_palette == null)
        {
            _palette = new AcWin.PaletteSet("CadWebView", PaletteId);
        }

        if (_panelControl == null)
        {
            _panelControl = new WebHostControl(config);
            // PaletteSet.Add 是 WinForms 重载（AddVisual 仅接受 WPF Visual）
            _palette.Add("CadWebView", _panelControl);
            _palette.Size = new Size(config.PanelWidth, config.PanelHeight);
        }
        else
        {
            _panelControl.Navigate(config.StartUrl);
        }

        _palette.Visible = true;
    }

    public void ShowWindow(CadWebViewConfig config)
    {
        if (_windowForm != null && !_windowForm.IsDisposed)
        {
            _windowForm.Activate();

            if (_windowForm.Controls.Count > 0 && _windowForm.Controls[0] is WebHostControl existing)
            {
                existing.Navigate(config.StartUrl);
            }

            return;
        }

        var form = new Form
        {
            Text = "CadWebView",
            StartPosition = FormStartPosition.CenterScreen,
            Width = config.PanelWidth,
            Height = config.PanelHeight,
            ShowInTaskbar = false,
            MinimizeBox = true,
            MaximizeBox = true,
            FormBorderStyle = FormBorderStyle.Sizable,
        };

        form.Controls.Add(new WebHostControl(config) { Dock = DockStyle.Fill });
        form.FormClosed += OnWindowClosed;
        _windowForm = form;

        // 必须用 CAD 自身的显示 API，保证与主窗口正确关联
        AcApp.Application.ShowModelessDialog(form);
    }

    public void CloseAll()
    {
        try
        {
            if (_palette != null)
            {
                _palette.Visible = false;
                _palette = null;
            }

            _panelControl?.Dispose();
            _panelControl = null;

            if (_windowForm != null && !_windowForm.IsDisposed)
            {
                _windowForm.Close();
            }

            _windowForm = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("CadWebView: 关闭容器失败 -> " + ex.Message);
        }
    }

    private static void OnWindowClosed(object? sender, FormClosedEventArgs e)
    {
        _windowForm = null;
    }
}