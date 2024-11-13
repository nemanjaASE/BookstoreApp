using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IBookstore : IService, ITransaction
	{
		Task<Dictionary<string, Book>> ListAvailableItems();
		Task EnlistPurchase(Guid transactionId, string bookID, uint count);
		Task<double> GetItemPrice(string bookID);
	}
}
