using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScraperAPI.DTOs;
using ScraperAPI.Models;
using System.Runtime.CompilerServices;
using WebApplication1.Models;
using System.Text.RegularExpressions;

namespace ScraperAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProfileSettingsController : ControllerBase
    {
        // depedancy injection (to secure the connection between the db and api):
        private readonly ILogger<ProfileSettingsController> _logger;
        private readonly ScrapingEngineDbContext _dbContext;
        public ProfileSettingsController(ILogger<ProfileSettingsController> logger,ScrapingEngineDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        [HttpPost("CreateUserProfile")] 
        public async Task<IActionResult> CreateUserProfile([FromBody] CreateUserProfileRequest request)
        {
            

            // 1. Validate Username
            if (string.IsNullOrWhiteSpace(request.UserName) || !char.IsLetter(request.UserName[0]))
            {
                _logger.LogError("Invalid username format.");
                return BadRequest("Username must start with a letter and cannot be empty.");
            }
            if (await _dbContext.Users.AnyAsync(u => u.UserName == request.UserName))
            {
                _logger.LogError($"Username '{request.UserName}' is already taken.");
                return BadRequest("Username is already taken.");
            }

            // 2. Validate Password (Strict Regex)
            // Pattern: At least 8 chars, 1 Upper, 1 Lower, 1 Digit, 1 Special Char
            string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$";
            if (string.IsNullOrWhiteSpace(request.UserPassword) || !Regex.IsMatch(request.UserPassword, PasswordPattern))
            {
                _logger.LogError("Invalid password format.");
                return BadRequest("Password must be at least 8 characters and include: Uppercase, Lowercase, Digit, and Special Character.");
            }

            // 3. Validate Email (Regex)
            string EmailPattern = @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$";
            if (string.IsNullOrWhiteSpace(request.UserEmail) || !Regex.IsMatch(request.UserEmail, EmailPattern))
            {
                _logger.LogError($"Invalid email format: {request.UserEmail}");
                return BadRequest("Invalid email address format.");
            }
            if (await _dbContext.Users.AnyAsync(u => u.UserEmail == request.UserEmail))
            {
                _logger.LogError($"Email '{request.UserEmail}' is already in use.");
                return BadRequest("Email address is already registered.");
            }

            // 4. Validate Phone (Regex: Jordan Format)
            // Pattern: 07 following by 7, 8, or 9, then 7 digits. Total 10 digits.
            string PhonePattern = @"^07[789]\d{7}$";
            if (!string.IsNullOrWhiteSpace(request.UserPhone))
            {
                if (!Regex.IsMatch(request.UserPhone, PhonePattern))
                {
                    _logger.LogError($"Invalid phone format: {request.UserPhone}");
                    return BadRequest("Phone number must be 10 digits starting with 077, 078, or 079.");
                }
                if (await _dbContext.Users.AnyAsync(u => u.UserPhone == request.UserPhone))
                {
                    _logger.LogError($"Phone '{request.UserPhone}' is already in use.");
                    return BadRequest("Phone number is already registered.");
                }
            }

            // 5. Validate Major (Regex: Letters only)
            string MajorPattern = @"^[a-zA-Z\s\-\(\)]+$";
            if (!string.IsNullOrWhiteSpace(request.UserMajor) && !Regex.IsMatch(request.UserMajor, MajorPattern))
            {
                _logger.LogError($"Invalid major format: {request.UserMajor}");
                return BadRequest("Major must contain only letters, spaces, or hyphens.");
            }

            // Create User Profile
            var DbUser = new User
            {
                UserName = request.UserName,
                UserAddress = request.UserAddress,
                UserEmail = request.UserEmail,
                UserMajor = request.UserMajor,
                UserPhone = request.UserPhone,
                UserPassword = request.UserPassword,
                CreationDate = DateTime.UtcNow
            };

            _dbContext.Users.Add(DbUser); // add the user object to the db
            await _dbContext.SaveChangesAsync(); // update the db
            _logger.LogInformation($"new user profile with ID ({DbUser.UserId}) was created successfully"); // log the result in the server terminal interface
            return Ok($"the user profile with ID ({DbUser.UserId}) was created successfully"); // return positive result (200 ok) to the client
        }
    }
}
