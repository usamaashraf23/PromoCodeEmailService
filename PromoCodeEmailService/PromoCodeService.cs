using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace PromoCodeEmailService.Services
{
    public class PromoCodeService
    {
        private readonly string _connectionString;
        private const decimal OriginalPrice = 2.00m;
        private static readonly Random _random = new Random();
        private bool _databaseInitialized = false;

        public PromoCodeService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public PromoCodeService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["AppointmentTracking"]?.ConnectionString
                ?? throw new Exception("Connection string 'AppointmentTracking' not found in app.config");
        }

        private async Task EnsureDatabaseInitialized()
        {
            if (!_databaseInitialized)
            {
                await InitializeDatabaseAsync();
                _databaseInitialized = true;
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                Console.WriteLine("Initializing database tables...");

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check and create tables using a transaction
                    var createTablesQuery = @"
                        -- Create PromoCodes table if it doesn't exist
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PromoCodes')
                        BEGIN
                            CREATE TABLE PromoCodes (
                                Id INT IDENTITY(1,1) PRIMARY KEY,
                                Code NVARCHAR(50) NOT NULL UNIQUE,
                                DiscountAmount DECIMAL(5,2) NOT NULL,
                                DiscountedPrice DECIMAL(5,2) NOT NULL,
                                Status NVARCHAR(20) DEFAULT 'Active',
                                CreatedDate DATETIME DEFAULT GETDATE(),
                                ValidFrom DATE NOT NULL,
                                ValidTo DATE NOT NULL,
                                IsUsed BIT DEFAULT 0,
                                UsedDate DATETIME NULL
                            );
                            PRINT 'PromoCodes table created.';
                        END
                        ELSE
                        BEGIN
                            PRINT 'PromoCodes table already exists.';
                        END
                        
                        -- Create EmailLogs table if it doesn't exist
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EmailLogs')
                        BEGIN
                            CREATE TABLE EmailLogs (
                                Id INT IDENTITY(1,1) PRIMARY KEY,
                                SentDate DATETIME DEFAULT GETDATE(),
                                RecipientCount INT NOT NULL,
                                PromoCodeBatchId INT NULL,
                                Status NVARCHAR(20) NOT NULL,
                                ErrorMessage NVARCHAR(500) NULL
                            );
                            PRINT 'EmailLogs table created.';
                        END
                        ELSE
                        BEGIN
                            PRINT 'EmailLogs table already exists.';
                        END";

                    using (var command = new SqlCommand(createTablesQuery, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                        Console.WriteLine("✅ Database tables initialized successfully.");
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"❌ SQL Error initializing database: {sqlEx.Message}");
                Console.WriteLine($"   Error Number: {sqlEx.Number}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initializing database: {ex.Message}");
                throw;
            }
        }

        public async Task<List<PromoCode>> GenerateWeeklyPromoCodes()
        {
            try
            {
                Console.WriteLine("\n=== Generating Weekly Promo Codes ===");

                // Ensure database is initialized first
                await EnsureDatabaseInitialized();

                var codes = new List<PromoCode>();

                // Calculate promo period (Monday to Sunday)
                var today = DateTime.Today;
                var daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                var startDate = today.AddDays(-daysSinceMonday);
                var endDate = startDate.AddDays(6);

                Console.WriteLine($"Promo period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                // Define 5 different discount levels
                var discounts = new[]
                {
                    0.25m,  // $1.75
                    0.50m,  // $1.50
                    0.75m,  // $1.25
                    1.00m,  // $1.00
                    1.25m   // $0.75
                };

                // Try to deactivate last week's codes
                await DeactivateOldPromoCodes();

                // Generate 5 new promo codes
                Console.WriteLine("\nCreating 5 new promo codes...");
                for (int i = 0; i < 5; i++)
                {
                    var discount = discounts[i];
                    var discountedPrice = OriginalPrice - discount;

                    Console.WriteLine($"\nCode {i + 1}:");
                    Console.WriteLine($"  Discount: ${discount:F2}");
                    Console.WriteLine($"  Price: ${discountedPrice:F2}");

                    var promoCode = await CreatePromoCode(discount, startDate, endDate);
                    codes.Add(promoCode);
                }

                Console.WriteLine($"\n✅ Successfully generated {codes.Count} promo codes.");
                return codes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GenerateWeeklyPromoCodes: {ex.Message}");
                throw;
            }
        }

        private async Task<PromoCode> CreatePromoCode(decimal discount, DateTime startDate, DateTime endDate)
        {
            var code = await GenerateUniqueCode();
            var discountedPrice = OriginalPrice - discount;

            var promoCode = new PromoCode
            {
                Code = code,
                DiscountAmount = discount,
                DiscountedPrice = discountedPrice,
                ValidFrom = startDate,
                ValidTo = endDate,
                Status = "Active"
            };

            await SavePromoCodeToDatabase(promoCode);
            return promoCode;
        }

        private async Task<string> GenerateUniqueCode()
        {
            string code;
            bool isUnique;
            int attempts = 0;
            const int maxAttempts = 10;

            do
            {
                code = _random.Next(100000, 999999).ToString();
                attempts++;

                Console.WriteLine($"  Attempt {attempts}: Checking code {code}...");

                isUnique = await CheckCodeUniqueness(code);

                if (attempts >= maxAttempts)
                {
                    throw new Exception($"Failed to generate unique promo code after {maxAttempts} attempts");
                }
            }
            while (!isUnique);

            Console.WriteLine($"  ✅ Unique code generated: {code}");
            return code;
        }

        private async Task<bool> CheckCodeUniqueness(string code)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = "SELECT COUNT(1) FROM PromoCodes WHERE Code = @Code";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Code", code);

                        var count = (int)await command.ExecuteScalarAsync();
                        return count == 0;
                    }
                }
            }
            catch (SqlException sqlEx) when (sqlEx.Number == 208) // Invalid object name
            {
                // Table doesn't exist yet, so code is unique
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Error checking uniqueness: {ex.Message}");
                // For any other error, assume not unique to be safe
                return false;
            }
        }

        private async Task SavePromoCodeToDatabase(PromoCode promoCode)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        INSERT INTO PromoCodes (Code, DiscountAmount, DiscountedPrice, Status, ValidFrom, ValidTo)
                        VALUES (@Code, @DiscountAmount, @DiscountedPrice, @Status, @ValidFrom, @ValidTo)";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Code", promoCode.Code);
                        command.Parameters.AddWithValue("@DiscountAmount", promoCode.DiscountAmount);
                        command.Parameters.AddWithValue("@DiscountedPrice", promoCode.DiscountedPrice);
                        command.Parameters.AddWithValue("@Status", promoCode.Status);
                        command.Parameters.AddWithValue("@ValidFrom", promoCode.ValidFrom);
                        command.Parameters.AddWithValue("@ValidTo", promoCode.ValidTo);

                        await command.ExecuteNonQueryAsync();
                        Console.WriteLine($"  💾 Saved to database: {promoCode.Code}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error saving promo code: {ex.Message}");
                throw;
            }
        }

        private async Task DeactivateOldPromoCodes()
        {
            try
            {
                Console.WriteLine("Deactivating old promo codes...");

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        UPDATE PromoCodes 
                        SET Status = 'Inactive'
                        WHERE Status = 'Active' 
                        AND ValidTo < @Today";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Today", DateTime.Today);
                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"  🔄 Deactivated {rowsAffected} old promo codes");
                        }
                        else
                        {
                            Console.WriteLine("  ℹ No old promo codes to deactivate");
                        }
                    }
                }
            }
            catch (SqlException sqlEx) when (sqlEx.Number == 208) // Invalid object name
            {
                // Table doesn't exist yet, that's okay - it's the first run
                Console.WriteLine("  ℹ PromoCodes table doesn't exist yet (first run)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Error deactivating old promo codes: {ex.Message}");
                // Don't throw - this is not critical
            }
        }
    }
}