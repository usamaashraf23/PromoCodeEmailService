using System;
using System.Configuration;
using System.Threading.Tasks;
using PromoCodeEmailService.Services;

namespace PromoCodeEmailService
{
    internal static class Program
    {
        static async Task Main()
        {
            try
            {
                #if DEBUG
                System.Diagnostics.Debugger.Launch();
                #endif

                Console.WriteLine("Starting Promo Code Email Service...");

                // Get connection string
                string connectionString = ConfigurationManager.ConnectionStrings["PromoCodeService"]?.ConnectionString;

                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("ERROR: Connection string not found");
                    return;
                }

                // Create services
                var promoCodeService = new PromoCodeService(connectionString);
                var emailService = new EmailService();

                // Generate promo codes
                Console.WriteLine("Generating promo codes...");
                var promoCodes = await promoCodeService.GenerateWeeklyPromoCodes();

                Console.WriteLine($"Generated {promoCodes.Count} codes:");
                foreach (var code in promoCodes)
                {
                    Console.WriteLine($"  {code.Code} - {code.FormattedPrice} (Discount: {code.FormattedDiscount})");
                }

                // Send email
                Console.WriteLine("Sending email...");
                await emailService.SendWeeklyPromoCodeEmail(promoCodes);
                Console.WriteLine("Email sent successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nPress any key to exit...");
        }
    }
}