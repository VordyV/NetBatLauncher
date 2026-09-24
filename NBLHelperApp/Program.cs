using System.IO.Pipes;
using System.Text;

namespace NBLHelperApp;

class Program
{
    
    private static string PipeName;
    private static readonly CancellationTokenSource _cts = new();
    
    private static readonly Dictionary<string, Func<string[], string>> _commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["setkey"] = OnCmd_SetKey,
    };

    static string OnCmd_SetKey(string[] args)
    {
        return "ok";
    }

    static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("pipe name not provided");
            return;
        }

        PipeName = args[0];

        if (PipeName.Length != 32)
        {
            Console.WriteLine("invalid pipe name value");
            return;
        }

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };
        
        while (!_cts.Token.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                transmissionMode: PipeTransmissionMode.Byte,
                options: PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(_cts.Token);
                
                await HandleClientAsync(pipe);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }
    }

    private static async Task HandleClientAsync(NamedPipeServerStream pipe)
    {
        byte[] buffer = new byte[4096];

        while (pipe.IsConnected && !_cts.Token.IsCancellationRequested)
        {
            int bytesRead;

            try
            {
                bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }

            if (bytesRead == 0)
                break;

            string received = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            if (string.IsNullOrWhiteSpace(received))
                continue;
            
            string[] parts = received.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0];
            string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            string response;

            if (_commands.TryGetValue(command, out var handler))
            {
                response = handler(args);

                if (response == "DISCONNECT")
                {
                    await SendAsync(pipe, "1");
                    break;
                }

                if (response == "SHUTDOWN")
                {
                    await SendAsync(pipe, "1");
                    _cts.Cancel();
                    break;
                }
            }
            else
            {
                response = $"Unknown command: \"{command}\". Type help";
            }

            await SendAsync(pipe, response);
        }
    }
    
    private static async Task SendAsync(NamedPipeServerStream pipe, string message)
    {
        if (!pipe.IsConnected) return;

        byte[] data = Encoding.UTF8.GetBytes(message + "\n");
        await pipe.WriteAsync(data, 0, data.Length);
        await pipe.FlushAsync();
    }
}