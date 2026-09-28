using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CadWebView;

/// <summary>
/// 插件配置模型。默认值与 <c>CadWebView.config.json</c> 同目录读取。
/// </summary>
public sealed class CadWebViewConfig
{
    /// <summary>配置文件（与 DLL 同目录）。</summary>
    public const string FileName = "CadWebView.config.json";

    /// <summary>默认打开的页面：在线 URL 或本地资源相对路径。</summary>
    public string StartUrl { get; set; } = "about:blank";

    /// <summary>面板形态取值：可停靠面板。</summary>
    public const string LaunchModePanel = "panel";

    /// <summary>面板形态取值：无模式窗口。</summary>
    public const string LaunchModeWindow = "window";

    /// <summary>
    /// 启动形态：<c>panel</c> = 可停靠面板，<c>window</c> = 无模式窗口（默认）。
    /// 命令内不再交互选择，一律由本项决定。
    /// </summary>
    public string LaunchMode { get; set; } = LaunchModeWindow;

    /// <summary>是否以无模式窗口形态启动。</summary>
    public bool OpenAsWindow =>
        string.Equals(LaunchMode, LaunchModeWindow, StringComparison.OrdinalIgnoreCase);

    /// <summary>是否允许把新窗口请求转交系统默认浏览器。</summary>
    public bool OpenNewWindowInSystemBrowser { get; set; } = true;

    /// <summary>面板默认宽度。</summary>
    public int PanelWidth { get; set; } = 1920;

    /// <summary>面板默认高度。</summary>
    public int PanelHeight { get; set; } = 1080;

    /// <summary>加载失败时显示的降级页；为空则显示内置错误页。</summary>
    public string FallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// 插件自身所在目录，用于定位配置与本地资源。
    /// AppBundle 自动加载时 <see cref="AppDomain.BaseDirectory"/> 是 acad.exe 目录，
    /// 因此必须以程序集位置为准（bundle 下为 Contents/&lt;家族&gt;/）。
    /// </summary>
    public static string BaseDirectory
    {
        get
        {
            try
            {
                var location = typeof(CadWebViewConfig).Assembly.Location;
                if (!string.IsNullOrEmpty(location))
                {
                    var dir = Path.GetDirectoryName(location);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        return dir!;
                    }
                }
            }
            catch (Exception)
            {
                // 单文件/动态加载等场景下 Location 可能不可用，退回进程基目录
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    /// <summary>配置文件完整路径。</summary>
    public static string FilePath => Path.Combine(BaseDirectory, FileName);

    /// <summary>
    /// 从 <c>CadWebView.config.json</c> 读取；文件缺失或解析失败时返回默认值（不抛出）。
    /// </summary>
    public static CadWebViewConfig Load()
    {
        var config = new CadWebViewConfig();

        try
        {
            var path = FilePath;
            if (!File.Exists(path))
            {
                return config;
            }

            var json = StripComments(File.ReadAllText(path, Encoding.UTF8));

            var startUrl = ReadString(json, "startUrl");
            if (!string.IsNullOrEmpty(startUrl))
            {
                config.StartUrl = startUrl!;
            }

            var launchMode = ReadString(json, "launchMode");
            if (!string.IsNullOrEmpty(launchMode))
            {
                config.LaunchMode = launchMode!;
            }

            var fallbackUrl = ReadString(json, "fallbackUrl");
            if (!string.IsNullOrEmpty(fallbackUrl))
            {
                config.FallbackUrl = fallbackUrl!;
            }

            var newWindow = ReadBool(json, "openNewWindowInSystemBrowser");
            if (newWindow.HasValue)
            {
                config.OpenNewWindowInSystemBrowser = newWindow.Value;
            }

            var width = ReadInt(json, "panelWidth");
            if (width.HasValue && width.Value > 0)
            {
                config.PanelWidth = width.Value;
            }

            var height = ReadInt(json, "panelHeight");
            if (height.HasValue && height.Value > 0)
            {
                config.PanelHeight = height.Value;
            }
        }
        catch (Exception ex)
        {
            // 配置不可用时退回默认值，不阻断插件加载
            System.Diagnostics.Debug.WriteLine("CadWebView: 读取配置失败，使用默认值 -> " + ex.Message);
        }

        return config;
    }

    /// <summary>
    /// 剥离 <c>//</c> 行注释与 <c>/* */</c> 块注释，使注释内容不再参与键值匹配。
    /// 下面的解析基于正则全文首个匹配，若不剥离，注释里的 <c>"键": 值</c> 会被当成真实配置命中。
    /// 字符串字面量内部的 <c>//</c>（如 URL 的 <c>https://</c>）不受影响。
    /// </summary>
    private static string StripComments(string json)
    {
        var sb = new StringBuilder(json.Length);
        var inString = false;
        var escaped = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (inString)
            {
                sb.Append(c);

                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                sb.Append(c);
                continue;
            }

            if (c == '/' && i + 1 < json.Length)
            {
                if (json[i + 1] == '/')
                {
                    // 行注释：丢弃到行尾（换行符留给循环处理）
                    while (i < json.Length && json[i] != '\n')
                    {
                        i++;
                    }

                    continue;
                }

                if (json[i + 1] == '*')
                {
                    // 块注释：丢弃到 */
                    i += 2;
                    while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/'))
                    {
                        i++;
                    }

                    i++;
                    continue;
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    // 该配置为扁平结构，用最小解析避免为 net45 引入第三方 JSON 依赖。
    private static string? ReadString(string json, string key)
    {
        var match = Regex.Match(
            json,
            "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Value;
        return value.IndexOf('\\') >= 0 ? Regex.Unescape(value) : value;
    }

    private static int? ReadInt(string json, string key)
    {
        var match = Regex.Match(
            json,
            "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+)",
            RegexOptions.IgnoreCase);

        if (match.Success &&
            int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static bool? ReadBool(string json, string key)
    {
        var match = Regex.Match(
            json,
            "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        return string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
    }
}