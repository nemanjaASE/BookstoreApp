using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface ITransactionCoordinator : IService
	{
		Task StartTransaction(string title, int quantity, string client);
	}
}
