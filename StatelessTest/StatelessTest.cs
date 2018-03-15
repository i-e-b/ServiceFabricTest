using System;
using System.Collections.Generic;
using System.Fabric;
using System.Net;
using System.Text;
using FabricHttp;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace StatelessTest
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class StatelessTest : StatelessService
    {
        public StatelessTest(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new[] { new ServiceInstanceListener(CreateInternalListener) };
        }

        private ICommunicationListener CreateInternalListener(StatelessServiceContext context)
        {
            try
            {
                return new HttpCommunicationListener("", context, ProcessInternalRequest);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }
        
        private void ProcessInternalRequest(HttpListenerContext context)
        {
            string output;

            try
            {
                output = "Hello, world!";
            }
            catch (Exception ex)
            {
                output = ex.Message;
            }

            using (var response = context.Response)
            {
                if (output == null) return;

                var outBytes = Encoding.UTF8.GetBytes(output);
                response.OutputStream.Write(outBytes, 0, outBytes.Length);
            }
        }

    }
}
