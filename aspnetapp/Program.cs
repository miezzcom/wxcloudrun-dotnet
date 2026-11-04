using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

var upstreamHost = builder.Configuration["Proxy:Host"] ?? "www.miezz.com";
var upstreamAddress = builder.Configuration["ReverseProxy:Clusters:production:Destinations:primary:Address"]
    ?? "https://116.62.125.28/";
var upstreamUri = new Uri(upstreamAddress);
var upstreamSni = upstreamUri.Host;
var upstreamEndpoint = new IPEndPoint(IPAddress.Parse(upstreamSni), upstreamUri.Port);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = null;
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .ConfigureHttpClient((_, handler) =>
    {
        // HttpClient 会用 Host 头做 SNI。Host 必须是 www.miezz.com，入口才把 /api 和 /auth 分到对应服务。
        // SNI 若是这个域名，阿里云会把未备案接入的连接重置。握手单独做，SNI 不用这个域名。
        handler.ConnectCallback = async (_, cancellationToken) =>
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(upstreamEndpoint, cancellationToken);
                var stream = new NetworkStream(socket, ownsSocket: true);
                var ssl = new SslStream(stream);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    RemoteCertificateValidationCallback = static (_, _, _, _) => true,
                }, cancellationToken);
                return ssl;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
    })
    .AddTransforms(context =>
    {
        context.AddRequestTransform(transform =>
        {
            transform.ProxyRequest.Headers.Host = upstreamHost;
            transform.ProxyRequest.Headers.Remove("X-Forwarded-Proto");
            transform.ProxyRequest.Headers.Remove("X-Forwarded-Host");
            transform.ProxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            transform.ProxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", upstreamHost);
            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();
app.MapReverseProxy();
app.Run();
