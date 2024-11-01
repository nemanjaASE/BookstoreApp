using Microsoft.ServiceFabric.Services.Remoting;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface IBookstore : IService, ITransaction
    {
        Task<Dictionary<string, Book>> ListAvailableItems();
        Task EnlistPurchase(string bookID, uint count);
        Task<double> GetItemPrice(string bookID);
    }
}
