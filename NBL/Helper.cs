using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace NBL;

public class Helper
{
    public event Func<string, Task> OnRecv;
    
    private string _path;
    private string _dir;
    private Process? _process;
    private NamedPipeClientStream? _pipeClientStream;
    private string _pipeName;
    
    public Helper(string path, string dir)
    {
        this._path = path;
        this._dir = dir;
        this._pipeName = Guid.NewGuid().ToString("N");
    }

    private void Debug(string txt) => System.Diagnostics.Debug.WriteLine($"HELPER: {txt}");
    
    private async Task Invoke(string cmd)
    {
        this.Debug($"invoked '{cmd}'");
        if (this._process is null || this._process.HasExited)
        {
            if (!await this.RunProcess())
            {
                this.Debug($"invocation failed: the process was not started");
                return;
            }
        }

        if (this._pipeClientStream is null || !this._pipeClientStream.IsConnected)
        {
            this.Debug($"invocation failed: no connection to the server");
            return;
        }
        
        byte[] msg = Encoding.UTF8.GetBytes(cmd);
        await this._pipeClientStream.WriteAsync(msg, 0, msg.Length);
        await this._pipeClientStream.FlushAsync();
    }

    public async Task GenNewKey()
    {
        Random random = new Random();
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        
        StringBuilder sb = new StringBuilder(20);

        for (int i = 0; i < 20; i++) {
            sb.Append(chars[random.Next(chars.Length)]);
        }

        string key = sb.ToString();

        await this.SetNewKey(key);
    }
    
    public async Task SetNewKey(string key)
    {
        await this.Invoke($"setkey {key}");
    }

    private async Task<bool> RunProcess()
    {
        this.Debug($"starting a new process");
        ProcessStartInfo processStartInfo = new ProcessStartInfo { FileName = this._path, Verb = "runas", UseShellExecute = true, Arguments = $"{this._pipeName}", WorkingDirectory = this._dir, CreateNoWindow = true };
        this._process = new Process { StartInfo = processStartInfo };
        this._pipeClientStream = new NamedPipeClientStream(".", this._pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            this._process.Start();
            this.Debug($"process '{this._process.Id}' started");
            await this._pipeClientStream.ConnectAsync(5000);
            this.Debug($"connected to the server");
            _ = this.Loop();
            
            return true;
        }
        catch (Exception e)
        {
            this.Debug($"failed to start a new process: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    private async Task Loop()
    {
        while (true)
        {
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead = await this._pipeClientStream.ReadAsync(buffer, 0, buffer.Length);
                await this.OnRecv.Invoke(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }
            catch (Exception ex)
            {
                this._pipeClientStream.Close();
                break;
            }
        }
    }

    public async Task Stop()
    {
        if (this._process is null || this._process.HasExited) return;
        await this._process.WaitForExitAsync();
    }

    public void Dispose()
    {
        this._process?.Dispose();        
    }
}