using ModelContextProtocol.Protocol;

namespace MineMCPLib.Extensions;

public static class ContentBlockExtensions
{
    /// <summary>
    /// 如果contentBlock不为空，则添加到list中，返回true
    /// </summary>
    /// <param name="list"></param>
    /// <param name="contentBlock"></param>
    /// <returns></returns>
    public static bool TryAdd(this List<ContentBlock> list, ContentBlock? contentBlock)
    {
        if (contentBlock is not null)
        {
            list.Add(contentBlock);
            return true;
        }
        return false;
    }
    public static List<ContentBlock> Add(this List<ContentBlock> list, string contentBlock)
    {
        list.Add(contentBlock.Block());
        return list;
    }
}
