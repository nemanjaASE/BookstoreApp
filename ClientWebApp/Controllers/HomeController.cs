using ClientWebApp.Models;
using Interfaces;
using ValidationService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.ServiceFabric.Services.Communication;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;

namespace ClientWebApp.Controllers
{
	public class HomeController : Controller
	{
        private readonly IValidation _validationService;

		public HomeController()
		{
            var serviceProxyFactory = new ServiceProxyFactory((callbackClient) =>
            {
                return new FabricTransportServiceRemotingClientFactory(
                    new FabricTransportRemotingSettings
                    {
                        ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
                    },
                    callbackClient);
            });

            var serviceUri = new Uri("fabric:/BookstoreApp/ValidationService");
            _validationService = serviceProxyFactory.CreateServiceProxy<IValidation>(serviceUri);
        }

		public IActionResult Index()
		{
			return View();
		}

        [HttpPost]
        public async Task<ActionResult> Order(string title, int quantity, string client)
        {
            try
            {
                await _validationService.ValidateBookAsync(title, quantity, client);
            }
            catch (AggregateException ex)
            {
                foreach (var e in ex.InnerExceptions)
                {
                    if (e is ArgumentException)
                    {
                        ViewBag.ErrorMessage = e.Message;

                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Something went wrong.";
            }

            return View("Index");
        }

        public IActionResult Privacy()
		{
			return View();
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}
