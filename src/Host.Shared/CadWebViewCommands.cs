using System;

namespace CadWebView.Platform;

/// <summary>
/// 对外命令：CADWEBVIEW —— 打开可停靠面板或无模式窗口（单例）。
/// </summary>
internal static class CadWebViewCommands
{
    [AcRt.CommandMethod("CADWEBVIEW")]
    public static void CadWebView()
    {
        var openWindow = false;
        var url = (string?)null;

        var document = AcApp.Application.DocumentManager.MdiActiveDocument;
        if (document != null)
        {
            var editor = document.Editor;

            // 注意：PromptStringOptions 不支持关键字，调用 Keywords.Add 会在 GetString 时抛
            // InvalidOperationException("Keywords are not allowed.")。P/W/URL 一律按普通输入识别。
            var options = new AcEd.PromptStringOptions("\n打开方式 [面板(P)/窗口(W)] 或直接输入 URL <面板>")
            {
                AllowSpaces = false,
                DefaultValue = "P",
                UseDefaultValue = true,
            };

            var result = editor.GetString(options);
            if (result.Status != AcEd.PromptStatus.OK)
            {
                return;
            }

            var text = result.StringResult ?? string.Empty;

            if (string.Equals(text, "W", StringComparison.OrdinalIgnoreCase))
            {
                openWindow = true;
            }
            else if (string.Equals(text, "P", StringComparison.OrdinalIgnoreCase))
            {
                openWindow = false;
            }
            else if (text.Length > 0)
            {
                url = text;
            }
        }

        if (openWindow)
        {
            Api.ShowWindow(url);
        }
        else
        {
            Api.ShowPanel(url);
        }
    }
}