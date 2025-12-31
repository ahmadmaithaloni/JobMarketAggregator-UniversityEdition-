using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScraperAPI.Models;
using System.Runtime.CompilerServices;
using WebApplication1.Models;

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

        [HttpPost("CreateUserProfile/{UserName}/{UserAddress}/{UserEmail}/{UserPhone}/{UserMajor}/{UserPassword}")] 
        public async Task<IActionResult> CreateUserProfile(string UserName,string UserAddress, string UserEmail, string UserPhone, string UserMajor, string UserPassword)
        {
            // check if the entered user ID exists already in the db or not exist (first validation phase):
            //var user = await _dbContext.Users.FindAsync(UserID);
            //if (user != null || UserID<= 0) 
            //{
            //    _logger.LogError($"the user with id ({UserID}) is already exist or entered wrong data"); // to assign the error on the server terminal interface
            //    return BadRequest($"the user with id ({UserID}) is already exist in the db or entered wrong data"); // to warn the frontend client
            //}

            // check if the name already exist or not, or if it is not entered yet (second validation phase):
            var Nuser = _dbContext.Users.FirstOrDefault(n => n.UserName==UserName);
            if (UserName.Length== 0 || !char.IsLetter(UserName[0]) || Nuser!=null)
            {
                _logger.LogError("the user does not entered a username or the username starts with incorrect chars (does not contain letters in the first)");
                return BadRequest("the user does not entered a username or the username starts with incorrect chars (does not contain letters in the first)");
            }

            // check if the user entered password and it was so weak or even does not entered any password, (and check if the password belongs to an already exist account (checked before)):
            var Puser = _dbContext.Users.FirstOrDefault(u => u.UserPassword==UserPassword);
            if (UserPassword.Length == 0 || Puser!= null)
            {
                _logger.LogError("the user does not entered a password");
                return BadRequest("the user does not entered a password");
            }
            else
            {
                // follow the score to make the passkey stronger ( the scale is length, complexity, numeric usage, symbols), one point for each grade:
                int PasswordScore = 0;

                // rule 1: password length:
                if (UserPassword.Length<8)
                {
                    _logger.LogError("the password is shorter than 8 chars");
                    return BadRequest("the password is shorter than 8 chars");
                }

                PasswordScore+=1;

                // rule 2: password complexity (uppercase letters):
                if (UserPassword.Any(char.IsUpper))
                {
                    PasswordScore += 1;
                }

                // rule 3: use numbers in the password:
                if (UserPassword.Any(char.IsDigit))
                {
                    PasswordScore+=1;
                }

                // rule 4: check for using special characters (symbols):
                if (UserPassword.Any(ch => !char.IsLetterOrDigit(ch)))
                {
                    PasswordScore += 1;
                }

                //final check if the password strong enough or not:
                switch (PasswordScore) {

                    case 4:
                        _logger.LogInformation("user password is very strong");
                        break;
                    case 3:
                        _logger.LogInformation("user password is strong");
                        break;
                    case 2:
                        _logger.LogInformation("user password is medium");
                        break;
                    case 1:
                        _logger.LogInformation("user password is very weak");
                        break;
                }
            }

            // now we can create the user profile:
            var DbUser = new User
            {
                //UserId = UserID,
                UserName = UserName,
                UserAddress = UserAddress,
                UserEmail = UserEmail,
                UserMajor = UserMajor,
                UserPhone = UserPhone,
                UserPassword = UserPassword,
                CreationDate = DateTime.UtcNow
            };

            _dbContext.Users.Add(DbUser); // add the user object to the db
            await _dbContext.SaveChangesAsync(); // update the db
            _logger.LogInformation($"new user profile with ID ({DbUser.UserId}) was created successfully"); // log the result in the server terminal interface
            return Ok($"the user profile with ID ({DbUser.UserId}) was created successfully"); // return positive result (200 ok) to the client
        }
    }
}
