namespace CadWebView;

/// <summary>
/// 平台适配接口，由各平台 Host（AutoCAD / ZWCAD / GstarCAD）实现。
/// </summary>
public interface IPlatformHost
{
    /// <summary>挂成"可停靠面板"（单例）。</summary>
    void ShowPanel(CadWebViewConfig config);

    /// <summary>挂成"无模式窗口"（单例）。</summary>
    void ShowWindow(CadWebViewConfig config);

    /// <summary>关闭并释放两个容器。</summary>
    void CloseAll();
}