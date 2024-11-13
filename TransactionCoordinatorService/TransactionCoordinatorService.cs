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
			Guid transactionId = Guid.NewGuid();

			var availableBooks = await _bookstoreService.ListAvailableItems();

			var bookResult = availableBooks.FirstOrDefault(b => b.Value.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

            string bookId = bookResult.Key;

            double price = await _bookstoreService.GetItemPrice(bookId);

            var clients = await _bankService.ListClients();

            var clientResult = clients.FirstOrDefault(c => c.Value.ClientName.Equals(client));

            string clientId = clientResult.Key;

            double amount = quantity * price;

            Debug.WriteLine($"State before transaction - " +
                $"BOOK (BookID: {bookResult.Key} Quantity: {bookResult.Value.Quantity}) " +
                $"CLIENT (ClientID: {clientResult.Key} Quantity: {clientResult.Value.Balance})");

            try
            {
				await _bookstoreService.EnlistPurchase(transactionId, bookId, (uint)quantity);
                await _bankService.EnlistMoneyTransfer(transactionId, clientId, amount);

                Debug.WriteLine("Enlist Ended.");

				bool isPreparedBookstore = await _bookstoreService.Prepare(transactionId);
                bool isPreparedBank = await _bankService.Prepare(transactionId);

				Debug.WriteLine("Prepare Ended.");

				if (isPreparedBookstore && isPreparedBank)
                {
					await _bookstoreService.Commit(transactionId);
                    await _bankService.Commit(transactionId);

					Debug.WriteLine("Commit Ended.");

					availableBooks = await _bookstoreService.ListAvailableItems();

                    foreach (var item in availableBooks)
                    {
                        Debug.WriteLine($"{item.Key} {item.Value.Title} {item.Value.Quantity}");
                    }

					clients = await _bankService.ListClients();

					foreach (var item in clients)
					{
						Debug.WriteLine($"{item.Key} {item.Value.ClientName} {item.Value.Balance}");
					}
				}
                else
                {
					await _bookstoreService.Rollback(transactionId);
                    await _bankService.Rollback(transactionId);

					Debug.WriteLine("Rollback Ended.");
				}
            }
            catch (Exception ex)
            {
				await _bookstoreService.Rollback(transactionId);
                await _bankService.Rollback(transactionId);

				Debug.WriteLine("Rollback Ended.");
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
    }
}
