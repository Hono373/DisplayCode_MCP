using MineMCPLib.Extensions;
using ModelContextProtocol.Protocol;

namespace MineMCPLib.Tools;

public class GeneralTools
{
    public static ContentBlock Time()
    {
        return $"当前服务器时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}".Block();
    }
}
