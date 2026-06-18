using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using ScraperAPI.DTOs;

namespace ScraperAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserManagementController : ControllerBase
    {
        private readonly ScrapingEngineDbContext _dbContext;
        private readonly ILogger<UserManagementController> _logger;
        public UserManagementController(ScrapingEngineDbContext dbContext, ILogger<UserManagementController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        // Login:
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation($"Login attempt for email: {request?.Email}");
            // check if the login info are null or empty or not:
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                _logger.LogWarning("Login failed: Missing email or password.");
                return BadRequest("Email and Password are required.");
            }
            // check for the info availability and validation in the db:
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserEmail == request.Email && u.UserPassword == request.Password);
            if (user == null)
            {
                _logger.LogWarning($"Login failed: User not found or wrong password for email {request.Email}");
                return NotFound("Invalid email or password.");
            }

            // check for account verification:
            if(user != null && !user.IsVerified)
            {
                _logger.LogError($"user with email ({request.Email}) cannot login, he should verified it's account first");
                return StatusCode(403, "Please verify your account first. Check your email.");
            }
            // log and return the user info:
            _logger.LogInformation($"Login successful for user: {user.UserName} ({user.UserId})");
            return Ok(user);
        }

        // change user name:
        [HttpPut("ChangeUserName/v1")]
        public async Task<IActionResult> ChangeUserName([FromBody] ChangeUserNameRequest request)
        {
            // check if the entered info filled up correct or not (validation phase 1):
            if (request.UserID  <=0 || request.UserPassKey.Equals("") || request.UserNewName.Equals(""))
            {
                _logger.LogError($"the Client entered information are not correct or have missed data");
                return NotFound("the entered data are not correct");
            }
            // check if the client exist in the db (validation phase 2):
            bool IsUserExists = await _dbContext.Users.AnyAsync(u => u.UserId == request.UserID);
            if (IsUserExists== false)
            {
                _logger.LogError($"the user with UserID : ({request.UserID}) is not regestered in the system");
                return NotFound($"the user does not exist");
            }
            // check if the password key is correct or not and if it related to the same user or not (validation phase 3):
            bool IsPassCorrect = await _dbContext.Users.AnyAsync(p => p.UserPassword == request.UserPassKey && p.UserId == request.UserID);
            if(IsPassCorrect == false)
            {
                _logger.LogError($"the password is not correct, or it is belongs to another user account");
                return NotFound($"the password or UserID is not correct");
            }
            // last validation phase, check if the entered name belongs to another user or belongs to the same client but not changed (validation phase 4):
            bool IsNameUsed = await _dbContext.Users.AnyAsync(n => n.UserName ==request.UserNewName);
            if(IsNameUsed == true)
            {
                _logger.LogError($"the client ({request.UserID}) tried to insert new name that already exists and related to another account");
                return NotFound($"the name ({request.UserNewName}) already exists"); 
            }
            bool IsNameOld = await _dbContext.Users.AnyAsync(n => n.UserId == request.UserID && n.UserName == request.UserNewName);
            if (IsNameOld == true)
            {
                _logger.LogError($"user with user ID ({request.UserID}), tried to enter the same name of himself");
                return NotFound("the name does not changed");
            }
            // everything good, now affect the db:
            var DbUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == request.UserID);
            // check if the user exists or not (for the safty):
            if(DbUser == null)
            {
                _logger.LogError($"the User with id({request.UserID}) is not exist");
                return NotFound($"the user ({request.UserID}) is not exist");
            }
            // update the Object then send to the db:
            DbUser.UserName = request.UserNewName;
            _dbContext.Users.Update(DbUser);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"the user with ID : ({request.UserID}), has update his username ti be ({request.UserNewName})");
            return Ok($"the user ({request.UserID}) name is updated successfully");
        }

        // change the user Address:
        [HttpPut("ChangeUserAddress/v1")]
        public async Task<IActionResult> ChangeUserAddress([FromBody] ChangeUserAddressRequest request)
        {
            // check if the entered data are empty (validation phase 1):
            if(request.UserID <= 0 || request.UserPassWord.Equals("") || request.UserNewAddress.Equals(""))
            {
                _logger.LogError($"the user ({request.UserID}) sent ChangeAddress request with empty field data");
                return NotFound($"the data fields are empty, please retry again");
            }
            // check if the client exist in the db (validation phase 2):
            bool IsUserExist = await _dbContext.Users.AnyAsync(u => u.UserId == request.UserID);
            if(IsUserExist == false)
            {
                _logger.LogError($"the user id ({request.UserID}) that comes from the (ChangeUserAddress) request is not exist");
                return NotFound($"the user with id ({request.UserID}) is not exist, please retry again later ...");
            }
            // chack if the password correct and belongs to the same user (validation phase 3):
            bool IsPasswordCorrect = await _dbContext.Users.AnyAsync(p => p.UserId == request.UserID && p.UserPassword == request.UserPassWord);
            if (IsPasswordCorrect == false)
            {
                _logger.LogError($"the user with id ({request.UserID}) has entered wrong password in the (ChangeUserAddress) request");
                return NotFound($"the password isn't correct, please retry again later ...");
            }
            // check if the new address equals to the previous (validation phase 4):
            bool IsAddressOld = await _dbContext.Users.AnyAsync(a => a.UserAddress == request.UserNewAddress && a.UserId == request.UserID);
            if (IsAddressOld == true)
            {
                _logger.LogError($"the user ({request.UserID}) tried to enter an already exist UserAdderss in request (ChangeUserAddress)");
                return NotFound("the entered Address is already exists, please retry again later ...");
            }
            // now save the changes in the db:
            var DbUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == request.UserID);
            // check if the user exists or not (for the safty):
            if(DbUser == null)
            {
                _logger.LogError($"the User with id({request.UserID}) is not exist");
                return NotFound($"the user ({request.UserID}) is not exist");
            }
            DbUser.UserAddress = request.UserNewAddress;
            _dbContext.Users.Update(DbUser);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"the user ({request.UserID}) has update his address successfully through (ChangeUserAddress) request");
            return Ok("Address updated successfully");
        }

        // change user Email:
        [HttpPut("ChangeEmailAddress/v1")]
        public async Task<IActionResult> ChangeEmailAddress([FromBody] ChangeUserEmailRequest request)
        {
            // check if the entered data are empty or have invallid data (validation phase 1):
            if(request.UserID <= 0 || request.UserPassword.Equals("") || request.UserNewEmail.Equals(""))
            {
                _logger.LogError($"the entered data from request (ChangeEmailAddress) are invalled");
                return BadRequest($"the entered Data are invalled, please retry again later ...");
            }
            // check if the user exist in the db or not (validation phase 2):
            bool IsUserExist = await _dbContext.Users.AnyAsync(u => u.UserId ==request.UserID);
            if (IsUserExist == false)
            {
                _logger.LogError($"the user with id ({request.UserID}) that comes from the (ChangeEmailAddress) request is not exist in the DB");
                return BadRequest($"the UserID ({request.UserID}) is not exist, please retry again later ...");
            }
            // check if the password belongs to the same person (validation phase 3):
            bool IsPassCorrect = await _dbContext.Users.AnyAsync(p => p.UserId == request.UserID && p.UserPassword == request.UserPassword);
            if (IsPassCorrect == false)
            {
                _logger.LogError($"the user ({request.UserID}) had entered wrong password in (ChangeEmailAddress) request");
                return BadRequest($"the entered password is not correct, please retry again later ...");
            }
            // check if the entered email address follows the email address standards or not (validation phase 4):
            string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(request.UserNewEmail, EmailPattern))
            {
                _logger.LogError($"the user ({request.UserID} tried to enter invalid email address ({request.UserNewEmail}) in the (ChangeEmailAddress) request");
                return BadRequest("The entered email address has invalid format, it should be (example@domain.com) , please retry again later ...");
            }
            // check if the email belongs to another user (validation phase 5):
            bool IsEmailTaken = await _dbContext.Users.AnyAsync(e => e.UserEmail == request.UserNewEmail );
            if (IsEmailTaken == true)
            {
                _logger.LogError($"the email ({request.UserNewEmail}) that entered from user ({request.UserID}) in request (ChangeEmailAddress) is already used");
                return BadRequest($"the entered email is already exist, try to enter another one again later ...");
            }
            // check if the email is already exist with the same user (validation phase 6):
            bool IsEmailUsed = await _dbContext.Users.AnyAsync(e => e.UserId == request.UserID && e.UserEmail == request.UserNewEmail);
            if (IsEmailUsed == true)
            {
                _logger.LogError($"user with id ({request.UserID}) tried to enter already used email (by him) in (ChangeEmailAddress) request");
                return BadRequest($"the entered email is already used by the same user account, please retry again later ...");
            }
            // now time to save the info:
            var DbUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == request.UserID);
            if(DbUser == null)
            {
                _logger.LogError($"the user with id ({request.UserID}) is not exist");
                return NotFound($"the user with id ({request.UserID}) is not found, please retry again later ...");
            }
            DbUser.UserEmail = request.UserNewEmail;
            _dbContext.Users.Update(DbUser);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"the user ({request.UserID}) was update his email address successfully through (ChangeEmailAddress) request");
            return Ok("the email address changed successfully");
        }

        // change user password:
        [HttpPut("ChangePassword/v1")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangeUserPasswordRequest request)
        {
            // check if the entered data empty or not (validation phase 1):
            if (request.UserID <=0 || request.UserPassword.Equals("") || request.UserNewPassword.Equals(""))
            {
                _logger.LogError($"the entered values are not correct");
                return BadRequest($"the entered information are not correct, please retry again later ...");
            }
            // grab the user from the db to reduce the db hits :
            var DbUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == request.UserID);
            // check if the User exist in the Db (validation phase 2):
            if(DbUser == null)
            {
                _logger.LogError($"the user with id ({request.UserID}) that comes from the (ChangePassword) request is not exist in the DB");
                return BadRequest($"the UserID ({request.UserID}) is not exist, please retry again later ...");
            }
            // check if the password belongs to the same user or not (validation phase 3):
            if(DbUser.UserPassword != request.UserPassword)
            {
                _logger.LogError($"the user ({request.UserID}) had entered wrong password in (ChangePassword) request");
                return BadRequest($"the entered password is not correct, please retry again later ...");
            }
            // check if the new password follows the password standards or not (validation phase 4):
            string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$";
            if(!Regex.IsMatch(request.UserNewPassword, PasswordPattern))
            {
                _logger.LogError($"the user with id ({request.UserID}) tried to enter a password that not follows the password standards ({request.UserNewPassword}) in (changePassword) request");
                return BadRequest($"wrong password, your password must be at least 8 characters and contain: Uppercase, Lowercase, Number, and Special Character (@!#$%?*&)");
            }
            // check if the password matched with the current password (validation phase 5):
            if(DbUser.UserPassword == request.UserNewPassword)
            {
                _logger.LogError($"the user ({request.UserID}) tried to enter a password that related with his account already in (ChangePassword) request");
                return BadRequest($"the password is already used, please try to enter new password later ...");
            }
            // now, save the data:
            DbUser.UserPassword = request.UserNewPassword;
            _dbContext.Update(DbUser);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"the user ({request.UserID}) was update his password successfully through (ChangePassword) request");
            return Ok($"the password is changed successfully");
        }

        // change user phone:
        [HttpPut("ChangePhone/v1")]
        public async Task<IActionResult> ChangePhone([FromBody] ChangeUserPhoneRequest request)
        {
            // check if the entered data are empty (validation phase 1):
            if (request.UserID <= 0 || request.UserPassword.Equals("") || request.UserNewPhone.Equals(""))
            {
                _logger.LogError($"the entered values are not correct");
                return BadRequest($"the entered information are not correct, please retry again later ...");   
            }
            // fetch the user for one time from the db (to reduce db hits) :
            var DbUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == request.UserID);
            // check if the user exist in db or not (validation phase 2):
            if (DbUser == null)
            {
                _logger.LogError($"the user with id ({request.UserID}) that comes from the (ChangePhone) request is not exist in the DB");
                return BadRequest($"the UserID ({request.UserID}) is not exist, please retry again later ...");   
            }
            // check if the user password correct or not (validation phase 3):
            if(DbUser.UserPassword != request.UserPassword)
            {
                _logger.LogError($"the user ({request.UserID}) had entered wrong password in (ChangePhone) request");
                return BadRequest($"the entered password is not correct, please retry again later ...");
            }
            // check it the phone number follows the phone standards (validation phase 4):
            string PhonePattern = @"^07[789]\d{7}$";
            if (!Regex.IsMatch(request.UserNewPhone, PhonePattern))
            {
                _logger.LogError($"the user with id ({request.UserID}) tried to enter a phone number that not follows the phone standards ({request.UserNewPhone}) in (ChangePhone) request");
                return BadRequest($"wrong phone format, your phone number must be exactly 10 numbers and starts with (07)(7 or 8 or 9)");
            }
            // check if the phone number used by another account -we have to hit the db again- (validation phase 5) :
            bool IsPhoneTaken = await _dbContext.Users.AnyAsync(u => u.UserId != request.UserID && u.UserPhone == request.UserNewPhone);
            if (IsPhoneTaken == true)
            {
                _logger.LogError($"the phone number ({request.UserNewPhone}) that entered from user ({request.UserID}) in request (ChangePhone) is already used by another account");
                return BadRequest($"the entered phone number is already exist, try to enter another one again later ...");
            }
            // check if the phone number matches the old one (validation phase 6):
            if (DbUser.UserPhone == request.UserNewPhone)
            {
                _logger.LogError($"the user ({request.UserID}) tried to enter a phone number that related with his account already in (ChangePhone) request");
                return BadRequest($"the phone number is already used, please try to enter new phone number later ...");
            }
            // now, save the data:
            DbUser.UserPhone = request.UserNewPhone;
            _dbContext.Users.Update(DbUser);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"the user ({request.UserID}) was update his phone number successfully through (ChangePhone) request");
            return Ok($"the phone number is changed successfully");
        }

        // change user major:
        [HttpPut("ChangeMajor/v1")]
        public async Task<IActionResult> ChangeMajor([FromBody] ChangeUserMajorRequest request)
        {
            // check if the entered data empty or not (validation phase 1):
            if (request.UserID <= 0 || request.UserPassword.Equals("") || request.UserNewMajor.Equals(""))
            {
                _logger.LogError($"the entered values are not correct");
                return BadRequest($"the entered information are not correct, please retry again later ...");   
            }
            // grab the user from the db:
            var DbUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == request.UserID);
            // check if the user exist in the db or not (validation phase 2):
            if (DbUser == null)
            {
                _logger.LogError($"the user with id ({request.UserID}) that comes from the (ChangeMajor) request is not exist in the DB");
                return BadRequest($"the UserID ({request.UserID}) is not exist, please retry again later ..."); 
            }
            // check if the password correct or not (validation phase 3):
            if(DbUser.UserPassword != request.UserPassword)
            {
                _logger.LogError($"the user ({request.UserID}) had entered wrong password in (ChangeMajor) request");
                return BadRequest($"the entered password is not correct, please retry again later ...");
            }
            // check if the major field follows the standards or not (validation phase 4):
            string MajorPattern = @"^[a-zA-Z\s\-\(\)]+$";
            if (!Regex.IsMatch(request.UserNewMajor, MajorPattern))
            {
                _logger.LogError($"the user with id ({request.UserID}) tried to enter a Major that not follows the text field standards ({request.UserNewMajor}) in (ChangeMajor) request");
                return BadRequest($"wrong Major text format, Use only letters ...");
            }
            // check if the length of the major is less than 100 letters or not -db validation- (validation phase 5):
            if (request.UserNewMajor.Length > 100)
            {
                _logger.LogError($"the user ({request.UserID}) tried to enter Major that has length more than 100 letters in (ChangeMajor) request");
                return BadRequest($"the Major length is too huge, retry again later ...");
            }
            // check if the major is taken before from the same user (validation phase 6):
            if(DbUser.UserMajor == request.UserNewMajor)
            {
                _logger.LogError($"the user ({request.UserID}) tried to enter a major that related with his account already in (ChangeMajor) request");
                return BadRequest($"the Major is already exist, please try to enter new Major later ...");
            }
            // save the data:
            DbUser.UserMajor = request.UserNewMajor;
            _dbContext.Users.Update(DbUser);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"the user ({request.UserID}) was update his Major successfully through (ChangeMajor) request");
            return Ok($"the Major is changed successfully");
        }
    }
}