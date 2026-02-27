using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotNetDebuggerMcp.Dap;

// Base message types
public class DapMessage
{
    [JsonProperty("seq")]
    public int Seq { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = "";
}

public class DapRequest : DapMessage
{
    public DapRequest() => Type = "request";

    [JsonProperty("command")]
    public string Command { get; set; } = "";

    [JsonProperty("arguments")]
    public JObject? Arguments { get; set; }
}

public class DapResponse : DapMessage
{
    [JsonProperty("request_seq")]
    public int RequestSeq { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("command")]
    public string Command { get; set; } = "";

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("body")]
    public JToken? Body { get; set; }
}

public class DapEvent : DapMessage
{
    public DapEvent() => Type = "event";

    [JsonProperty("event")]
    public string EventType { get; set; } = "";

    [JsonProperty("body")]
    public JToken? Body { get; set; }
}

// Initialize request/response
public class InitializeArguments
{
    [JsonProperty("clientID")]
    public string ClientId { get; set; } = "dotnet-debugger-mcp";

    [JsonProperty("clientName")]
    public string ClientName { get; set; } = "DotNet Debugger MCP";

    [JsonProperty("adapterID")]
    public string AdapterId { get; set; } = "coreclr";

    [JsonProperty("locale")]
    public string Locale { get; set; } = "en-US";

    [JsonProperty("linesStartAt1")]
    public bool LinesStartAt1 { get; set; } = true;

    [JsonProperty("columnsStartAt1")]
    public bool ColumnsStartAt1 { get; set; } = true;

    [JsonProperty("pathFormat")]
    public string PathFormat { get; set; } = "path";

    [JsonProperty("supportsVariableType")]
    public bool SupportsVariableType { get; set; } = true;

    [JsonProperty("supportsRunInTerminalRequest")]
    public bool SupportsRunInTerminalRequest { get; set; } = false;
}

// Launch arguments
public class LaunchArguments
{
    [JsonProperty("type")]
    public string Type { get; set; } = "coreclr";

    [JsonProperty("request")]
    public string Request { get; set; } = "launch";

    [JsonProperty("program")]
    public string Program { get; set; } = "";

    [JsonProperty("args")]
    public string[]? Args { get; set; }

    [JsonProperty("cwd")]
    public string? Cwd { get; set; }

    [JsonProperty("stopAtEntry")]
    public bool StopAtEntry { get; set; } = false;

    [JsonProperty("console")]
    public string Console { get; set; } = "internalConsole";
}

// Attach arguments
public class AttachArguments
{
    [JsonProperty("type")]
    public string Type { get; set; } = "coreclr";

    [JsonProperty("request")]
    public string Request { get; set; } = "attach";

    [JsonProperty("processId")]
    public int ProcessId { get; set; }
}

// Breakpoint types
public class SourceBreakpoint
{
    [JsonProperty("line")]
    public int Line { get; set; }

    [JsonProperty("condition")]
    public string? Condition { get; set; }
}

public class SetBreakpointsArguments
{
    [JsonProperty("source")]
    public DapSource Source { get; set; } = new();

    [JsonProperty("breakpoints")]
    public SourceBreakpoint[] Breakpoints { get; set; } = [];
}

public class DapSource
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("path")]
    public string? Path { get; set; }
}

public class FunctionBreakpoint
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("condition")]
    public string? Condition { get; set; }
}

public class SetFunctionBreakpointsArguments
{
    [JsonProperty("breakpoints")]
    public FunctionBreakpoint[] Breakpoints { get; set; } = [];
}

public class Breakpoint
{
    [JsonProperty("id")]
    public int? Id { get; set; }

    [JsonProperty("verified")]
    public bool Verified { get; set; }

    [JsonProperty("line")]
    public int? Line { get; set; }

    [JsonProperty("source")]
    public DapSource? Source { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }
}

// Execution control
public class ContinueArguments
{
    [JsonProperty("threadId")]
    public int ThreadId { get; set; }
}

public class NextArguments
{
    [JsonProperty("threadId")]
    public int ThreadId { get; set; }

    [JsonProperty("granularity")]
    public string? Granularity { get; set; }
}

public class StepInArguments
{
    [JsonProperty("threadId")]
    public int ThreadId { get; set; }

    [JsonProperty("granularity")]
    public string? Granularity { get; set; }
}

public class StepOutArguments
{
    [JsonProperty("threadId")]
    public int ThreadId { get; set; }
}

// Inspection types
public class Thread
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = "";
}

public class StackFrame
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("source")]
    public DapSource? Source { get; set; }

    [JsonProperty("line")]
    public int Line { get; set; }

    [JsonProperty("column")]
    public int Column { get; set; }
}

public class Scope
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("variablesReference")]
    public int VariablesReference { get; set; }

    [JsonProperty("expensive")]
    public bool Expensive { get; set; }
}

public class Variable
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("value")]
    public string Value { get; set; } = "";

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("variablesReference")]
    public int VariablesReference { get; set; }
}

public class EvaluateArguments
{
    [JsonProperty("expression")]
    public string Expression { get; set; } = "";

    [JsonProperty("frameId")]
    public int? FrameId { get; set; }

    [JsonProperty("context")]
    public string Context { get; set; } = "repl";
}

// Disconnect
public class DisconnectArguments
{
    [JsonProperty("restart")]
    public bool Restart { get; set; } = false;

    [JsonProperty("terminateDebuggee")]
    public bool TerminateDebuggee { get; set; } = true;
}
