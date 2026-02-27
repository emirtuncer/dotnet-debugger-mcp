using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotNetDebuggerMcp.Dap;

/// <summary>
/// Handles DAP message framing: Content-Length headers + JSON body
/// </summary>
public static class DapProtocol
{
    private const string HeaderSeparator = "\r\n\r\n";
    private const string ContentLengthHeader = "Content-Length: ";

    public static byte[] Encode(object message)
    {
        string json = JsonConvert.SerializeObject(message, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
        byte[] body = Encoding.UTF8.GetBytes(json);
        string header = $"{ContentLengthHeader}{body.Length}{HeaderSeparator}";
        byte[] headerBytes = Encoding.UTF8.GetBytes(header);

        var result = new byte[headerBytes.Length + body.Length];
        headerBytes.CopyTo(result, 0);
        body.CopyTo(result, headerBytes.Length);
        return result;
    }

    /// <summary>
    /// Reads a single DAP message from the stream. Returns null on EOF.
    /// </summary>
    public static async Task<JObject?> ReadMessageAsync(Stream stream, CancellationToken ct)
    {
        // Read headers until we find Content-Length
        int contentLength = -1;
        var headerBuffer = new List<byte>();

        while (true)
        {
            // Read byte by byte until \r\n\r\n
            int b = await ReadByteAsync(stream, ct);
            if (b == -1) return null;
            headerBuffer.Add((byte)b);

            // Check for end of headers
            if (headerBuffer.Count >= 4)
            {
                int n = headerBuffer.Count;
                if (headerBuffer[n - 4] == '\r' && headerBuffer[n - 3] == '\n' &&
                    headerBuffer[n - 2] == '\r' && headerBuffer[n - 1] == '\n')
                {
                    string headers = Encoding.UTF8.GetString(headerBuffer.ToArray());
                    foreach (var line in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.StartsWith(ContentLengthHeader, StringComparison.OrdinalIgnoreCase))
                        {
                            contentLength = int.Parse(line[ContentLengthHeader.Length..].Trim());
                            break;
                        }
                    }
                    break;
                }
            }
        }

        if (contentLength <= 0)
            throw new InvalidDataException($"Invalid Content-Length: {contentLength}");

        byte[] body = new byte[contentLength];
        int read = 0;
        while (read < contentLength)
        {
            int n = await stream.ReadAsync(body.AsMemory(read, contentLength - read), ct);
            if (n == 0) return null;
            read += n;
        }

        string json = Encoding.UTF8.GetString(body);
        return JObject.Parse(json);
    }

    private static async Task<int> ReadByteAsync(Stream stream, CancellationToken ct)
    {
        var buf = new byte[1];
        int n = await stream.ReadAsync(buf.AsMemory(0, 1), ct);
        return n == 0 ? -1 : buf[0];
    }
}
