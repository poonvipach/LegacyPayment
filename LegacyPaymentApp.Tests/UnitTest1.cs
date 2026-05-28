using Xunit;
using LegacyPaymentApp;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;

namespace LegacyPaymentApp.Tests
{
    public class PaymentServiceTests
    {
        [Fact]
        public void ProcessPayment_WithValidStrategy_ShouldExecuteWithoutErrors()
        {
            // Arrange
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var realApi = new ExternalExchangeRateApi();
            var cachedService = new CachedExchangeRateService(realApi, memoryCache);

            var strategies = new List<IPaymentStrategy>
            {
                new CreditCardPaymentStrategy()
            };

            var service = new PaymentService(cachedService, strategies);

            // Act
            var exception = Record.Exception(() => service.ProcessPayment("CreditCard", 100, "THB"));

            // Assert
            Assert.Null(exception); // The test passes if no exceptions are thrown
        }
    }
}
