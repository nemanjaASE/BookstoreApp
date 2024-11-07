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
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Models;

namespace BankService
{

    internal sealed class BankService : StatefulService, IBank
    {
		private const string CLIENTS_DICTIONARY = "accounts";
		private const string RESERVED_FUNDS_DICTIONARY = "reserved_funds";

		private IReliableDictionary<string, Client> _clients;
		private IReliableDictionary<string, double> _reservedFunds;
		public BankService(StatefulServiceContext context)
            : base(context)
        { }

		public async Task Commit()
		{
			var stateManager = this.StateManager;

			_clients = await stateManager.GetOrAddAsync<IReliableDictionary<string, Client>>(CLIENTS_DICTIONARY);
			_reservedFunds = await stateManager.GetOrAddAsync<IReliableDictionary<string, double>>(RESERVED_FUNDS_DICTIONARY);

			using (var tx = stateManager.CreateTransaction())
			{
				var reservedClientEnumerator = (await _reservedFunds.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

				while (await reservedClientEnumerator.MoveNextAsync(CancellationToken.None))
				{
					string clientId = reservedClientEnumerator.Current.Key;
					double reservedAmount = reservedClientEnumerator.Current.Value;

					Client client = await ClientHelper.getClientById(tx, _clients, clientId);

					client.Balance -= reservedAmount;

					await _clients.SetAsync(tx, reservedClientEnumerator.Current.Key, client);
				}

				await _reservedFunds.ClearAsync();

				await tx.CommitAsync();
			}
		}
		public async Task<bool> Prepare()
		{
			var stateManager = this.StateManager;

			_clients = await stateManager.GetOrAddAsync<IReliableDictionary<string, Client>>(CLIENTS_DICTIONARY);
			_reservedFunds = await stateManager.GetOrAddAsync<IReliableDictionary<string, double>>(RESERVED_FUNDS_DICTIONARY);

			using (var tx = stateManager.CreateTransaction())
			{
				var reservedClientEnumerator = (await _reservedFunds.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

				while (await reservedClientEnumerator.MoveNextAsync(CancellationToken.None))
				{
					string clientId = reservedClientEnumerator.Current.Key;

					Client client = await ClientHelper.getClientById(tx, _clients, clientId);

					if (reservedClientEnumerator.Current.Value > client.Balance)
					{
						return false;
					}
				}

				return true;
			}
		}

		public async Task Rollback()
		{
			var stateManager = this.StateManager;

			_reservedFunds = await stateManager.GetOrAddAsync<IReliableDictionary<string, double>>(RESERVED_FUNDS_DICTIONARY);

			using (var tx = stateManager.CreateTransaction())
			{
				await _reservedFunds.ClearAsync();
				await tx.CommitAsync();
			}
		}

		public async Task EnlistMoneyTransfer(string userID, double amount)
		{
			var stateManeger = this.StateManager;
			_reservedFunds = await stateManeger.GetOrAddAsync<IReliableDictionary<string, double>>(RESERVED_FUNDS_DICTIONARY);

			using (var tx = stateManeger.CreateTransaction())
			{
				var reservedFunds = await _reservedFunds.TryGetValueAsync(tx, userID);
				double newReservedFunds = reservedFunds.HasValue ? reservedFunds.Value + amount : amount;

				await _reservedFunds.SetAsync(tx, userID, newReservedFunds);

				await tx.CommitAsync();
			}
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
			_clients = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Client>>(CLIENTS_DICTIONARY);
			_reservedFunds = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, double>>(RESERVED_FUNDS_DICTIONARY);

			using (var tx = this.StateManager.CreateTransaction())
			{
				var enumerator = (await _clients.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
				if (!await enumerator.MoveNextAsync(cancellationToken))
				{
					Debug.WriteLine("---Uspesno inicijalizovani podaci!---");
					await _clients.AddAsync(tx, "client1", new Client { ClientName = "Pera", Balance = 20000});
					await _clients.AddAsync(tx, "client2", new Client { ClientName = "Ana", Balance = 1000});
				}

				await _reservedFunds.ClearAsync();

				await tx.CommitAsync();
			}
		}
    }
}
