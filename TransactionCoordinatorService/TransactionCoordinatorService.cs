using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Interfaces;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;

namespace TransactionCoordinatorService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class TransactionCoordinatorService : StatefulService, ITransactionCoordinator
    {
        private readonly IBookstore _bookstoreService;
		private readonly IBank _bankService;
		public TransactionCoordinatorService(StatefulServiceContext context)
            : base(context)
        {
            var serviceProxyFactory1 = new ServiceProxyFactory((callbackClient) =>
            {
                return new FabricTransportServiceRemotingClientFactory(
                    new FabricTransportRemotingSettings
                    {
                        ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
                    }, callbackClient);
            });

            var serviceUri1 = new Uri("fabric:/BookstoreApp/BookstoreService");

            _bookstoreService = serviceProxyFactory1.CreateServiceProxy<IBookstore>(serviceUri1, new ServicePartitionKey(0));

			var serviceProxyFactory2 = new ServiceProxyFactory((callbackClient) =>
			{
				return new FabricTransportServiceRemotingClientFactory(
					new FabricTransportRemotingSettings
					{
						ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
					}, callbackClient);
			});

			var serviceUri2 = new Uri("fabric:/BookstoreApp/BankService");

			_bankService = serviceProxyFactory2.CreateServiceProxy<IBank>(serviceUri2, new ServicePartitionKey(0));
		}


        public async Task StartTransaction(string title, int quantity, string client)
        {
            var availableBooks = await _bookstoreService.ListAvailableItems();

			var bookID = availableBooks.FirstOrDefault(b => b.Value.Title.Equals(title, StringComparison.OrdinalIgnoreCase)).Key;

            double price = await _bookstoreService.GetItemPrice(bookID);

            var clients = await _bankService.ListClients();

            var clientID = clients.FirstOrDefault(c => c.Value.ClientName.Equals(client)).Key;

            double amount = quantity * price;

            try
            {
                await _bookstoreService.EnlistPurchase(bookID, (uint)quantity);
                await _bankService.EnlistMoneyTransfer(clientID, amount);

                bool isPreparedBookstore = await _bookstoreService.Prepare();
                bool isPreparedBank = await _bankService.Prepare();

                if(isPreparedBookstore && isPreparedBank)
                {
                    await _bookstoreService.Commit();
                    await _bankService.Commit();
                }
                else
                {
                    await _bookstoreService.Rollback();
                    await _bankService.Rollback();
                }
            }
            catch (Exception ex)
            {
				await _bookstoreService.Rollback();
                await _bankService.Rollback();
				throw new Exception(ex.Message);
            }
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {

            return new List<ServiceReplicaListener>
            {
                new ServiceReplicaListener(serviceContext =>
                    new FabricTransportServiceRemotingListener(
                        serviceContext,
                        this, new FabricTransportRemotingListenerSettings
                            {
                                ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
                            })
                    )
            };
        }
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
