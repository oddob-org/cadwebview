namespace CadWebView.Platform;

/// <summary>
/// 平台入口：注册平台适配实现；卸载时关闭容器。
/// </summary>
internal sealed class PlatformExtensionApplication : AcRt.IExtensionApplication
{
    public void Initialize()
    {
        Api.RegisterHost(new PlatformHost());
    }

    public void Terminate()
    {
        Api.Close();
    }
}