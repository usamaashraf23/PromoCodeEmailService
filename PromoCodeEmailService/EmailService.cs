using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace PromoCodeEmailService.Services
{
    public class EmailService
    {
        public async Task SendEmail(List<string> mailTo, Email objEmail, List<string> listBCC = null, string filePath = null)
        {
            try
            {
                string host = ConfigurationManager.AppSettings["SmtpHost"];
                string userName = ConfigurationManager.AppSettings["SmtpUserName"];
                string password = ConfigurationManager.AppSettings["SmtpPassword"];
                int port = Convert.ToInt32(ConfigurationManager.AppSettings["SmtpPort"]);
                SmtpClient client = new SmtpClient(host)
                {
                    UseDefaultCredentials = true,
                    Credentials = new NetworkCredential(userName,password),
                    Port = port,
                    EnableSsl = true
                };

                MailMessage msg = new MailMessage
                {
                    From = new MailAddress(userName),
                    Subject = objEmail.subject,
                    Body = objEmail.body,
                    IsBodyHtml = true,
                    Priority = MailPriority.High
                };

                foreach (var to in mailTo)
                {
                    msg.To.Add(to);
                }

                if (listBCC != null && listBCC.Any())
                {
                    foreach (var bcc in listBCC)
                    {
                        msg.Bcc.Add(bcc);
                    }
                }

                await client.SendMailAsync(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        public async Task SendWeeklyPromoCodeEmail(List<PromoCode> promoCodes)
        {
            try
            {
                var startDate = promoCodes.FirstOrDefault()?.WeekStartDate ?? DateTime.Today;
                var endDate = promoCodes.FirstOrDefault()?.WeekEndDate ?? DateTime.Today.AddDays(6);

                var emailBody = GenerateEmailBody(promoCodes, startDate, endDate);
                var emailSubject = $"Weekly Promo Codes for StartusAI Feature! ({startDate:dd MMM} - {endDate:dd MMM})";

                string userEmail = ConfigurationManager.AppSettings["MailTo"];
                List<string> listUser = userEmail.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                      .Select(email => email.Trim())
                                                      .ToList();

                var bccEmailsString = ConfigurationManager.AppSettings["MailBCC"];
                List<string> listBCC = bccEmailsString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                      .Select(email => email.Trim())
                                                      .ToList();

                Email objEmail = new Email
                {
                    body = emailBody,
                    messageTo = string.Join(", ", listUser),
                    subject = emailSubject
                };
                await SendEmail(listUser, objEmail, listBCC);

                Console.WriteLine($"Weekly promo code email sent to {listUser.Count} recipients");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        private string GenerateEmailBody(List<PromoCode> promoCodes, DateTime startDate, DateTime endDate)
        {
            var startDateStr = startDate.ToString("dd MMM");
            var endDateStr = endDate.ToString("dd MMM");
            var startDateFull = startDate.ToString("MMMM dd, yyyy");
            var endDateFull = endDate.ToString("MMMM dd, yyyy");

            var html = $@"
                    <html>
                    <body style=""font-family: Arial, sans-serif; font-size: 14px; color: #000000; margin: 0; padding: 0;"">
                    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color: #f5f5f5;"">
                    <tr>
                    <td align=""center"">
                        <!-- Main container -->
                        <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color: #ffffff; border: 1px solid #cccccc; font-family: Arial, sans-serif;"">
    
                        <!-- Header -->
                        <tr>
                            <td bgcolor=""#2c5aa0"" align=""center"" style=""padding: 15px; border-bottom: 3px solid #1a3d7a;"">
                                <div style=""font-family: Arial, sans-serif; color: #ffffff; font-size: 18px; font-weight: bold;"">
                                    Weekly Promo Codes
                                </div>
                                <div style=""font-family: Arial, sans-serif; color: #ffffff; font-size: 14px; margin-top: 5px;"">
                                    {startDateFull} - {endDateFull}
                                </div>
                            </td>
                        </tr>
    
                        <!-- Content -->
                        <tr>
                            <td style=""padding: 20px;"">
                                <p style=""margin-top: 0; margin-bottom: 10px; font-family: Arial, sans-serif;"">Dear Team,</p>
            
                                <p style=""margin-bottom: 10px; font-family: Arial, sans-serif;"">Below are the promo codes for the StartusAI feature this week. Please share these with our customers and encourage them to take advantage of these discounts.</p>
            
                                <p style=""margin-bottom: 15px; font-family: Arial, sans-serif;"">
                                    <strong>Original Price:</strong> 
                                    <span style=""font-weight: bold;"">$2.00</span>
                                </p>
            
                                <!-- Section title -->
                                <div style=""font-family: Arial, sans-serif; font-size: 16px; font-weight: bold; border-bottom: 2px solid #2c5aa0; padding-bottom: 5px; margin-bottom: 10px;"">
                                    Promo Code Details
                                </div>
            
                                <!-- Promo codes table -->
                                <table width=""100%"" cellpadding=""4"" cellspacing=""0"" border=""1"" bordercolor=""#dddddd"" style=""border-collapse: collapse; margin-bottom: 15px; font-family: Arial, sans-serif;"">
                                <tr bgcolor=""#f0f0f0"">
                                    <th width=""10%"" align=""left"" style=""padding: 6px; border: 1px solid #dddddd;"">#</th>
                                    <th width=""35%"" align=""left"" style=""padding: 6px; border: 1px solid #dddddd;"">Discounted Price</th>
                                    <th width=""25%"" align=""left"" style=""padding: 6px; border: 1px solid #dddddd;"">Discount</th>
                                    <th width=""30%"" align=""left"" style=""padding: 6px; border: 1px solid #dddddd;"">Promo Code</th>
                                </tr>";

                                for (int i = 0; i < promoCodes.Count; i++)
                                {
                                    var promo = promoCodes[i];
                                    html += $@"
                                <tr>
                                    <td style=""padding: 6px; border: 1px solid #dddddd; font-family: Arial, sans-serif;"">{i + 1}</td>
                                    <td style=""padding: 6px; border: 1px solid #dddddd; font-family: Arial, sans-serif;""><strong>{promo.FormattedPrice}</strong></td>
                                    <td style=""padding: 6px; border: 1px solid #dddddd; color: #008000; font-family: Arial, sans-serif;""><strong>{promo.FormattedDiscount} off</strong></td>
                                    <td style=""padding: 6px; border: 1px solid #dddddd; color: #2c5aa0; font-size: 15px; font-family: Arial, sans-serif;""><strong>{promo.Code}</strong></td>
                                </tr>";
                                }

                                html += $@"
                                </table>
            
                                <!-- Important Notes -->
                                <div style=""font-family: Arial, sans-serif; background-color: #f8f9fa; border-left: 4px solid #2c5aa0; padding: 12px; margin-bottom: 15px;"">
                                    <div style=""font-weight: bold; margin-bottom: 5px;"">Important Notes:</div>
                                    <div style=""padding-left: 10px;"">
                                        • Valid from <strong>{startDateFull}</strong> to <strong>{endDateFull}</strong><br>
                                        • Each code can be used only once per customer<br>
                                        • Codes expire automatically at the end of the validity period
                                    </div>
                                </div>
            
                                <!-- Action Required -->
                                <p style=""margin-bottom: 10px; font-family: Arial, sans-serif;"">
                                    <strong>Action Required:</strong> Please use these promo codes when assisting customers with purchases for the StartusAI feature.
                                </p>
            
                                <p style=""margin-bottom: 15px; font-family: Arial, sans-serif;"">If you have any questions or need assistance, please reach out.</p>
            
                                <!-- Footer -->
                                <div style=""font-family: Arial, sans-serif; border-top: 1px solid #dddddd; padding-top: 15px;"">
                                    <p style=""margin-bottom: 5px;"">Best regards,<br>
                                    <strong>CareCloud Team</strong></p>
                
                                    <p style=""margin-top: 10px; color: #666666; font-size: 12px;"">
                                        This is an automated email. Please do not reply directly to this message.<br>
                                        For support, contact: support@carecloud.com
                                    </p>
                                </div>
                            </td>
                        </tr>
                        </table>
                    </td>
                    </tr>
                    </table>
                    </body>
                    </html>";

            return html;
        }

    }
}