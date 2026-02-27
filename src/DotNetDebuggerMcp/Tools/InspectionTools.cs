using System.ComponentModel;
using System.Text;
using DotNetDebuggerMcp.Dap;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;

namespace DotNetDebuggerMcp.Tools;

[McpServerToolType]
public class InspectionTools(DebugSession session, DapClient dap)
{
    [McpServerTool(Name = "get_variables")]
    [Description("Get local variables and arguments at the current stack frame. Process must be paused.")]
    public async Task<string> GetVariables(
        [Description("Stack frame ID (from get_call_stack). Defaults to the top frame.")] int? frameId = null)
    {
        session.EnsurePaused();

        try
        {
            int fid = frameId ?? await GetTopFrameIdAsync();

            // Get scopes for this frame
            var scopesResp = await dap.SendRequestAsync("scopes", new { frameId = fid });
            var scopes = scopesResp["body"]?["scopes"] as JArray ?? [];

            var sb = new StringBuilder();

            foreach (var scope in scopes)
            {
                string scopeName = scope["name"]?.Value<string>() ?? "Unknown";
                int varRef = scope["variablesReference"]?.Value<int>() ?? 0;
                bool expensive = scope["expensive"]?.Value<bool>() ?? false;

                if (varRef == 0 || expensive) continue;

                sb.AppendLine($"[{scopeName}]");

                var varsResp = await dap.SendRequestAsync("variables", new { variablesReference = varRef });
                var variables = varsResp["body"]?["variables"] as JArray ?? [];

                if (!variables.Any())
                {
                    sb.AppendLine("  (no variables)");
                    continue;
                }

                foreach (var variable in variables)
                {
                    string name = variable["name"]?.Value<string>() ?? "?";
                    string value = variable["value"]?.Value<string>() ?? "null";
                    string? type = variable["type"]?.Value<string>();

                    string typeDisplay = type != null ? $" ({type})" : "";
                    sb.AppendLine($"  {name}{typeDisplay} = {value}");
                }
            }

            return sb.Length > 0 ? sb.ToString().TrimEnd() : "No variables found at this frame.";
        }
        catch (Exception ex)
        {
            return $"Error getting variables: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_call_stack")]
    [Description("Get the call stack frames for a thread. Process must be paused.")]
    public async Task<string> GetCallStack(
        [Description("Thread ID (defaults to the stopped thread)")] int? threadId = null)
    {
        session.EnsurePaused();

        int tid = threadId ?? session.GetDefaultThreadId();

        try
        {
            var resp = await dap.SendRequestAsync("stackTrace", new { threadId = tid, levels = 20 });
            var frames = resp["body"]?["stackFrames"] as JArray ?? [];

            if (!frames.Any())
                return "No stack frames available.";

            var sb = new StringBuilder();
            sb.AppendLine($"Call stack (thread {tid}):");

            int i = 0;
            foreach (var frame in frames)
            {
                int id = frame["id"]?.Value<int>() ?? 0;
                string name = frame["name"]?.Value<string>() ?? "<unknown>";
                string? sourcePath = frame["source"]?["path"]?.Value<string>();
                string? sourceName = frame["source"]?["name"]?.Value<string>();
                int line = frame["line"]?.Value<int>() ?? 0;

                string location = sourcePath != null
                    ? $"{sourcePath}:{line}"
                    : (sourceName != null ? $"{sourceName}:{line}" : "<no source>");

                sb.AppendLine($"  #{i} [id={id}] {name}");
                sb.AppendLine($"       at {location}");
                i++;
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Error getting call stack: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_threads")]
    [Description("List all threads in the debugged process and their current state.")]
    public async Task<string> GetThreads()
    {
        session.EnsureConnected();

        try
        {
            var resp = await dap.SendRequestAsync("threads");
            var threads = resp["body"]?["threads"] as JArray ?? [];

            if (!threads.Any())
                return "No threads found.";

            var sb = new StringBuilder();
            int stoppedTid = session.StoppedThreadId;

            sb.AppendLine("Threads:");
            foreach (var thread in threads)
            {
                int id = thread["id"]?.Value<int>() ?? 0;
                string name = thread["name"]?.Value<string>() ?? $"Thread {id}";
                string marker = id == stoppedTid ? " ← stopped" : "";
                sb.AppendLine($"  [{id}] {name}{marker}");
            }

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Error getting threads: {ex.Message}";
        }
    }

    [McpServerTool(Name = "evaluate_expression")]
    [Description("Evaluate a C# expression in the context of the current frame. Process must be paused.")]
    public async Task<string> EvaluateExpression(
        [Description("C# expression to evaluate (e.g., 'myVar.ToString()', '1 + 1')")] string expression,
        [Description("Frame ID to evaluate in (defaults to top frame)")] int? frameId = null)
    {
        session.EnsurePaused();

        try
        {
            int fid = frameId ?? await GetTopFrameIdAsync();

            var args = new EvaluateArguments
            {
                Expression = expression,
                FrameId = fid,
                Context = "repl"
            };

            var resp = await dap.SendRequestAsync("evaluate", args);
            string result = resp["body"]?["result"]?.Value<string>() ?? "null";
            string? type = resp["body"]?["type"]?.Value<string>();

            return type != null ? $"{result} ({type})" : result;
        }
        catch (DapException ex)
        {
            return $"Evaluation error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error evaluating expression: {ex.Message}";
        }
    }

    private async Task<int> GetTopFrameIdAsync()
    {
        int tid = session.GetDefaultThreadId();
        var resp = await dap.SendRequestAsync("stackTrace", new { threadId = tid, levels = 1 });
        var frames = resp["body"]?["stackFrames"] as JArray;
        int id = frames?[0]?["id"]?.Value<int>() ?? 0;
        if (id == 0)
            throw new InvalidOperationException("Could not get top stack frame.");
        return id;
    }
}
