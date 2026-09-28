using System;

namespace CadWebView.Platform;

/// <summary>
/// 对外入口：CADWEBVIEW 命令 + CADWEBVIEW-OPEN 原生 LISP 函数。
/// 形态（面板 / 窗口）由配置决定，容器均为单例。
/// </summary>
internal static class CadWebViewCommands
{
    [AcRt.CommandMethod("CADWEBVIEW")]
    public static void CadWebView()
    {
        var url = (string?)null;

        var document = AcApp.Application.DocumentManager.MdiActiveDocument;
        if (document != null)
        {
            var editor = document.Editor;

            // 形态（面板 / 窗口）由 CadWebView.config.json 的 launchMode 决定，命令内不再选择。
            // 注意：PromptStringOptions 不支持关键字，调用 Keywords.Add 会在 GetString 时抛
            // InvalidOperationException("Keywords are not allowed.")。此处的输入只当作 URL / 本地相对路径。
            var options = new AcEd.PromptStringOptions("\n输入要打开的 URL <回车使用配置默认页>")
            {
                AllowSpaces = false,
            };

            var result = editor.GetString(options);
            if (result.Status != AcEd.PromptStatus.OK)
            {
                return;
            }

            var text = result.StringResult ?? string.Empty;
            if (text.Length > 0)
            {
                url = text;
            }
        }

        Open(url);
    }

    /// <summary>
    /// 原生 LISP 入口：<c>(cadwebview-open "https://...")</c>；参数省略或为 nil 时使用配置中的
    /// <c>startUrl</c>。形态仍由 <c>CadWebView.config.json</c> 的 <c>launchMode</c> 决定。返回 1 表示已受理。
    /// </summary>
    [AcRt.LispFunction("CADWEBVIEW-OPEN")]
    public static AcDb.ResultBuffer OpenFromLisp(AcDb.ResultBuffer? args)
    {
        var url = (string?)null;

        if (args != null)
        {
            var values = args.AsArray();
            if (values.Length > 0 && values[0].TypeCode == (int)AcRt.LispDataType.Text)
            {
                url = values[0].Value as string;
            }
        }

        Open(url);

        return new AcDb.ResultBuffer(new AcDb.TypedValue((int)AcRt.LispDataType.Int16, 1));
    }

    /// <summary>按配置的 launchMode 打开面板或窗口；url 为空则用配置中的 StartUrl。</summary>
    private static void Open(string? url)
    {
        var config = CadWebViewConfig.Load();
        if (config.OpenAsWindow)
        {
            Api.ShowWindow(url);
        }
        else
        {
            Api.ShowPanel(url);
        }
    }
}