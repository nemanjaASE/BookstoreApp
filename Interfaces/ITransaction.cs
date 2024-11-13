using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface ITransaction : IService
    {
        Task<bool> Prepare(Guid transactionId);
        Task Commit(Guid transactionId);
        Task Rollback(Guid transactionId);
    }
}
