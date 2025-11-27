using PulseLink.ViewModels;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PulseLink.Services;

public class HttpServerService : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Func<int> _getBpmFunc;
    private readonly CancellationTokenSource _cts = new();

    public string ServerUrl { get; }

    public HttpServerService(Func<int> getBpmFunc)
    {
        _getBpmFunc = getBpmFunc;
        ServerUrl = Config.HttpServerBaseUrl;
        _listener.Prefixes.Add(ServerUrl); // Uses the updated Config.HttpServerBaseUrl (with new port)
        _listener.Prefixes.Add($"http://127.0.0.1:{Config.HttpServerPort}/");
    }

    public void Start()
    {
        _listener.Start();
        Task.Run(() => ListenLoop(_cts.Token));
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context), token);
            }
            catch (HttpListenerException) when (token.IsCancellationRequested)
            {
                // Listener was stopped, exit loop
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"HttpServerService: Error in ListenLoop: {ex.Message}");
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            if (request.Url?.AbsolutePath == "/api/bpm")
            {
                await HandleApiRequest(response);
            }
            else
            {
                await HandlePageRequest(response);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"HttpServerService: Error processing request: {ex.Message}");
        }
        finally
        {
            response.OutputStream.Close();
        }
    }

    private async Task HandleApiRequest(HttpListenerResponse response)
    {
        var payload = JsonSerializer.Serialize(new { bpm = _getBpmFunc() });
        var buffer = Encoding.UTF8.GetBytes(payload);
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    private async Task HandlePageRequest(HttpListenerResponse response)
    {
        string html = GenerateHtml();
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }

    private string GenerateHtml()
    {
        return @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>PulseLink Direct</title>
    <style>
        body { background: #111; color: #00FF41; font-family: system-ui, sans-serif; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }
        #bpm { font-size: 15vmin; font-weight: bold; text-shadow: 0 0 20px #00FF41; }
        .pump { animation: beat 0.5s infinite alternate; }
        @keyframes beat { from { transform: scale(1); } to { transform: scale(1.05); } }
    </style>
</head>
<body>
    <div id=""bpm"">--</div>
    <script>
        const bpmElement = document.getElementById('bpm');
        async function fetchBpm() {
            try {
                const response = await fetch('/api/bpm');
                if (!response.ok) throw new Error('Network response was not ok');
                const data = await response.json();
                const bpm = data.bpm;

                bpmElement.textContent = bpm > 0 ? bpm : '--';
                if(bpm > 0) {
                    bpmElement.classList.add('pump');
                    bpmElement.style.animationDuration = (60 / bpm) + 's';
                } else {
                    bpmElement.classList.remove('pump');
                }
            } catch (error) {
                bpmElement.textContent = 'ERR';
                bpmElement.classList.remove('pump');
            }
        }
        setInterval(fetchBpm, 1000);
        fetchBpm(); // Initial fetch
    </script>
</body>
</html>";
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
        _cts.Dispose();
    }
}
