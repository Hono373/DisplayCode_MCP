namespace MineMCPLib;

/// <summary>
/// LLM 通信工具类
/// 封装 MEAILLMTalk 提供简化的 LLM 调用接口
/// </summary>
public static class LLMTalk
{
    /// <summary>
    /// 测试 LM Studio 连接
    /// </summary>
    public static async Task TestLink()
    {
        await MEAILLMTalk.TestLinkAsync();
    }

    /// <summary>
    /// 向 LM Studio 发送一张图片 + 提示词，并返回模型回复
    /// </summary>
    public static async Task<string> SendImageToLMStudioAsync(
      string base64,
      string type,
      string prompt,
      string model,
      string baseUrl = "http://localhost:1234/v1")
    {
        return await MEAILLMTalk.SendImageToLLMAsync(base64, type, prompt, model, baseUrl);
    }

    /// <summary>
    /// 向 LLM 发送纯文本消息
    /// </summary>
    public static async Task<string> SendTextToLMStudioAsync(
        string prompt,
        string model,
        string baseUrl = "http://localhost:1234/v1",
        string? systemPrompt = null)
    {
        return await MEAILLMTalk.SendTextToLLMAsync(prompt, model, baseUrl, systemPrompt);
    }

    /// <summary>
    /// 流式发送消息
    /// </summary>
    public static IAsyncEnumerable<string> StreamTextToLMStudioAsync(
        string prompt,
        string model,
        string baseUrl = "http://localhost:1234/v1",
        string? systemPrompt = null)
    {
        return MEAILLMTalk.StreamTextToLLMAsync(prompt, model, baseUrl, systemPrompt);
    }
}
