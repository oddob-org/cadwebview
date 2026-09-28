using System;

namespace CadWebView;

/// <summary>
/// 对外公共 API（供宿主/壳程序调用）。
/// </summary>
public static class Api
{
    private static IPlatformHost? _host;

    /// <summary>
    /// 由各平台 Host 的 <c>IExtensionApplication.Initialize</c> 调用，注入平台适配实现。
    /// </summary>
    public static void RegisterHost(IPlatformHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>打开可停靠面板（单例）。url 为空时使用配置中的 StartUrl。</summary>
    public static void ShowPanel(string? url = null)
    {
        Resolve().ShowPanel(BuildConfig(url));
    }

    /// <summary>打开无模式窗口（单例）。url 为空时使用配置中的 StartUrl。</summary>
    public static void ShowWindow(string? url = null)
    {
        Resolve().ShowWindow(BuildConfig(url));
    }

    /// <summary>关闭面板与窗口。</summary>
    public static void Close()
    {
        _host?.CloseAll();
    }

    private static IPlatformHost Resolve()
    {
        return _host ?? throw new InvalidOperationException(
            "CadWebView 平台适配层尚未注册：请确认已加载对应平台的 CadWebView Host DLL。");
    }

    private static CadWebViewConfig BuildConfig(string? url)
    {
        var config = CadWebViewConfig.Load();

        if (!string.IsNullOrWhiteSpace(url))
        {
            config.StartUrl = url!;
        }

        return config;
    }
}