using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
using PromoCodeEmailService.Services;

namespace PromoCodeEmailService
{
    public partial class PromoCodeEmailService : ServiceBase
    {
        private System.Timers.Timer _weeklyTimer;
        private PromoCodeService _promoCodeService;
        private EmailService _emailService;
        private readonly EventLog _eventLog;
        private bool _isRunning = false;
        private string _connectionString;
        private List<string> _recipients;

        public PromoCodeEmailService()
        {
            InitializeComponent();

            _eventLog = new EventLog();
            _eventLog.Source = "PromoCodeEmailService";
            _eventLog.Log = "Application";
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                _eventLog.WriteEntry("Promo Code Email Service is starting...", EventLogEntryType.Information);

                // Load configuration
                LoadConfiguration();

                // Initialize services
                InitializeServices();

                // Setup the weekly timer
                SetupWeeklyTimer();

                _isRunning = true;
                _eventLog.WriteEntry("Promo Code Email Service started successfully.", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry($"Error starting service: {ex.Message}\n{ex.StackTrace}", EventLogEntryType.Error);
                throw;
            }
        }

        protected override void OnStop()
        {
            try
            {
                _eventLog.WriteEntry("Promo Code Email Service is stopping...", EventLogEntryType.Information);

                _isRunning = false;

                // Stop and dispose the timer
                if (_weeklyTimer != null)
                {
                    _weeklyTimer.Stop();
                    _weeklyTimer.Dispose();
                }

                _eventLog.WriteEntry("Promo Code Email Service stopped successfully.", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry($"Error stopping service: {ex.Message}", EventLogEntryType.Error);
            }
        }

        protected override void OnPause()
        {
            if (_weeklyTimer != null)
            {
                _weeklyTimer.Stop();
            }
            _isRunning = false;
            _eventLog.WriteEntry("Service paused.", EventLogEntryType.Information);
        }

        protected override void OnContinue()
        {
            if (_weeklyTimer != null)
            {
                _weeklyTimer.Start();
            }
            _isRunning = true;
            _eventLog.WriteEntry("Service continued.", EventLogEntryType.Information);
        }

        protected override void OnShutdown()
        {
            OnStop();
            base.OnShutdown();
        }

        private void LoadConfiguration()
        {
            try
            {
                // Get connection string from app.config
                _connectionString = ConfigurationManager.ConnectionStrings["PromoCodeService"]?.ConnectionString;

                if (string.IsNullOrEmpty(_connectionString))
                {
                    throw new Exception("Connection string 'PromoCodeService' not found in app.config");
                }

                // Get recipients from app.config
                var recipientsValue = ConfigurationManager.AppSettings["MailTo"];
                if (!string.IsNullOrEmpty(recipientsValue))
                {
                    _recipients = recipientsValue.Split(';')
                        .Select(email => email.Trim())
                        .Where(email => !string.IsNullOrEmpty(email))
                        .ToList();
                }
                else
                {
                    _recipients = new List<string>();
                    _eventLog.WriteEntry("No recipients configured in MailTo setting", EventLogEntryType.Warning);
                }

                _eventLog.WriteEntry("Configuration loaded successfully.", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry($"Error loading configuration: {ex.Message}", EventLogEntryType.Error);
                throw;
            }
        }

        private void InitializeServices()
        {
            try
            {
                // Initialize services
                _promoCodeService = new PromoCodeService(_connectionString);
                _emailService = new EmailService();

                // Test database connection
                var connectionOk = TestDatabaseConnection();
                if (!connectionOk)
                {
                    _eventLog.WriteEntry("Cannot connect to database. Service may not function properly.",
                        EventLogEntryType.Warning);
                }
                else
                {
                    _eventLog.WriteEntry("Database initialized successfully.", EventLogEntryType.Information);
                }

                _eventLog.WriteEntry("Services initialized successfully.", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry($"Error initializing services: {ex.Message}\n{ex.StackTrace}",
                    EventLogEntryType.Error);
                throw;
            }
        }

        private bool TestDatabaseConnection()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry($"Database connection test failed: {ex.Message}", EventLogEntryType.Error);
                return false;
            }
        }
        private void SetupWeeklyTimer()
        {
            try
            {
                _weeklyTimer = new System.Timers.Timer();
                _weeklyTimer.Elapsed += OnWeeklyTimerElapsed;
                _weeklyTimer.AutoReset = true;

                // Calculate next Monday at 9:00 AM
                var nextRunTime = GetNextMondayAt9AM();
                var timeUntilFirstRun = nextRunTime - DateTime.Now;

                _eventLog.WriteEntry($"Next email will be sent on: {nextRunTime}", EventLogEntryType.Information);

                // Set timer interval to 7 days (weekly)
                _weeklyTimer.Interval = TimeSpan.FromDays(7).TotalMilliseconds;

                // Start the timer
                _weeklyTimer.Start();

                // Also trigger immediately if we want to test or if it's Monday morning
                if (timeUntilFirstRun.TotalHours < 1)
                {
                    Task.Run(() => ExecuteWeeklyTask());
                }
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry($"Error setting up timer: {ex.Message}", EventLogEntryType.Error);
                throw;
            }
        }

        private DateTime GetNextMondayAt9AM()
        {
            var today = DateTime.Today;
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;

            // If today is Monday and before 9:00 AM, send today
            if (daysUntilMonday == 0 && DateTime.Now.TimeOfDay < new TimeSpan(9, 0, 0))
            {
                return today.Add(new TimeSpan(9, 0, 0));
            }

            var nextMonday = today.AddDays(daysUntilMonday);
            return nextMonday.Add(new TimeSpan(9, 0, 0));
        }

        private async void OnWeeklyTimerElapsed(object sender, ElapsedEventArgs e)
        {
            await ExecuteWeeklyTask();
        }

        private async Task ExecuteWeeklyTask()
        {
            if (!_isRunning) return;

            try
            {
                _eventLog.WriteEntry("Starting weekly promo code generation and email sending...",
                    EventLogEntryType.Information);

                // Step 1: Generate new promo codes
                var promoCodes = await _promoCodeService.GenerateWeeklyPromoCodes();

                if (promoCodes.Count == 0)
                {
                    _eventLog.WriteEntry("No promo codes were generated", EventLogEntryType.Warning);
                    return;
                }

                // Log generated codes
                var codesLog = string.Join(", ", promoCodes.Select(p => $"{p.Code} (${p.DiscountAmount:F2})"));
                _eventLog.WriteEntry($"Generated {promoCodes.Count} promo codes: {codesLog}",
                    EventLogEntryType.Information);

                // Step 2: Send email with generated codes using your EmailService
                await _emailService.SendWeeklyPromoCodeEmail(promoCodes);

                // Log the email sending
                LogEmailToDatabase(_recipients.Count, promoCodes.Count, true);

                _eventLog.WriteEntry($"Weekly promo code email sent to {_recipients.Count} recipients",
                    EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry($"Error executing weekly task: {ex.Message}\n{ex.StackTrace}",
                    EventLogEntryType.Error);

                // Log error to database
                LogEmailToDatabase(0, 0, false, ex.Message);
            }
        }

        private void LogEmailToDatabase(int recipientCount, int promoCodeCount, bool isSuccess, string errorMessage = null)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    var query = @"
                        INSERT INTO EmailLogs (SentDate, RecipientCount, PromoCodeBatchId, Status, ErrorMessage)
                        VALUES (@SentDate, @RecipientCount, @PromoCodeBatchId, @Status, @ErrorMessage)";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SentDate", DateTime.Now);
                        command.Parameters.AddWithValue("@RecipientCount", recipientCount);
                        command.Parameters.AddWithValue("@PromoCodeBatchId", DBNull.Value);
                        command.Parameters.AddWithValue("@Status", isSuccess ? "Success" : "Failed");
                        command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? (object)DBNull.Value);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry($"Error logging email to database: {ex.Message}", EventLogEntryType.Warning);
            }
        }

        // Public method for manual execution
        public async Task ExecuteManually()
        {
            try
            {
                _eventLog.WriteEntry("Manual execution triggered", EventLogEntryType.Information);
                await ExecuteWeeklyTask();
                _eventLog.WriteEntry("Manual execution completed", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                _eventLog.WriteEntry($"Error in manual execution: {ex.Message}", EventLogEntryType.Error);
                throw;
            }
        }
    }
}