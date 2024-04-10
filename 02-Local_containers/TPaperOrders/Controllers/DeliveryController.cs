using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TPaperOrders
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeliveryController
    {
        public DeliveryController()
        {
        }

        [HttpGet]
        [Route("create/{clientId}/{ediOrderId}/{productCode}/{number}")]
        public async Task<IActionResult> ProcessEdiOrder(
            int clientId,
            int ediOrderId,
            int productCode,
            int number,
            CancellationToken cts)
        {

            var newDelivery = new Delivery
            {
                Id = 0,
                ClientId = clientId,
                EdiOrderId = ediOrderId,
                Number = number,
                ProductId = 1,
                ProductCode = 1,
                Notes = "Prepared for shipment"
            };

            return new OkObjectResult(newDelivery);
        }
    }
}
