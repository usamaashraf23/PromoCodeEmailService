using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
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
            _connectionString = ConfigurationManager.ConnectionStrings["PromoCodeService"]?.ConnectionString
                ?? throw new Exception("Connection string 'PromoCodeService' not found in app.config");
        }

        public async Task<List<PromoCode>> GenerateWeeklyPromoCodes()
        {
            try
            {
                Console.WriteLine("\n=== Generating Weekly Promo Codes ===");

                //await EnsureDatabaseInitialized();

                var codes = new List<PromoCode>();

                // Calculate promo period (Monday to Sunday)
                var today = DateTime.Today;
                var daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                var startDate = today.AddDays(-daysSinceMonday);
                var endDate = startDate.AddDays(6);

                Console.WriteLine($"Promo period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                // 5 different discount levels
                var discounts = new[]
                {
                    0.25m,  // $1.75
                    0.50m,  // $1.50
                    0.75m,  // $1.25
                    1.00m,  // $1.00
                    1.25m   // $0.75
                };

                // deactivate last week's codes
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

                    var promoCode = await CreatePromoCode(discount, startDate, endDate, "Stratus", "PromoCodeEmailService");
                    codes.Add(promoCode);
                }

                Console.WriteLine($"\nSuccessfully generated {codes.Count} promo codes.");
                return codes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GenerateWeeklyPromoCodes: {ex.Message}");
                throw;
            }
        }

        private async Task<PromoCode> CreatePromoCode(
            decimal promoValue,
            DateTime weekStartDate,
            DateTime weekEndDate,
            string createdBy,
            string applicationName)
        {
            var code = await GenerateUniqueCode();

            var promoCode = new PromoCode
            {
                PromoCodeValue = code,
                PromoValue = promoValue,
                WeekStartDate = weekStartDate,
                WeekEndDate = weekEndDate,
                Deleted = false,

                CreatedDate = DateTime.Now,
                CreatedBy = createdBy,
                ModifiedDate = DateTime.Now,
                ModifiedBy = createdBy,
                ApplicationName = applicationName
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

                Console.WriteLine($"Attempt {attempts}: Checking code {code}...");

                isUnique = await CheckCodeUniqueness(code);

                if (attempts >= maxAttempts)
                {
                    throw new Exception($"Failed to generate unique promo code after {maxAttempts} attempts");
                }
            }
            while (!isUnique);

            Console.WriteLine($"Unique code generated: {code}");
            return code;
        }

        private async Task<bool> CheckCodeUniqueness(string code)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = "SELECT COUNT(1) FROM AF_TBL_WEEKLY_PROMO_CODES WHERE PROMO_CODE = @Code";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Code", code);

                        var count = (int)await command.ExecuteScalarAsync();
                        return count == 0;
                    }
                }
            }
            catch (SqlException sqlEx) when (sqlEx.Number == 208)
            {
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking uniqueness: {ex.Message}");
                return false;
            }
        }
        private async Task<long> GetNextPromoId(SqlConnection connection)
        {
            using (var cmd = new SqlCommand("AF_GET_MAX_COLUMN_ID_QRDA", connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@COLUMNNAME", "PROMO_ID");

                var outputParam = new SqlParameter("@OUTVALUE", SqlDbType.BigInt)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(outputParam);

                await cmd.ExecuteNonQueryAsync();
                return (long)outputParam.Value;
            }
        }
        private async Task SavePromoCodeToDatabase(PromoCode promoCode)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    long promoId = await GetNextPromoId(connection);

                    var query = @"
                                INSERT INTO AF_TBL_WEEKLY_PROMO_CODES
                                (
                                    PROMO_ID,
                                    WEEK_START_DATE,
                                    WEEK_END_DATE,
                                    PROMO_CODE,
                                    PROMO_VALUE,
                                    DELETED,
                                    CREATED_DATE,
                                    CREATED_BY,
                                    MODIFIED_DATE,
                                    MODIFIED_BY,
                                    APPLICATION_NAME
                                )
                                VALUES
                                (
                                    @PromoId,
                                    @WeekStartDate,
                                    @WeekEndDate,
                                    @PromoCode,
                                    @PromoValue,
                                    1,
                                    GETDATE(),
                                    @CreatedBy,
                                    GETDATE(),
                                    @ModifiedBy,
                                    @ApplicationName
                                );";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.Add("@PromoId", SqlDbType.BigInt).Value = promoId;
                        command.Parameters.Add("@WeekStartDate", SqlDbType.Date).Value = promoCode.WeekStartDate;
                        command.Parameters.Add("@WeekEndDate", SqlDbType.Date).Value = promoCode.WeekEndDate;
                        command.Parameters.Add("@PromoCode", SqlDbType.VarChar, 50).Value = promoCode.PromoCodeValue;
                        command.Parameters.Add("@PromoValue", SqlDbType.VarChar, 50).Value = promoCode.PromoValue; // or formatted value
                        command.Parameters.Add("@CreatedBy", SqlDbType.VarChar, 100).Value = promoCode.CreatedBy;
                        command.Parameters.Add("@ModifiedBy", SqlDbType.VarChar, 100).Value = promoCode.ModifiedBy;
                        command.Parameters.Add("@ApplicationName", SqlDbType.VarChar, 100).Value = promoCode.ApplicationName;

                        await command.ExecuteNonQueryAsync();
                    }

                    Console.WriteLine($"Saved promo code {promoCode.PromoCodeValue} with ID {promoId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving promo code: {ex.Message}");
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
                                UPDATE AF_TBL_WEEKLY_PROMO_CODES 
                                SET DELETED = 1,
                                    MODIFIED_DATE = GETDATE(),
                                    MODIFIED_BY = 'Stratus'
                                WHERE DELETED = 0 
                                AND WEEK_END_DATE < @Today";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Today", DateTime.Today);
                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"Soft-deleted {rowsAffected} old promo codes");
                        }
                        else
                        {
                            Console.WriteLine("No old promo codes to deactivate");
                        }
                    }
                }
            }
            catch (SqlException sqlEx) when (sqlEx.Number == 208)
            {
                Console.WriteLine("AF_TBL_WEEKLY_PROMO_CODES table doesn't exist yet (first run)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deactivating old promo codes: {ex.Message}");
            }
        }

    }
}