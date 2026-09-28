using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CadWebView;

/// <summary>
/// WebView2 托管程序集通过 DllImport 加载 WebView2Loader.dll，而插件所在目录
/// 不在 CLR 默认的原生库探测路径内（宿主是 CAD，探测起点为 CAD 安装目录）。
/// 这里按绝对路径把随包分发的 loader 预先加载进进程，之后按文件名加载即可命中该模块。
/// </summary>
internal static class WebView2LoaderBootstrap
{
    private const string LoaderFileName = "WebView2Loader.dll";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string lpLibFileName);

    private static bool _attempted;

    /// <summary>首次创建浏览器控件前调用一次；失败不抛出，交由后续初始化报错。</summary>
    public static void EnsureLoaded()
    {
        if (_attempted)
        {
            return;
        }

        _attempted = true;

        var baseDir = CadWebViewConfig.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(baseDir, LoaderFileName),
            // 与 WebView2 包输出约定保持一致（runtimes\win-x64\native）
            Path.Combine(baseDir, "runtimes", "win-x64", "native", LoaderFileName),
        };

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                if (LoadLibraryW(candidate) != IntPtr.Zero)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CadWebView: 预加载 WebView2Loader 失败 -> " + ex.Message);
            }
        }

        Debug.WriteLine("CadWebView: 未找到可用的 " + LoaderFileName + "，若初始化失败请确认该文件随包分发");
    }
}