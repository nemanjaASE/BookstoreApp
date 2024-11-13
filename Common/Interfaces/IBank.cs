using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IBank : IService, ITransaction
	{
		Task<Dictionary<string, Client>> ListClients();
		Task EnlistMoneyTransfer(Guid transactionId, string userID, double amount);
	}
}
