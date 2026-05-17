using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace PicAnalysis;
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

        var app = builder.Build();
        app.Urls.Add("http://localhost:5000");
        app.MapMcp("/mcp");

        Console.WriteLine("✅ MCP 服务器已启动：http://localhost:5000/mcp");
        await app.RunAsync();
    }
}
