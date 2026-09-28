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

    /// <summary>是否允许把新窗口请求转交系统默认浏览器。</summary>
    public bool OpenNewWindowInSystemBrowser { get; set; } = true;

    /// <summary>面板默认宽度。</summary>
    public int PanelWidth { get; set; } = 400;

    /// <summary>面板默认高度。</summary>
    public int PanelHeight { get; set; } = 600;

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

            var json = File.ReadAllText(path, Encoding.UTF8);

            var startUrl = ReadString(json, "startUrl");
            if (!string.IsNullOrEmpty(startUrl))
            {
                config.StartUrl = startUrl!;
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