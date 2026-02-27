using DotNetDebuggerMcp;
using DotNetDebuggerMcp.Dap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Ensure netcoredbg is present (auto-downloads on first run if missing).
// Sets NETCOREDBG_PATH so DapClient picks it up without any extra config.
string netcoredbgPath = await NetcoredbgInstaller.EnsureInstalledAsync();
Environment.SetEnvironmentVariable("NETCOREDBG_PATH", netcoredbgPath);

var builder = Host.CreateApplicationBuilder(args);

// Direct all logs to stderr so stdout remains clean for MCP STDIO transport
builder.Logging.AddConsole(opts =>
{
    opts.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton<DebugSession>();
builder.Services.AddSingleton<DapClient>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
