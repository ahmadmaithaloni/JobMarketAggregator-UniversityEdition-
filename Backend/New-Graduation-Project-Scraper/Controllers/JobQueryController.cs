using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScraperAPI.DTOs;
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

        [HttpPost("CreateJobQuery/v1")]
        public async Task<ActionResult<ScrapedJob>> CreateJobQuery([FromBody] CreateJobQueryRequest request) // accepts job query from an (identified user) {should contain the date and user id and query description and query id}
        {
            // check if the user exists in the database or the id is a different random number comes from the frontend (first validation):
            var user = await _dbContext.Users.FindAsync(request.UserID);
            if (user == null || request.UserID <= 0)
            {
                _logger.LogError($"user with id ({request.UserID}) is not regestered in the system, or entered wrong data"); // log the error in the terminal
                return NotFound("User not found"); // return 404 not found if the user is not found
            }

            
            // check about the job details validity -> if the fields are empty or not (second validation phase):

            // 1. JobName field:
            if (string.IsNullOrWhiteSpace(request.JobName)) // check if the feild has null/empty value 
            {
                _logger.LogError($"the field JobName has null/empty value");
                return BadRequest($"the field JobName has null/empty value");
            }
            // 2. JobLocation field:
            if (string.IsNullOrWhiteSpace(request.JobLocation)) // check if the feild has null/empty value
            {
                _logger.LogError($"the field JobLocation has null/empty value");
                return BadRequest($"the field JobLocation has null/empty value");
            }
            // 3. Job start and end time validation (see if the start time after end time of not):
            
            // Validate time range
            if (request.JobStartTime > request.JobEndTime)
            {
                _logger.LogError($"the user ({request.UserID}) has enter the JobEndTime -> ({request.JobEndTime}) lower than JobStartTime -> ({request.JobStartTime})");
                return BadRequest("Start time cannot be after end time");
            }

            // 4. Job low and high salary validations:
            if (request.JobLowSalary <= 0 || request.JobHighSalary <= 0)
            {
                _logger.LogError("Salary values must be positive");
                return BadRequest("Salary values must be positive");
            }

            if (request.JobLowSalary > request.JobHighSalary)
            {
                _logger.LogError("Low salary cannot be greater than high salary");
                return BadRequest("Low salary cannot be greater than high salary");
            }

            // --- enforce limit of 10 queries per user ---
            var existingQueriesCount = await _dbContext.JobQueries.CountAsync(q => q.UserId == request.UserID);
            if (existingQueriesCount >= 10)
            {
                var oldestQuery = await _dbContext.JobQueries
                    .Where(q => q.UserId == request.UserID)
                    .OrderBy(q => q.CreationDate)
                    .FirstOrDefaultAsync();

                if (oldestQuery != null)
                {
                    _dbContext.JobQueries.Remove(oldestQuery);
                    // We don't save changes here yet, we'll save when we add the new one.
                    _logger.LogInformation($"Limit of 10 reached. Deleted oldest query ID: {oldestQuery.QueryId} for User: {request.UserID}");
                }
            }
            // --------------------------------------------

            // check if the query exist already in the db (validation 3th phase):
            var UserQuery = await _dbContext.JobQueries.FirstOrDefaultAsync(uq => uq.QjobName == request.JobName && uq.QjobLocation == request.JobLocation); 

            if (UserQuery != null) // there is other query exist in the database (case)
            {
                // existing query found, return the query object so the frontend can use the ID
                _logger.LogInformation($"the user get similar query with ID: {UserQuery.QueryId}");
                return Ok(UserQuery);
            }

            // save the User Inputs into one string variable named (QueryDescription):
            string QueryDescription = string.Empty;
            QueryDescription = $"JobSites that has {request.JobName} Jobs in {request.JobLocation} that start from {request.JobStartTime} to {request.JobEndTime} with salary starts from {request.JobLowSalary} to {request.JobHighSalary}";

            // after validation phases and query existance in the db check-> the api should add the data into the JobQuery Table in the db:
            
            // Let the database generate the primary key (identity/auto-increment). Do not manually assign QueryId.
            var DbJobQuery = new JobQuery
            {
                UserId = request.UserID,
                QueryDescription = QueryDescription,
                CreationDate = DateTime.UtcNow,
                QjobName = request.JobName,
                QjobLocation = request.JobLocation,
                QjobStartTime = request.JobStartTime, 
                QjobEndTime = request.JobEndTime,     
                QlowSalary = request.JobLowSalary,
                QhighSalary = request.JobHighSalary
            };

            _dbContext.JobQueries.Add(DbJobQuery); // save the object in the correct table in memory
            await _dbContext.SaveChangesAsync(); // save changes in the db 
            _logger.LogInformation($"the User ({request.UserID}), had entered Job Query to the DB, and data had inserted successfully to the DB)"); // log the changes in the terminal
            
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
            var queries = await _dbContext.JobQueries.Where(q => q.UserId == UserID).OrderByDescending(q => q.CreationDate).ToListAsync();
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