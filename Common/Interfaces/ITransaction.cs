using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface ITransaction : IService
	{
		Task<bool> Prepare(Guid transactionId);
		Task Commit(Guid transactionId);
		Task Rollback(Guid transactionId);
	}
}
