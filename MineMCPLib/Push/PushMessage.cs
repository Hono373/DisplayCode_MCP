using System.Text.Json.Serialization;

namespace MineMCPLib.Push;

/// <summary>
/// 推送消息类型枚举
/// </summary>
public enum PushMessageType
{
    /// <summary>工具执行结果</summary>
    ToolResult,

    /// <summary>流式文本片段</summary>
    StreamChunk,

    /// <summary>流式结束信号</summary>
    StreamEnd,

    /// <summary>错误信息</summary>
    Error,

    /// <summary>进度通知</summary>
    Progress,

    /// <summary>完成信号</summary>
    Complete
}

/// <summary>
/// Service → Client 推送的结构化消息
/// 用于传递 MCP 工具执行结果、LLM 流式输出等数据
/// 
/// 消息协议设计原则：
/// - Service 只推送引擎层原始数据
/// - Client 决定如何压缩/格式化后暴露给前端
/// </summary>
public class PushMessage
{
    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("type")]
    public PushMessageType Type { get; set; }

    /// <summary>
    /// 工具名称（当 Type 为 ToolResult 时）
    /// </summary>
    [JsonPropertyName("toolName")]
    public string? ToolName { get; set; }

    /// <summary>
    /// 消息内容（文本或 JSON 字符串）
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// 序列 ID，用于关联请求和响应
    /// </summary>
    [JsonPropertyName("sequenceId")]
    public string? SequenceId { get; set; }

    /// <summary>
    /// 附加元数据（用于传递额外信息如 token 用量、耗时等）
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }

    /// <summary>
    /// 是否为最后一条消息
    /// </summary>
    [JsonPropertyName("isFinal")]
    public bool IsFinal { get; set; }

    /// <summary>
    /// 创建工具结果消息
    /// </summary>
    public static PushMessage ToolResultMessage(string toolName, string content, string? sequenceId = null)
    {
        return new PushMessage
        {
            Type = PushMessageType.ToolResult,
            ToolName = toolName,
            Content = content,
            SequenceId = sequenceId,
            IsFinal = true
        };
    }

    /// <summary>
    /// 创建流式文本消息
    /// </summary>
    public static PushMessage StreamChunkMessage(string content, string? sequenceId = null, bool isFinal = false)
    {
        return new PushMessage
        {
            Type = PushMessageType.StreamChunk,
            Content = content,
            SequenceId = sequenceId,
            IsFinal = isFinal
        };
    }

    /// <summary>
    /// 创建流式结束消息
    /// </summary>
    public static PushMessage StreamEndMessage(string? sequenceId = null)
    {
        return new PushMessage
        {
            Type = PushMessageType.StreamEnd,
            SequenceId = sequenceId,
            IsFinal = true
        };
    }

    /// <summary>
    /// 创建错误消息
    /// </summary>
    public static PushMessage ErrorMessage(string error, string? sequenceId = null)
    {
        return new PushMessage
        {
            Type = PushMessageType.Error,
            Content = error,
            SequenceId = sequenceId,
            IsFinal = true
        };
    }

    /// <summary>
    /// 创建进度消息
    /// </summary>
    public static PushMessage ProgressMessage(string message, int? percent = null, string? sequenceId = null)
    {
        var metadata = new Dictionary<string, object?>();
        if (percent.HasValue)
        {
            metadata["percent"] = percent.Value;
        }

        return new PushMessage
        {
            Type = PushMessageType.Progress,
            Content = message,
            SequenceId = sequenceId,
            Metadata = metadata,
            IsFinal = false
        };
    }

    /// <summary>
    /// 创建完成消息
    /// </summary>
    public static PushMessage CompleteMessage(string? summary = null, string? sequenceId = null)
    {
        return new PushMessage
        {
            Type = PushMessageType.Complete,
            Content = summary,
            SequenceId = sequenceId,
            IsFinal = true
        };
    }

    /// <summary>
    /// 序列化为 SSE 格式行
    /// </summary>
    public string ToSseLine()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        });
        return $"data: {json}\n\n";
    }

    /// <summary>
    /// 序列化为 JSON 字符串
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        });
    }

    /// <summary>
    /// 从 JSON 反序列化
    /// </summary>
    public static PushMessage? FromJson(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<PushMessage>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        });
    }
}
