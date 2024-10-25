using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
	public interface IValidation : IService
	{
		Task ValidateBookAsync(string title, int quantity, string client);
	}
}
