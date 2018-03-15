using System;
using System.Fabric;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;

namespace FabricHttp
{
    public class HttpCommunicationListener : ICommunicationListener
    {
        private readonly string _appRoot;
        private readonly ServiceContext _serviceContext;
        private readonly Action<HttpListenerContext> _processInternalRequest;

        private string _listeningAddress;
        private string _published;

        private HttpListener _listener;
        private IDisposable _serverHandle;

        /// <summary>
        /// Setup a HTTP listener
        /// </summary>
        /// <param name="appRoot">Optional: Root path in the listening URL, if this is a stateless app. Can be null or empty</param>
        /// <param name="serviceInitializationParameters">Required: Service context</param>
        /// <param name="processInternalRequest">Required: Action to perform when a HTTP request is received</param>
        public HttpCommunicationListener(string appRoot, ServiceContext serviceInitializationParameters, Action<HttpListenerContext> processInternalRequest)
        {
            _appRoot = appRoot;
            _serviceContext = serviceInitializationParameters;
            _processInternalRequest = processInternalRequest;
        }

        public Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            var serviceEndpoint = _serviceContext.CodePackageActivationContext.GetEndpoint("ServiceEndpoint");
            var port = serviceEndpoint.Port;

            switch (_serviceContext)
            {
                case StatefulServiceContext _:
                    var ctx = (StatefulServiceContext) _serviceContext;

                    _listeningAddress = string.Format(
                        CultureInfo.InvariantCulture,
                        "http://+:{0}/{1}/{2}/{3}",
                        port,
                        ctx.NodeContext.NodeName,//ctx.PartitionId,
                        ctx.PartitionId.ToString().Substring(0,4),//ctx.ReplicaId,
                        "");//Guid.NewGuid().ToString() + '/');
                    break;
                case StatelessServiceContext _:
                    _listeningAddress = string.Format(
                        CultureInfo.InvariantCulture,
                        "http://+:{0}/{1}",
                        port,
                        string.IsNullOrWhiteSpace(_appRoot)
                            ? string.Empty
                            : _appRoot.TrimEnd('/') + '/');
                    break;
                default:
                    throw new InvalidOperationException();
            }

            _published = _listeningAddress.Replace("+", FabricRuntime.GetNodeContext().IPAddressOrFQDN);
            
            _listener = new HttpListener();
            _listener.Prefixes.Add(_listeningAddress);

            _listener.Start();
            StartListenerLoop();

            Console.WriteLine("Listener starting on " + _listeningAddress + ", this node published at " + _published);
            System.IO.File.AppendAllText(@"C:\Temp\FabricLog.txt", "\r\nOpening "+_published);

            _serverHandle = _listener;


            return Task.FromResult(_published);
        }

        
        private void StartListenerLoop()
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                try
                {
                    while (_listener.IsListening)
                        ThreadPool.QueueUserWorkItem(c =>
                        {
                            if (!(c is HttpListenerContext ctx)) return;
                            _processInternalRequest(ctx);
                        }, _listener.GetContext());
                }
                catch (HttpListenerException)
                {
                    Ignore();
                }
            });

        }

        private static void Ignore() { }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            StopWebServer();

            return Task.FromResult(true);
        }

        public void Abort()
        {
            StopWebServer();
        }

        private void StopWebServer()
        {
            if (_serverHandle == null) return;
            try
            {
                _serverHandle.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // no-op
            }
        }
    }
}
