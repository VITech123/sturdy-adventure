using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseWorkReport.Services;

namespace EnterpriseWorkReport.Services
{
    /// <summary>
    /// Simple HTTP server for LAN access to the application
    /// Allows other computers on the network to view/access the application
    /// </summary>
    public class LanServerService
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private readonly int _port;
        private bool _isRunning;

        public event Action<string> LogMessage;
        public bool IsRunning => _isRunning;

        public LanServerService(int port = 5050)
        {
            _port = port;
        }

        public void Start()
        {
            if (_isRunning) return;

            Task.Run(() => StartServer());
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _cts?.Cancel();
            _listener?.Close();
            _isRunning = false;
            LogMessage?.Invoke("LAN Server stopped.");
        }

        private async Task StartServer()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{_port}/");
                
                _listener.Start();
                _isRunning = true;
                
                var ipAddresses = GetLocalIPAddresses();
                var msg = $"LAN Server started on port {_port}\n";
                msg += "Access URLs:\n";
                foreach (var ip in ipAddresses)
                {
                    msg += $"  http://{ip}:{_port}/\n";
                }
                msg += "Press Settings > Network Server to stop.";
                
                LogMessage?.Invoke(msg);

                while (_isRunning && !_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();
                        _ = Task.Run(() => HandleRequest(context));
                    }
                    catch (HttpListenerException) when (_cts.Token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke($"Error: {ex.Message}");
                    }
                }
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access Denied
            {
                LogMessage?.Invoke("Server Error: Access is denied. Please run the application as Administrator to enable LAN access.");
                _isRunning = false;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Server Error: {ex.Message}");
                _isRunning = false;
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // Build a simple HTML response with redirect to login or info
                string htmlContent = BuildHtmlResponse(request.Url?.ToString() ?? "");

                byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = 200;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Request Error: {ex.Message}");
            }
        }

        private string BuildHtmlResponse(string url)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<title>Enterprise Work Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 0; background: linear-gradient(135deg, #3B5BDB 0%, #7048E8 100%); min-height: 100vh; }");
            sb.AppendLine(".container { max-width: 700px; margin: 50px auto; background: white; border-radius: 16px; padding: 40px; box-shadow: 0 20px 60px rgba(0,0,0,0.3); }");
            sb.AppendLine("h1 { color: #3B5BDB; margin-bottom: 10px; }");
            sb.AppendLine(".subtitle { color: #666; margin-bottom: 30px; }");
            sb.AppendLine(".info-box { background: #f0f7ff; border-left: 4px solid #3B5BDB; padding: 20px; margin: 20px 0; border-radius: 0 8px 8px 0; }");
            sb.AppendLine(".info-box h3 { margin-top: 0; color: #3B5BDB; }");
            sb.AppendLine("ul { color: #444; line-height: 1.8; }");
            sb.AppendLine(".btn { display: inline-block; background: #3B5BDB; color: white; padding: 12px 30px; border-radius: 8px; text-decoration: none; margin: 10px 5px; }");
            sb.AppendLine(".btn:hover { background: #2a45b5; }");
            sb.AppendLine(".status { display: inline-block; background: #22c55e; color: white; padding: 5px 15px; border-radius: 20px; font-size: 12px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine("<div class='container'>");
            sb.AppendLine("<h1>📊 Enterprise Work Report</h1>");
            sb.AppendLine("<p class='subtitle'>Workforce Management Platform</p>");
            
            sb.AppendLine("<div class='info-box'>");
            sb.AppendLine("<h3>ℹ️ Access Information</h3>");
            sb.AppendLine("<p><strong>Status:</strong> <span class='status'>Server Running</span></p>");
            sb.AppendLine("<p>This is a desktop application. To use it, please:</p>");
            sb.AppendLine("<ol>");
            sb.AppendLine("<li>Open the application on this computer</li>");
            sb.AppendLine("<li>Log in with your credentials</li>");
            sb.AppendLine("<li>The application handles multiple users simultaneously</li>");
            sb.AppendLine("</ol>");
            sb.AppendLine("</div>");
            
            sb.AppendLine("<div class='info-box'>");
            sb.AppendLine("<h3>🌐 Network Features</h3>");
            sb.AppendLine("<ul>");
            sb.AppendLine("<li><strong>Multiple Users:</strong> Multiple users can work on the same data at the same time</li>");
            sb.AppendLine("<li><strong>Real-time Sync:</strong> All changes are immediately visible to all users</li>");
            sb.AppendLine("<li><strong>Data Safety:</strong> Data is automatically backed up and protected</li>");
            sb.AppendLine("</ul>");
            sb.AppendLine("</div>");
            
            sb.AppendLine("<p><strong>Note:</strong> This is a Windows desktop application. Contact your administrator to install it on your computer.</p>");
            sb.AppendLine("</div></body></html>");

            return sb.ToString();
        }

        private string[] GetLocalIPAddresses()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ips = new System.Collections.Generic.List<string>();
                
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        ips.Add(ip.ToString());
                    }
                }
                
                // Add localhost
                ips.Insert(0, "localhost");
                ips.Insert(1, "127.0.0.1");
                
                return ips.ToArray();
            }
            catch
            {
                return new[] { "localhost", "127.0.0.1" };
            }
        }
    }
}
