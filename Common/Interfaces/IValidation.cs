using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IValidation : IService
	{
		Task ValidateBookAsync(string title, int quantity, string client);
	}
}
