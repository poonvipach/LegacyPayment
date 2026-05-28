using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Collections.Generic;

namespace LegacyPaymentApp
{
    public interface IPaymentStrategy
    {
        string PaymentType { get; }
        void Process(decimal amountInUsd);
    }

    public class CreditCardPaymentStrategy : IPaymentStrategy
    {
        public string PaymentType => "CreditCard";

        public void Process(decimal amountInUsd)
        {
            Console.WriteLine($"Processing Credit Card payment of ${amountInUsd}");
        }
    }

    public class PayPalPaymentStrategy : IPaymentStrategy
    {
        public string PaymentType => "PayPal";

        public void Process(decimal amountInUsd)
        {
            Console.WriteLine($"Processing PayPal payment of ${amountInUsd}");
        }
    }

    /* public class PaymentService
    {
        private readonly IExchangeRateService _exchangeRateService;

        public PaymentService(IExchangeRateService exchangeRateService)
        {
            _exchangeRateService = exchangeRateService;
        }

        public void ProcessPayment(string paymentType, decimal amount, string currency)
        {
            // 1. ดึงอัตราแลกเปลี่ยน (ช้ามาก)
            var api = new ExternalExchangeRateApi();
            decimal rate = api.GetRate(currency);
            decimal amountInUsd = amount * rate;

            // 2. ประมวลผลตามประเภทการจ่ายเงิน
            if (paymentType == "CreditCard")
            {
                Console.WriteLine($"Processing Credit Card payment of ${amountInUsd}");
            }
            else if (paymentType == "PayPal")
            {
                Console.WriteLine($"Processing PayPal payment of ${amountInUsd}");
            }
            else
            {
                throw new NotSupportedException("Payment method not supported");
            }

            // 3. บันทึก Log ลงระบบ
            File.WriteAllText("log.txt", $"Processed {paymentType} for ${amountInUsd} at {DateTime.Now}");
        }
    }*/

    public class PaymentService
    {
        private readonly IExchangeRateService _exchangeRateService;
        // เปลี่ยนมารับคอลเลกชันของ Strategy ทั้งหมดที่มีในระบบ
        private readonly IEnumerable<IPaymentStrategy> _strategies;

        public PaymentService(IExchangeRateService exchangeRateService, IEnumerable<IPaymentStrategy> strategies)
        {
            _exchangeRateService = exchangeRateService;
            _strategies = strategies;
        }

        public void ProcessPayment(string paymentType, decimal amount, string currency)
        {
            decimal rate = _exchangeRateService.GetRate(currency);
            decimal amountInUsd = amount * rate;

            // ค้นหา Strategy ที่มีค่า PaymentType ตรงกับที่ส่งเข้ามา
            var strategy = _strategies.FirstOrDefault(s => s.PaymentType.Equals(paymentType, StringComparison.OrdinalIgnoreCase));

            if (strategy == null)
            {
                throw new NotSupportedException($"Payment method '{paymentType}' not supported");
            }

            // สั่งรันโลจิกเฉพาะของคลาสนั้นๆ ทันทีโดยไม่ต้องมี if-else
            strategy.Process(amountInUsd);

            File.WriteAllText("log.txt", $"Processed {paymentType} for ${amountInUsd} at {DateTime.Now}");
        }
    }

    public interface IExchangeRateService
    {
        decimal GetRate(string currency);
    }

    public class ExternalExchangeRateApi : IExchangeRateService
    {
        public decimal GetRate(string currency)
        {
            Thread.Sleep(2000); // จำลองว่า API ทำงานช้ามาก
            if (currency == "THB") return 0.03m;
            return 1.0m;
        }
    }

    public class CachedExchangeRateService : IExchangeRateService
    {
        private readonly IExchangeRateService _innerService;
        private readonly IMemoryCache _cache;
        public CachedExchangeRateService(IExchangeRateService innerService, IMemoryCache cache)
        {
            _innerService = innerService;
            _cache = cache;
        }

        // 2. เพิ่มฟังก์ชันประมวลผลดึงค่า/เก็บแคช เข้าไปตรงนี้
        public decimal GetRate(string currency)
        {
            string cacheKey = $"rate_{currency}";

            if (!_cache.TryGetValue(cacheKey, out decimal cachedRate))
            {
                cachedRate = _innerService.GetRate(currency);

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

                _cache.Set(cacheKey, cachedRate, cacheOptions);
            }

            return cachedRate;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            // 1. สร้างก้อนระบบ Memory Cache เริ่มต้นขึ้นมา
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            // 2. สร้าง API ตัวจริง
            var realApi = new ExternalExchangeRateApi();
            // 3. เอาคลาสแคชครอบ API ตัวจริงไว้ (Decorator Pattern)
            var cachedService = new CachedExchangeRateService(realApi, memoryCache);
            
            // 4. ส่งผ่านให้ PaymentService เรียกใช้งานผ่าน Interface
            //var service = new PaymentService(cachedService);
            
            // สร้างรายการกลยุทธ์การชำระเงินที่ระบบเรายอมรับในตอนนี้
            var strategies = new List<IPaymentStrategy>
            {
                new CreditCardPaymentStrategy(),
                new PayPalPaymentStrategy()
            };       
            // ส่งชุดข้อมูลเข้า PaymentService
            var service = new PaymentService(cachedService, strategies);
    
            Console.WriteLine("--- เริ่มทดสอบระบบชำระเงินแบบมีแคช ---");
            // รายการที่ 1: จะช้า 2 วินาที (เพราะดึงครั้งแรกเข้า Cache)
            Console.WriteLine($"\n [{DateTime.Now:HH:mm:ss}] กำลังประมวลผลชิ้นที่ 1...");
            service.ProcessPayment("CreditCard", 1000, "THB");
            // รายการที่ 2: สกุลเงินเดิม "THB" ต้องเร็วทันทีแบบไม่ค้างเลย!
            Console.WriteLine($"\n [{DateTime.Now:HH:mm:ss}] กำลังประมวลผลชิ้นที่ 2 (สกุลเงินเดิม)...");
            service.ProcessPayment("PayPal", 500, "THB");
            Console.WriteLine("\n--- จบการทดสอบ ---");
        }
    }
}
