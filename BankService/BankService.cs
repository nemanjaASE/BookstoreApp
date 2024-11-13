using System.Diagnostics;
using System.Fabric;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace BankService
{

    internal sealed class BankService(StatefulServiceContext context) : StatefulService(context), IBank
    {
		private const string CLIENTS_DICTIONARY = "accounts";
		private const string RESERVED_FUNDS_DICTIONARY = "reserved_funds";

		private IReliableDictionary<string, Client>? _clients;
		private IReliableDictionary<Guid, ReservedFund>? _reservedFunds;

		public async Task Commit(Guid transactionId)
		{
			_clients = await StateManager.GetOrAddAsync<IReliableDictionary<string, Client>>(CLIENTS_DICTIONARY);

			_reservedFunds = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedFund>>(RESERVED_FUNDS_DICTIONARY);

			using var tx = StateManager.CreateTransaction();

			var reservedFundResult = await _reservedFunds.TryGetValueAsync(tx, transactionId);

			if (reservedFundResult.HasValue)
			{
				ReservedFund reservedFund = reservedFundResult.Value;

				var clientResult = await _clients.TryGetValueAsync(tx, reservedFund.ClientId);

				if (clientResult.HasValue)
				{
					Client client = clientResult.Value;

					client.Balance -= reservedFund.Amount;

					await _clients.SetAsync(tx, reservedFund.ClientId, client);

					await _reservedFunds.TryRemoveAsync(tx, transactionId);

					await tx.CommitAsync();
				}
			}
		}
		public async Task<bool> Prepare(Guid transactionId)
		{
			bool isPrepared = false;

			_clients = await StateManager.GetOrAddAsync<IReliableDictionary<string, Client>>(CLIENTS_DICTIONARY);

			_reservedFunds = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedFund>>(RESERVED_FUNDS_DICTIONARY);

			using var tx = StateManager.CreateTransaction();

			var reservedFundResult = await _reservedFunds.TryGetValueAsync(tx, transactionId);

			if (reservedFundResult.HasValue)
			{
				ReservedFund reservedFund = reservedFundResult.Value;

				var clientResult = await _clients.TryGetValueAsync(tx, reservedFund.ClientId);

				if (clientResult.HasValue)
				{
					Client client = clientResult.Value;
					isPrepared = reservedFund.Amount <= client.Balance;
				}
			}

			return isPrepared;
		}

		public async Task Rollback(Guid transactionId)
		{
			_reservedFunds = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedFund>>(RESERVED_FUNDS_DICTIONARY);

			using var tx = StateManager.CreateTransaction();

			await _reservedFunds.TryRemoveAsync(tx, transactionId);

			await tx.CommitAsync();
		}

		public async Task EnlistMoneyTransfer(Guid transactionId, string userID, double amount)
		{
			_reservedFunds = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedFund>>(RESERVED_FUNDS_DICTIONARY);

			using var tx = StateManager.CreateTransaction();

			await _reservedFunds.SetAsync(tx, transactionId, new ReservedFund() { ClientId = userID, Amount = amount});

			await tx.CommitAsync();
		}

		public async Task<Dictionary<string, Client>> ListClients()
		{
			var stateManager = this.StateManager;

			var clients = new Dictionary<string, Client>();

			_clients = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Client>>(CLIENTS_DICTIONARY);

			using (var tx = stateManager.CreateTransaction())
			{
				var enumerator = (await _clients.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

				while (await enumerator.MoveNextAsync(CancellationToken.None))
				{
					clients.Add(enumerator.Current.Key, enumerator.Current.Value);
				}
			}

			return clients;
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
			_clients = await StateManager.GetOrAddAsync<IReliableDictionary<string, Client>>(CLIENTS_DICTIONARY);

			_reservedFunds = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedFund>>(RESERVED_FUNDS_DICTIONARY);

			using var tx = StateManager.CreateTransaction();

			var enumerator = (await _clients.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

			if (!await enumerator.MoveNextAsync(cancellationToken))
			{
				Debug.WriteLine("---Uspesno inicijalizovani podaci!---");
				await _clients.AddAsync(tx, "client1", new Client { ClientName = "Pera", Balance = 20000 });
				await _clients.AddAsync(tx, "client2", new Client { ClientName = "Ana", Balance = 1000 });
			}

			await _reservedFunds.ClearAsync();

			await tx.CommitAsync();
		}
    }
}
