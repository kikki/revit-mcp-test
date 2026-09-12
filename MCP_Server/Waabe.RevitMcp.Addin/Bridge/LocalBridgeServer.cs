using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Waabe.RevitMcp.Addin.Bridge
{
    internal sealed class LocalBridgeServer : IDisposable
    {
        private readonly int _year;
        private readonly int _port;
        private readonly string _token;
        private readonly string _discoveryPath;
        private readonly RevitRequestDispatcher _dispatcher;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private TcpListener _listener;
        private Task _acceptLoop;

        public LocalBridgeServer(int year, RevitRequestDispatcher dispatcher)
        {
            _year = year;
            _port = 48000 + (year % 100); // 48023 and 48026; one endpoint per supported Revit year.
            _token = Guid.NewGuid().ToString("N");
            _dispatcher = dispatcher;
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Waabe", "RevitMcp");
            Directory.CreateDirectory(directory);
            _discoveryPath = Path.Combine(directory, "bridge-" + year + ".json");
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            WriteDiscovery();
            _acceptLoop = Task.Run((Func<Task>)AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException) when (_stop.IsCancellationRequested) { return; }
                catch (SocketException) when (_stop.IsCancellationRequested) { return; }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, true))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true })
            {
                try
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        await writer.WriteLineAsync(JsonWire.Serialize(BridgeResponse.Failure("EMPTY_REQUEST", "Request is empty."))).ConfigureAwait(false);
                        return;
                    }

                    var request = JsonWire.Deserialize<BridgeRequest>(line);
                    if (!FixedTimeEquals(request?.Token, _token))
                    {
                        await writer.WriteLineAsync(JsonWire.Serialize(BridgeResponse.Failure("UNAUTHORIZED", "Invalid bridge token."))).ConfigureAwait(false);
                        return;
                    }

                    var operation = _dispatcher.Enqueue(request.Method);
                    var completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(20))).ConfigureAwait(false);
                    var response = completed == operation
                        ? await operation.ConfigureAwait(false)
                        : BridgeResponse.Failure("REVIT_TIMEOUT", "Revit did not process the request within 20 seconds.");
                    await writer.WriteLineAsync(JsonWire.Serialize(response)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await writer.WriteLineAsync(JsonWire.Serialize(BridgeResponse.Failure("BRIDGE_ERROR", ex.Message))).ConfigureAwait(false);
                }
            }
        }

        private void WriteDiscovery()
        {
            var record = new DiscoveryRecord
            {
                Schema = "waabe-revit-mcp-discovery/1",
                RevitYear = _year,
                Host = "127.0.0.1",
                Port = _port,
                Token = _token,
                ProcessId = Process.GetCurrentProcess().Id
            };
            var temp = _discoveryPath + ".tmp";
            File.WriteAllText(temp, JsonWire.Serialize(record), new UTF8Encoding(false));
            if (File.Exists(_discoveryPath)) File.Delete(_discoveryPath);
            File.Move(temp, _discoveryPath);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        public void Dispose()
        {
            _stop.Cancel();
            try { _listener?.Stop(); } catch { }
            try
            {
                if (File.Exists(_discoveryPath) && File.ReadAllText(_discoveryPath).Contains(_token))
                    File.Delete(_discoveryPath);
            }
            catch { }
            _stop.Dispose();
        }
    }
}
