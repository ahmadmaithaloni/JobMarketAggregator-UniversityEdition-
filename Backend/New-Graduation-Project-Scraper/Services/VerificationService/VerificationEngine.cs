using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WebApplication1.Models;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScraperAPI.Options;
using System.Net;

namespace ScraperAPI.Services.VerificationService
{
    public class VerificationEngine : IVerificationService
    {
        // injection container
        private readonly ILogger<VerificationEngine> _logger;
        private readonly ScrapingEngineDbContext _dbContext;
        private readonly IOptions<EmailSettingsOptions> _options;
        public VerificationEngine(ILogger<VerificationEngine> logger, ScrapingEngineDbContext dbContext, IOptions<EmailSettingsOptions> options)
        {
            _logger = logger;
            _dbContext = dbContext;
            _options = options;
        }

        // methods
        public async Task<string?> GenerateVerificationCodeAsync(string email)
        {
            // email validation:
            if(email.Equals(""))
            {
                _logger.LogError($"the entered email is invalled, can't generate verification code");
                return null;
            }
            string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(email, EmailPattern))
            {
                _logger.LogError($"the entered email does not follow the email standards, can't generate verification code");
                return null;
            }
            
            // check if the client that has this email exist or not:
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserEmail == email);
            if (user == null)
            {
                _logger.LogError($"the email ({email}) is not belong to a valid user in the db, can't generate the verification code");
                return null;
            }

            // create the verification code (encrypted):
            int SecureCode = RandomNumberGenerator.GetInt32(100000, 1000000); // min max boundaries
            string FinalCode = SecureCode.ToString("D6"); // turn it to string to sent it and save it, and to avoid started 0 degit
            
            // save and sent the code:
            user.VerificationCode = FinalCode;
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"the verifcation code for ({email}) is generated and saved successfully");
            return FinalCode;
        }

        public async Task<bool?> SendVerificationCodeAsync(string email)
        {
            //email validation:
            if(email.Equals(""))
            {
                _logger.LogError($"the entered email is invalled, can't send verification code");
                return false;
            }
            string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(email, EmailPattern))
            {
                _logger.LogError($"the entered email does not follow the email standards, can't send verification code");
                return false;
            }

            // check if the client that has this email exist or not:
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserEmail == email);
            if (user == null)
            {
                _logger.LogError($"the email ({email}) is not belong to a valid user in the db, can't send the verification code");
                return false;
            }
            
            // setup the code message to sent:
            string? code = user.VerificationCode;
            string? CodeMessage = $"<h3>Welcome</h3><p>Your verification code is: <b>{code}</b></p>";
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.Value.SenderName, _options.Value.SenderEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Verification Message";
            message.Body = new TextPart("html") {
                Text = $"<h3>Welcome</h3><p>Your verification code is: <b>{user.VerificationCode}</b></p>"
            };

            // send the message then log and return true:
            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(_options.Value.SmtpServer, _options.Value.Port, SecureSocketOptions.Auto);
                await client.AuthenticateAsync(_options.Value.SenderEmail, _options.Value.AppPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Email sent successfully to {email}");
                return true;
            }  
            catch (Exception ex)
            {
            _logger.LogError($"MailKit Error: {ex.Message}");
            return false;
            }
        }

        public async Task<bool?> CheckVerificationCodeAsync(string email, string code)
        {
            // email validation:
            if(email.Equals(""))
            {
                _logger.LogError($"the entered email is invalled, can't check verification code match");
                return false;
            }
            string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(email, EmailPattern))
            {
                _logger.LogError($"the entered email does not follow the email standards, can't check verification code match");
                return false;
            }

            // check if the client that has this email exist or not:
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserEmail == email && u.VerificationCode == code);
            if (user == null)
            {
                _logger.LogError($"either the email ({email}) or verification code ({code})are not belong to a valid user in the db, can't check verification code match");
                return false;
            }
            
            // update the verification status value and wipe Code attribute:
            user.IsVerified = true;
            user.VerificationCode = null;
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"User with email ({email}) has entered valid verification code, the account is now activated");
            return true;
        }
    }
}