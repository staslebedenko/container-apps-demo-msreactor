using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace TPaperOrders
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController
    {
        private readonly ILogger<OrderController> _logger;

        private readonly IHttpClientFactory _clientFactory;

        public OrderController(ILogger<OrderController> logger, IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _clientFactory = clientFactory;
        }

        [HttpGet]
        [Route("create/{quantity}")]
        public async Task<IActionResult> ProcessEdiOrder(decimal quantity, CancellationToken cts)
        {
            _logger.LogInformation("Processed a request.");

            var order = new EdiOrder
            {
                Id = 1,
                ClientId = 1,
                DeliveryId = 1,
                Notes = "Test order",
                ProductCode = 1,
                Quantity = quantity
            };


            Delivery savedDelivery = await CreateDeliveryForOrder(order, cts);

            string responseMessage = $"Accepted EDI message {order.Id} and created delivery {order.Id}";

            return new OkObjectResult(responseMessage);
        }

        private async Task<Delivery> CreateDeliveryForOrder(EdiOrder savedOrder, CancellationToken cts)
        {
            string url =
                $"http://tpaperorders:80/api/delivery/create/{savedOrder.ClientId}/{savedOrder.Id}/{savedOrder.ProductCode}/{savedOrder.Quantity}";

            using var httpClient = _clientFactory.CreateClient();
            var uriBuilder = new UriBuilder(url);

            using var result = await httpClient.GetAsync(uriBuilder.Uri);
            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await result.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<Delivery>(content);
        }
    }
}
