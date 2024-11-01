using Microsoft.ServiceFabric.Data.Collections;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BankService
{
	internal static class ClientHelper
	{
		public static async Task<Client> getClientById(Microsoft.ServiceFabric.Data.ITransaction tx, IReliableDictionary<string, Client> clients, string clientId)
		{
			var client = await clients.TryGetValueAsync(tx, clientId);

			if (!client.HasValue)
			{
				throw new ArgumentException($"Client with {clientId} doesn't exists!");
			}

			return client.Value;
		}
	}
}
