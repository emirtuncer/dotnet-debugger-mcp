using System.ComponentModel;
using DotNetDebuggerMcp.Dap;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;

namespace DotNetDebuggerMcp.Tools;

[McpServerToolType]
public class SessionTools(DebugSession session, DapClient dap)
{
    [McpServerTool(Name = "launch_process")]
    [Description("Launch a .NET application under the debugger. Spawns netcoredbg and starts the process. program should be the path to a .dll or .exe.")]
    public async Task<string> LaunchProcess(
        [Description("Path to the .NET assembly (.dll or .exe) to debug")] string program,
        [Description("Optional command-line arguments for the process")] string[]? args = null,
        [Description("Optional working directory for the process")] string? cwd = null,
        [Description("Stop at entry point before executing any code")] bool stopAtEntry = false)
    {
        if (session.State != SessionState.Disconnected && session.State != SessionState.Terminated)
        {
            return $"Error: A debug session is already active (state: {session.State}). Call disconnect first.";
        }

        if (!File.Exists(program))
            return $"Error: Program not found: {program}";

        try
        {
            session.Reset();
            session.SetState(SessionState.Initializing);

            await dap.StartAsync();

            // Initialize
            var initArgs = new InitializeArguments();
            await dap.SendRequestAsync("initialize", initArgs);

            // Launch
            var launchArgs = new LaunchArguments
            {
                Program = program,
                Args = args,
                Cwd = cwd ?? Path.GetDirectoryName(program),
                StopAtEntry = stopAtEntry
            };
            await dap.SendRequestAsync("launch", launchArgs);

            // Configuration done
            await dap.SendRequestAsync("configurationDone");

            return $"Debug session started. Process launching: {program}" +
                   (stopAtEntry ? " (stopped at entry)" : " (running)");
        }
        catch (Exception ex)
        {
            session.SetState(SessionState.Terminated);
            await TryStop();
            return $"Error launching process: {ex.Message}";
        }
    }

    [McpServerTool(Name = "attach_process")]
    [Description("Attach the debugger to an already-running .NET process by its PID.")]
    public async Task<string> AttachProcess(
        [Description("Process ID of the running .NET process to attach to")] int pid)
    {
        if (session.State != SessionState.Disconnected && session.State != SessionState.Terminated)
        {
            return $"Error: A debug session is already active (state: {session.State}). Call disconnect first.";
        }

        try
        {
            session.Reset();
            session.SetState(SessionState.Initializing);

            await dap.StartAsync();

            // Initialize
            var initArgs = new InitializeArguments();
            await dap.SendRequestAsync("initialize", initArgs);

            // Attach
            var attachArgs = new AttachArguments { ProcessId = pid };
            await dap.SendRequestAsync("attach", attachArgs);

            // Skip configurationDone for attach: netcoredbg on Windows returns 0x80070057 (E_INVALIDARG)
            // for configurationDone after attach (see nvim-dap/netcoredbg reports). Attach doesn't need it.

            return $"Attached to process {pid}. Debug session running.";
        }
        catch (Exception ex)
        {
            session.SetState(SessionState.Terminated);
            await TryStop();
            return $"Error attaching to process {pid}: {ex.Message}";
        }
    }

    [McpServerTool(Name = "disconnect")]
    [Description("Terminate the current debug session, stop netcoredbg, and kill the debugged process.")]
    public async Task<string> Disconnect()
    {
        if (session.State == SessionState.Disconnected)
            return "No active debug session.";

        try
        {
            if (dap.IsConnected)
            {
                var disconnectArgs = new DisconnectArguments { TerminateDebuggee = true };
                try { await dap.SendRequestAsync("disconnect", disconnectArgs); } catch { }
            }
        }
        finally
        {
            await TryStop();
            session.Reset();
        }

        return "Debug session disconnected.";
    }

    private async Task TryStop()
    {
        try { await dap.StopAsync(); } catch { }
    }
}
