using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScraperAPI.Models;
using WebApplication1.Models;

namespace ScraperAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobQueryController : ControllerBase
    {
        private readonly ILogger<JobQueryController> _logger;
        private readonly ScrapingEngineDbContext _dbContext;
        public JobQueryController(ILogger<JobQueryController> logger, ScrapingEngineDbContext dbContext) // inject the logger and db contect session to 
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        [HttpPost("CreateJobQuery/v1/{UserID}/{JobName}/{JobLocation}/{JobStartTime}/{JobEndTime}/{JobLowSalary}/{JobHighSalary}")]
        public async Task<ActionResult<ScrapedJob>> CreateJobQuery(int UserID, string JobName, string JobLocation, TimeOnly JobStartTime, TimeOnly JobEndTime, decimal JobLowSalary, decimal JobHighSalary) // accepts job query from an (identified user) {should contain the date and user id and query description and query id}
        {
            // check if the user exists in the database or the id is a different random number comes from the frontend (first validation):
            var user = await _dbContext.Users.FindAsync(UserID);
            if (user == null || UserID <= 0)
            {
                _logger.LogError($"user with id ({UserID}) is not regestered in the system, or entered wrong data"); // log the error in the terminal
                return NotFound("User not found"); // return 404 not found if the user is not found
            }

            
            // check about the job details validity -> if the fields are empty or not (second validation phase):

            // 1. JobName field:
            // 1. JobName field:
            if (string.IsNullOrWhiteSpace(JobName)) // check if the feild has null/empty value 
            {
                _logger.LogError($"the field JobName has null/empty value");
                return BadRequest($"the field JobName has null/empty value");
            }
            // 2. JobLocation field:
            if (string.IsNullOrWhiteSpace(JobLocation)) // check if the feild has null/empty value
            {
                _logger.LogError($"the field JobLocation has null/empty value");
                return BadRequest($"the field JobLocation has null/empty value");
            }
            // 3. Job start and end time validation (see if the start time after end time of not):
            
            // Validate time range
            if (JobStartTime > JobEndTime)
            {
                _logger.LogError($"the user ({UserID}) has enter the JobEndTime -> ({JobEndTime}) lower than JobStartTime -> ({JobStartTime})");
                return BadRequest("Start time cannot be after end time");
            }

            // 4. Job low and high salary validations:
            if (JobLowSalary <= 0 || JobHighSalary <= 0)
            {
                _logger.LogError("Salary values must be positive");
                return BadRequest("Salary values must be positive");
            }

            if (JobLowSalary > JobHighSalary)
            {
                _logger.LogError("Low salary cannot be greater than high salary");
                return BadRequest("Low salary cannot be greater than high salary");
            }

            // check if the query exist already in the db (validation 3th phase):
            var UserQuery = await _dbContext.JobQueries.FirstOrDefaultAsync(uq => uq.QjobName == JobName && uq.QjobLocation == JobLocation); 

            if (UserQuery != null) // there is other query exist in the database (case)
            {
                // existing query found, return the query object so the frontend can use the ID
                _logger.LogInformation($"the user get similar query with ID: {UserQuery.QueryId}");
                return Ok(UserQuery);
            }

            // save the User Inputs into one string variable named (QueryDescription):
            string QueryDescription = string.Empty;
            QueryDescription = $"JobSites that has {JobName} Jobs in {JobLocation} that start from {JobStartTime} to {JobEndTime} with salary starts from {JobLowSalary} to {JobHighSalary}";

            // after validation phases and query existance in the db check-> the api should add the data into the JobQuery Table in the db:
            
            // Let the database generate the primary key (identity/auto-increment). Do not manually assign QueryId.
            var DbJobQuery = new JobQuery
            {
                UserId = UserID,
                QueryDescription = QueryDescription,
                CreationDate = DateTime.UtcNow,
                QjobName = JobName,
                QjobLocation = JobLocation,
                QjobStartTime = JobStartTime, 
                QjobEndTime = JobEndTime,     
                QlowSalary = JobLowSalary,
                QhighSalary = JobHighSalary
            };

            _dbContext.JobQueries.Add(DbJobQuery); // save the object in the correct table in memory
            await _dbContext.SaveChangesAsync(); // save changes in the db 
            _logger.LogInformation($"the User ({UserID}), had entered Job Query to the DB, and data had inserted successfully to the DB)"); // log the changes in the terminal
            
            // Return the created object so the frontend can get the QueryId
            return Ok(DbJobQuery); 
        }
        [HttpGet("GetUserQueries/v1/{UserID}")]
        public async Task<ActionResult<List<JobQuery>>> GetUserQueries(int UserID)
        {
            var user = await _dbContext.Users.FindAsync(UserID);
            if (user == null)
            {
                return NotFound("User not found");
            }

            var queries = await _dbContext.JobQueries
                .Where(q => q.UserId == UserID)
                .OrderByDescending(q => q.CreationDate)
                .ToListAsync();

            return Ok(queries);
        }
        [HttpGet("GetJobsByQueryId/v1/{QueryId}")]
        public async Task<ActionResult<List<ScrapedJob>>> GetJobsByQueryId(int QueryId)
        {
            var jobs = await _dbContext.ScrapedJobs
                .Where(j => j.QueryId == QueryId)
                .ToListAsync();

            if (jobs == null || !jobs.Any()) 
            {
                // It's okay to return empty list if no jobs found, users might want to re-scrape
                return Ok(new List<ScrapedJob>());
            }
            
            return Ok(jobs);
        }
    }
}