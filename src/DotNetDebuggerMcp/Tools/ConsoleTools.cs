using System.ComponentModel;
using System.Text;
using DotNetDebuggerMcp.Dap;
using ModelContextProtocol.Server;

namespace DotNetDebuggerMcp.Tools;

[McpServerToolType]
public class ConsoleTools(DebugSession session, DapClient dap)
{
    [McpServerTool(Name = "read_output")]
    [Description("Read buffered stdout/stderr output from the debugged process. Drains the buffer.")]
    public string ReadOutput(
        [Description("Maximum number of output lines to return (default 100)")] int maxLines = 100)
    {
        if (session.State == SessionState.Disconnected)
            return "No active debug session.";

        var lines = session.ReadOutput(maxLines);
        if (lines.Count == 0)
            return "(no output)";

        return string.Join("\n", lines);
    }

    [McpServerTool(Name = "send_stdin")]
    [Description("Send text input to the stdin of the debugged process.")]
    public async Task<string> SendStdin(
        [Description("Text to send to stdin (newline appended automatically if missing)")] string input)
    {
        session.EnsureConnected();

        if (!input.EndsWith('\n'))
            input += "\n";

        try
        {
            // DAP evaluate with context "stdin" or use a custom request
            // netcoredbg supports sending input via the "evaluate" request with context "stdin"
            await dap.SendRequestAsync("evaluate", new
            {
                expression = input,
                context = "stdin"
            });

            return $"Sent to stdin: {input.TrimEnd()}";
        }
        catch (Exception ex)
        {
            return $"Error sending stdin: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_debug_events")]
    [Description("Drain and return pending debug events (breakpoint hits, exceptions, output, thread changes, exits). Call this after stepping or continuing to see what happened.")]
    public string GetDebugEvents(
        [Description("Maximum number of events to return (default 50)")] int maxEvents = 50)
    {
        if (session.State == SessionState.Disconnected)
            return "No active debug session.";

        var events = session.DrainEvents(maxEvents);
        if (events.Count == 0)
            return $"No pending events. Current session state: {session.State}";

        var sb = new StringBuilder();
        sb.AppendLine($"Debug events ({events.Count}) — session state: {session.State}:");

        foreach (var evt in events)
        {
            string timestamp = evt.Timestamp.ToString("HH:mm:ss.fff");
            string typeStr = evt.Type.ToString();
            string threadStr = evt.ThreadId.HasValue ? $" [thread {evt.ThreadId}]" : "";
            string msgStr = evt.Message != null ? $": {evt.Message}" : "";

            sb.AppendLine($"  [{timestamp}] {typeStr}{threadStr}{msgStr}");
        }

        return sb.ToString().TrimEnd();
    }
}
