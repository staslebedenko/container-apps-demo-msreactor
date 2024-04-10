using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TPaperOrders
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController
    {
        private readonly PaperDbContext _context;

        private readonly ILogger<OrderController> _logger;

        private readonly DaprClient _daprClient;

        public OrderController(
            PaperDbContext context,
            ILogger<OrderController> logger,
            DaprClient daprClient)
        {
            _context = context;
            _logger = logger;
            _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
        }

        [HttpGet]
        [Route("create/{quantity}")]
        public async Task<IActionResult> ProcessEdiOrder(decimal quantity, CancellationToken cts)
        {
            _logger.LogInformation("Processed a request.");

            var order = new EdiOrder
            {
                ClientId = 1,
                DeliveryId = 1,
                Notes = "Test order",
                ProductCode = 1,
                Quantity = quantity
            };

            EdiOrder savedOrder = (await _context.EdiOrder.AddAsync(order, cts)).Entity;
            await _context.SaveChangesAsync(cts);

            DeliveryModel savedDelivery = await CreateDeliveryForOrder(savedOrder, cts);

            string responseMessage = $"Accepted EDI message {order.Id} and created delivery {savedDelivery?.Id}";

            var test = responseMessage;

            return new OkObjectResult(responseMessage);
        }

        private async Task<DeliveryModel> CreateDeliveryForOrder(EdiOrder savedOrder, CancellationToken cts)
        {
            var newDelivery = new DeliveryModel
            {
                Id = 0,
                ClientId = savedOrder.ClientId,
                EdiOrderId = savedOrder.Id,
                Number = savedOrder.Quantity,
                ProductId = 0,
                ProductCode = savedOrder.ProductCode,
                Notes = "Prepared for shipment"
            };

            await _daprClient.PublishEventAsync<DeliveryModel>("pubsub-super-new", "aksdelivery", newDelivery, cts);

            return newDelivery;
        }

        [HttpGet]
        [Route("health")]
        public async Task<IActionResult> Health(CancellationToken cts)
        {
            return new OkObjectResult("Started");
        }

        // create unit tests for the following class #file:OrderController.cs
        // please refactor only method ProcessEdiOrder to lower cyclomatic complexity 
        // I need a new method that will receive a post message with DeliveryModel and save it to the database
    }
}
