using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using HamroMart.Models;

namespace HamroMart.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string email, string subject, string message);
        Task SendOTPAsync(string email, string otp);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                    Subject = subject,
                    Body = message,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(email);

                using var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password)
                };

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send email: {ex.Message}");
            }
        }

        public async Task SendOTPAsync(string email, string otp)
        {
            var subject = "HamroMart - Email Verification OTP";
            var message = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: #28a745; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background: #f9f9f9; }}
                        .otp {{ font-size: 32px; font-weight: bold; color: #28a745; text-align: center; margin: 20px 0; }}
                        .footer {{ text-align: center; padding: 20px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>HamroMart</h1>
                            <p>Email Verification</p>
                        </div>
                        <div class='content'>
                            <h3>Hello!</h3>
                            <p>Thank you for registering with HamroMart. Use the following OTP to verify your email address:</p>
                            <div class='otp'>{otp}</div>
                            <p>This OTP will expire in 10 minutes.</p>
                            <p>If you didn't request this, please ignore this email.</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2025 HamroMart. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(email, subject, message);
        }
    }
}