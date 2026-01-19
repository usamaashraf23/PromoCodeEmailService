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
                var startDate = promoCodes.FirstOrDefault()?.ValidFrom ?? DateTime.Today;
                var endDate = promoCodes.FirstOrDefault()?.ValidTo ?? DateTime.Today.AddDays(6);

                var emailBody = GenerateEmailBody(promoCodes, startDate, endDate);
                var emailSubject = $"Weekly Promo Codes for StartusAI Feature! ({startDate:dd MMM} - {endDate:dd MMM})";

                // Convert recipients array to list
                //var recipientsList = _emailConfig.Recipients.ToList();
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

        //        private string GenerateEmailBody(List<PromoCode> promoCodes, DateTime startDate, DateTime endDate)
        //        {
        //            var startDateStr = startDate.ToString("dd MMM");
        //            var endDateStr = endDate.ToString("dd MMM");
        //            var startDateFull = startDate.ToString("MMMM dd, yyyy");
        //            var endDateFull = endDate.ToString("MMMM dd, yyyy");

        //            // Start with the simplest HTML structure for Outlook compatibility
        //            var html = $@"
        //<!DOCTYPE html>
        //<html>
        //<head>
        //    <meta charset='UTF-8'>
        //    <!--[if mso]>
        //    <style>
        //        table {{border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt;}}
        //        td, th {{border: 1px solid #dddddd;}}
        //    </style>
        //    <![endif]-->
        //</head>
        //<body style='font-family: Arial, sans-serif; line-height: 1.5; color: #333333; margin: 0; padding: 20px; background-color: #f5f5f5;'>
        //<div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; border: 1px solid #dddddd;'>

        //    <!-- Header -->
        //    <div style='background-color: #2c5aa0; color: white; padding: 20px; text-align: center; border-bottom: 3px solid #1a3d7a;'>
        //        <h2 style='margin: 0; font-size: 20px;'>Weekly Promo Codes</h2>
        //        <p style='margin: 5px 0 0 0; font-size: 14px;'>{startDateStr} - {endDateStr}</p>
        //    </div>

        //    <!-- Content -->
        //    <div style='padding: 25px;'>
        //        <p style='margin: 0 0 15px 0;'>Dear Team,</p>

        //        <p style='margin: 0 0 15px 0;'>Below are the promo codes for the StartusAI feature this week. Please share these with our customers and encourage them to take advantage of these discounts.</p>

        //        <div style='margin: 20px 0;'>
        //            <strong>Original Price:</strong> <span style='color: #d9534f; font-weight: bold;'>$2.00</span>
        //        </div>

        //        <div style='color: #2c5aa0; border-bottom: 1px solid #eaeaea; padding-bottom: 8px; margin: 25px 0 15px 0; font-size: 16px; font-weight: bold;'>
        //            Promo Code Details
        //        </div>

        //        <!-- Table - Using basic table structure for Outlook compatibility -->
        //        <table style='width: 100%; border-collapse: collapse; margin: 15px 0; font-size: 14px;' border='1' cellpadding='8' cellspacing='0'>
        //            <thead>
        //                <tr style='background-color: #f0f0f0;'>
        //                    <th style='text-align: left; padding: 10px; border: 1px solid #dddddd; font-weight: bold;'>#</th>
        //                    <th style='text-align: left; padding: 10px; border: 1px solid #dddddd; font-weight: bold;'>Discounted Price</th>
        //                    <th style='text-align: left; padding: 10px; border: 1px solid #dddddd; font-weight: bold;'>Discount</th>
        //                    <th style='text-align: left; padding: 10px; border: 1px solid #dddddd; font-weight: bold;'>Promo Code</th>
        //                </tr>
        //            </thead>
        //            <tbody>";

        //            for (int i = 0; i < promoCodes.Count; i++)
        //            {
        //                var promo = promoCodes[i];
        //                html += $@"
        //                <tr>
        //                    <td style='padding: 10px; border: 1px solid #eeeeee;'>{i + 1}</td>
        //                    <td style='padding: 10px; border: 1px solid #eeeeee;'><strong>{promo.FormattedPrice}</strong></td>
        //                    <td style='padding: 10px; border: 1px solid #eeeeee; color: #5cb85c; font-weight: bold;'>{promo.FormattedDiscount} off</td>
        //                    <td style='padding: 10px; border: 1px solid #eeeeee; font-family: 'Courier New', monospace; font-weight: bold; color: #2c5aa0; font-size: 15px; letter-spacing: 1px;'>{promo.Code}</td>
        //                </tr>";
        //            }

        //            html += $@"
        //            </tbody>
        //        </table>

        //        <!-- Note Box -->
        //        <div style='background-color: #f8f9fa; border-left: 4px solid #2c5aa0; padding: 12px 15px; margin: 20px 0;'>
        //            <strong>Important Notes:</strong>
        //            <ul style='margin: 8px 0 0 0; padding-left: 20px;'>
        //                <li>Valid from <strong>{startDateFull}</strong> to <strong>{endDateFull}</strong></li>
        //                <li>Each code can be used only once per customer</li>
        //                <li>Codes expire automatically at the end of the validity period</li>
        //            </ul>
        //        </div>

        //        <p style='margin: 20px 0 15px 0;'><strong>Action Required:</strong> Please use these promo codes when assisting customers with purchases for the StartusAI feature.</p>

        //        <p style='margin: 0 0 20px 0;'>If you have any questions or need assistance, please reach out.</p>

        //        <!-- Footer -->
        //        <div style='margin-top: 25px; padding-top: 20px; border-top: 1px solid #dddddd; color: #666666; font-size: 12px; line-height: 1.4;'>
        //            <p>Best regards,<br>
        //            <strong>CareCloud Team</strong></p>

        //            <p style='margin-top: 15px; color: #999999;'>
        //                This is an automated email. Please do not reply directly to this message.<br>
        //                For support, contact: support@carecloud.com
        //            </p>
        //        </div>
        //    </div>
        //</div>
        //</body>
        //</html>";

        //            return html;
        //        }

        private string GenerateEmailBody(List<PromoCode> promoCodes, DateTime startDate, DateTime endDate)
        {
            var startDateStr = startDate.ToString("dd MMM");
            var endDateStr = endDate.ToString("dd MMM");
            var startDateFull = startDate.ToString("MMMM dd, yyyy");
            var endDateFull = endDate.ToString("MMMM dd, yyyy");

            // Start with the simplest possible HTML for Outlook compatibility
            var html = $@"
<html>
<body style=""font-family: Arial, sans-serif; font-size: 14px; color: #000000; margin: 0; padding: 0;"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color: #f5f5f5;"">
<tr>
<td align=""center"">
    <!-- Main container -->
    <table width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color: #ffffff; border: 1px solid #cccccc;"">
    
    <!-- Header -->
    <tr>
        <td bgcolor=""#2c5aa0"" align=""center"" style=""padding: 20px; border-bottom: 3px solid #1a3d7a;"">
            <font face=""Arial, sans-serif"" color=""#ffffff"" size=""5"">
                <strong>Weekly Promo Codes</strong>
            </font><br>
            <font face=""Arial, sans-serif"" color=""#ffffff"" size=""3"">
                {startDateStr} - {endDateStr}
            </font>
        </td>
    </tr>
    
    <!-- Content -->
    <tr>
        <td style=""padding: 25px 20px;"">
            <p style=""margin-top: 0; margin-bottom: 15px;"">Dear Team,</p>
            
            <p style=""margin-bottom: 15px;"">Below are the promo codes for the StartusAI feature this week. Please share these with our customers and encourage them to take advantage of these discounts.</p>
            
            <p style=""margin-bottom: 20px;"">
                <strong>Original Price:</strong> 
                <font color=""#d9534f""><strong>$2.00</strong></font>
            </p>
            
            <!-- Section title -->
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-bottom: 15px;"">
            <tr>
                <td style=""border-bottom: 2px solid #2c5aa0; padding-bottom: 5px;"">
                    <font face=""Arial, sans-serif"" color=""#2c5aa0"" size=""4"">
                        <strong>Promo Code Details</strong>
                    </font>
                </td>
            </tr>
            </table>
            
            <!-- Promo codes table -->
            <table width=""100%"" cellpadding=""8"" cellspacing=""0"" border=""1"" bordercolor=""#dddddd"" style=""border-collapse: collapse; margin-bottom: 20px;"">
            <tr bgcolor=""#f0f0f0"">
                <th width=""10%"" align=""left"" style=""padding: 10px; border: 1px solid #dddddd;"">#</th>
                <th width=""35%"" align=""left"" style=""padding: 10px; border: 1px solid #dddddd;"">Discounted Price</th>
                <th width=""25%"" align=""left"" style=""padding: 10px; border: 1px solid #dddddd;"">Discount</th>
                <th width=""30%"" align=""left"" style=""padding: 10px; border: 1px solid #dddddd;"">Promo Code</th>
            </tr>";

            for (int i = 0; i < promoCodes.Count; i++)
            {
                var promo = promoCodes[i];
                html += $@"
            <tr>
                <td style=""padding: 10px; border: 1px solid #dddddd;"">{i + 1}</td>
                <td style=""padding: 10px; border: 1px solid #dddddd;""><strong>{promo.FormattedPrice}</strong></td>
                <td style=""padding: 10px; border: 1px solid #dddddd; color: #008000;""><strong>{promo.FormattedDiscount} off</strong></td>
                <td style=""padding: 10px; border: 1px solid #dddddd; font-family: 'Courier New', Courier, monospace; color: #2c5aa0; font-size: 15px;""><strong>{promo.Code}</strong></td>
            </tr>";
            }

            html += $@"
            </table>
            
            <!-- Important Notes -->
            <table width=""100%"" cellpadding=""15"" cellspacing=""0"" border=""0"" style=""background-color: #f8f9fa; border-left: 4px solid #2c5aa0; margin-bottom: 20px;"">
            <tr>
                <td>
                    <font face=""Arial, sans-serif"" size=""3"">
                        <strong>Important Notes:</strong>
                    </font>
                    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-top: 8px;"">
                    <tr>
                        <td style=""padding-left: 20px;"">
                            • Valid from <strong>{startDateFull}</strong> to <strong>{endDateFull}</strong><br>
                            • Each code can be used only once per customer<br>
                            • Codes expire automatically at the end of the validity period
                        </td>
                    </tr>
                    </table>
                </td>
            </tr>
            </table>
            
            <!-- Action Required -->
            <p style=""margin-bottom: 15px;"">
                <strong>Action Required:</strong> Please use these promo codes when assisting customers with purchases for the StartusAI feature.
            </p>
            
            <p style=""margin-bottom: 20px;"">If you have any questions or need assistance, please reach out.</p>
            
            <!-- Footer -->
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-top: 1px solid #dddddd; padding-top: 20px;"">
            <tr>
                <td>
                    <p style=""margin-bottom: 5px;"">Best regards,<br>
                    <strong>CareCloud Team</strong></p>
                    
                    <p style=""margin-top: 15px; color: #666666; font-size: 12px;"">
                        This is an automated email. Please do not reply directly to this message.<br>
                        For support, contact: support@carecloud.com
                    </p>
                </td>
            </tr>
            </table>
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