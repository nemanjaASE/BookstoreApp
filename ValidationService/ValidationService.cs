using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Interfaces;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace ValidationService
{
	internal sealed class ValidationService : StatelessService, IValidation
    {
        private readonly ITransactionCoordinator _transactionCoordinatorService;
        public ValidationService(StatelessServiceContext context)
            : base(context)
        {
            var serviceProxyFactory = new ServiceProxyFactory((callbackClient) =>
            {
                return new FabricTransportServiceRemotingClientFactory(
                    new FabricTransportRemotingSettings
                {
                    ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
                }, callbackClient);
            });

            var serviceUri = new Uri("fabric:/BookstoreApp/TransactionCoordinatorService");

            _transactionCoordinatorService = serviceProxyFactory.CreateServiceProxy<ITransactionCoordinator>(serviceUri, new ServicePartitionKey(0));
        }


		public async Task ValidateBookAsync(string title, int quantity, string client)
		{
			Debug.WriteLine($"Client - Book Title: {title}, Quantity: {quantity} and Client: {client}");

            if (string.IsNullOrEmpty(title))
            {
                throw new ArgumentException("Title cannot be null or empty. Please provide a valid title.");
            }

            if (quantity <= 0)
            {
                throw new ArgumentException("Quantity must be greater than zero. Please provide a valid quantity.");
            }

            if (string.IsNullOrEmpty(client))
            {
                throw new ArgumentException("Client cannot be null or empty. Please provide a valid client name.");
            }

            await _transactionCoordinatorService.StartTransaction(title, quantity, client);
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new List<ServiceInstanceListener>
            {
                new ServiceInstanceListener(serviceContext =>
                    new FabricTransportServiceRemotingListener(
                        serviceContext,
                        this,
                        new FabricTransportRemotingListenerSettings
                            {
                                ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
                            }),
                        "ServiceEndpointV2")
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
