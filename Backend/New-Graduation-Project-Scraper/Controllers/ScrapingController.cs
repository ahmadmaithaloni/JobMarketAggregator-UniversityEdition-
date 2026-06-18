using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using ScraperAPI.Models;
using ScraperAPI.Services.Scraping_Service;
using SerpApi;
using System;
using System.Collections;
using System.Runtime.CompilerServices;
using WebApplication1.Models;
namespace ScraperAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScrapingController : ControllerBase
    {
        private readonly ScrapingEngineDbContext _dbContext;
        private readonly ILogger<ScrapingController> _logger;
        private readonly IScrapingService _scraper;
        private readonly ScraperAPI.Services.LocationMapper_Service.ILocationMapperService _locationMapper;

        public ScrapingController(ScrapingEngineDbContext dbContext, ILogger<ScrapingController> logger, IScrapingService scraper, ScraperAPI.Services.LocationMapper_Service.ILocationMapperService locationMapper)
        {
            _dbContext = dbContext;
            _logger = logger;
            _scraper = scraper;
            _locationMapper = locationMapper;
        }

        [HttpGet("GetLocations")]
        public ActionResult<List<string>> GetLocations()
        {
            return Ok(_locationMapper.GetAllLocations());
        }

        [HttpDelete("DeleteAllJobSites")]
        public async Task<IActionResult> DeleteAllJobSites()
        {
            // Count first to avoid loading all entities unnecessarily
            int totalSites = await _dbContext.JobSites.CountAsync();

            if (totalSites == 0)
            {
                _logger.LogWarning("No job sites found to delete.");
                return NotFound("No job sites found to delete.");
            }

            // Bulk delete without tracking all entities
            _dbContext.JobSites.RemoveRange(_dbContext.JobSites);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("{Count} job sites have been deleted successfully.", totalSites);

            return Ok(new
            {
                Message = "All job sites have been deleted successfully.",
                DeletedCount = totalSites
            });
        }


        [HttpGet("GetAvailableJobSitesURLS/v1/{UserID}")]
        public async Task<IActionResult> GetJobURLS(int UserID)
        {
            List<JobQuery> JobQuerys = await _dbContext.JobQueries.Where(qr => qr.UserId == UserID).ToListAsync();

            // check if the query does not has data or not (first validation phase):
            if (!JobQuerys.Any())
            {
                _logger.LogError($"The user with id ({UserID}), does not entered any queries");
                return NotFound("you should enter at least one query to perform this search");
            }

            // the Serp Api Key:
            String apiKey = "27126251a9a7362f541988c20d933c3ea6e74e94c2e2b215ccacd9630a2cf92e";
            int NumberOfFetchedSites = 0;
            foreach (JobQuery jobQuery in JobQuerys) // try theAPI Query for all JobQueries that related to the person
            {
                string QueryDetails = $"Jobsites that offer {jobQuery.QjobName} jobs in {jobQuery.QjobLocation}"; // store the query details (in unified format) into one 
                string QueryLocation = jobQuery.QjobLocation;
                Hashtable ht = new Hashtable();
                ht.Add("q", $"{QueryDetails}"); // should contain the query
                ht.Add("location", $"{QueryLocation}"); // should contain the locations 
                ht.Add("hl", "en"); // should contain the ابصر شو
                ht.Add("gl", "us"); // IDK
                ht.Add("google_domain", "google.com"); // search domain 

                // try the api search:
                 GoogleSearch search = new GoogleSearch(ht, apiKey); // sent the query with the api key to the SerpAPI
                 JObject data = search.GetJson();// receive the results as Json Array
                 JArray results = (JArray)data["organic_results"];
                 
                 //check if the query get results or not:
                 if (results== null)
                 {
                     _logger.LogError($"the retrieved Json Array from Serp API has no data, for Query({jobQuery.QueryId})");
                     return NotFound("there are no jobs related with this query");
                 }

                 foreach (JObject result in results)
                 {
                     // string JobTitle = result["title"].ToString(); // extract the job titles as values linked to the "title" keys
                     string url = result["link"].ToString(); // extract the urls as values linked to the "link" keys
                     string SiteNeme = result["source"].ToString(); // extract the site names as values linked to the "source" keys

                    // increment the Site id based on the total number of Sites in the DB:
                    int TotalNumberOfSites = await _dbContext.JobSites.CountAsync();
                    int NewSiteId = TotalNumberOfSites + 1;

                    // save the site data into the database:
                    JobSite DbSite = new JobSite
                     {
                        SiteId = NewSiteId,
                        SiteName = SiteNeme,
                        SiteUrl = url
                     };

                    

                     await _dbContext.JobSites.AddAsync(DbSite);
                     await _dbContext.SaveChangesAsync();
                     NumberOfFetchedSites += 1;
                     _logger.LogInformation($"Site with name ({SiteNeme}) and URL ({url}) has been added to the database."); 
                 }
            }
            return Ok($"the Query returned ({NumberOfFetchedSites}) of results successfully");
        }

        // version 2 from GetJobUrls endpoint:
        [HttpGet("GetAvailableJobSitesURLS/v2/{UserID}")]
        public async Task<IActionResult> GetJobUrls(int UserID)
        {
            // check if the entered ID is valid or not :
            bool UserExist= await _dbContext.Users.AnyAsync(u => u.UserId == UserID);
            if (!UserExist)
            {
                _logger.LogError($"the client has entered invalid UserID number ({UserID})");
                return NotFound($"you had entered wrong UserID number ({UserID}), please try again later");
            }

            // list the user queries:
            List<JobQuery> JobQuerys = await _dbContext.JobQueries.Where(qr => qr.UserId == UserID).ToListAsync();

            // check if the user has any query related to his id or not:
            if (!JobQuerys.Any())
            {
                _logger.LogError($"The user with id ({UserID}), does not entered any queries");
                return NotFound("you should enter at least one query to perform this search");
            }

            List<ScrapedJob> DbJobs = new List<ScrapedJob>();
            // then, list the queries details (loop):
            foreach (JobQuery jobQuery in JobQuerys)
            {
                // each job query must has query details to share with the _scraper service (QueryID):
                List<ScrapedJob> jobs = await _scraper.ScrapeWebsiteAsync(jobQuery.QueryId);

                // check if the scraper fetchs any data or not:
                if(jobs != null && jobs.Count >0)
                {
                    // add the list of scraped jobs froom the query to the big list and store it in the Scraped jobs table:
                    DbJobs.AddRange(jobs);
                }
                else
                {
                    _logger.LogWarning($"there is no any fetched jobs for this query ({jobQuery.QueryId}) that written by user ({UserID})");
                    // pass for the next qieries ... 
                }
                
            }
            // if there are no found jobs at all, the endpoint should return 404 :
            if (DbJobs.Count() == 0)
            {
                _logger.LogError($"the scraper does not find any work at all for all the job queries comes from the user ({UserID})");
                return NotFound($"there is no jobs found at all for all user ({UserID}) queries");
            }


            // store the big list in the db :
            _dbContext.ScrapedJobs.AddRangeAsync(DbJobs);
            await _dbContext.SaveChangesAsync();

            // log the info then return the success value :
            _logger.LogInformation($"all queries from the user ({UserID}) had fetched data and store it successfully to the db");
            return Ok(DbJobs);
        }

        // version 3 from GetJobUrls endpoint:
        [HttpGet("GetAvailableJobSitesURLS/v3/{UserID}")]
        public async Task<IActionResult> GetJobUrlsV3(int UserID)
        {
            // check if the entered ID is valid or not :
            bool UserExist = await _dbContext.Users.AnyAsync(u => u.UserId == UserID);
            if (!UserExist)
            {
                _logger.LogError($"the client has entered invalid UserID number ({UserID})");
                return NotFound($"you had entered wrong UserID number ({UserID}), please try again later");
            }

            // list the user queries:
            List<JobQuery> JobQuerys = await _dbContext.JobQueries.Where(qr => qr.UserId == UserID).ToListAsync();

            // check if the user has any query related to his id or not:
            if (!JobQuerys.Any())
            {
                _logger.LogError($"The user with id ({UserID}), does not entered any queries");
                return NotFound("you should enter at least one query to perform this search");
            }

            List<ScrapedJob> DbJobs = new List<ScrapedJob>();
            // then, list the queries details (loop):
            foreach (JobQuery jobQuery in JobQuerys)
            {
                // each job query must has query details to share with the _scraper service (QueryID):
                List<ScrapedJob> jobs = await _scraper.ScrapeWebsiteAsync(jobQuery.QueryId);

                // check if the scraper fetchs any data or not:
                if (jobs != null && jobs.Count > 0)
                {
                    // add the list of scraped jobs froom the query to the big list and store it in the Scraped jobs table:
                    DbJobs.AddRange(jobs);
                }
                else
                {
                    _logger.LogWarning($"there is no any fetched jobs for this query ({jobQuery.QueryId}) that written by user ({UserID})");
                }

            }
            // if there are no found jobs at all, the endpoint should return 404 :
            if (DbJobs.Count() == 0)
            {
                _logger.LogError($"the scraper does not find any work at all for all the job queries comes from the user ({UserID})");
                return NotFound($"there is no jobs found at all for all user ({UserID}) queries");
            }


            // store the big list in the db :
            _dbContext.ScrapedJobs.AddRangeAsync(DbJobs);
            await _dbContext.SaveChangesAsync();

            // log the info then return the success value :
            _logger.LogInformation($"all queries from the user ({UserID}) had fetched data and store it successfully to the db");
            return Ok(DbJobs);
        }

        // v4 (dynamic scraping using playwright)
        [HttpGet("GetAvailableJobSitesURLS/v4/{UserID}")]
        public async Task<IActionResult> GetJobUrlsV4(int UserID)
        {
            // check if the entered ID is valid or not :
            bool UserExist = await _dbContext.Users.AnyAsync(u => u.UserId == UserID);
            if (!UserExist)
            {
                _logger.LogError($"the client has entered invalid UserID number ({UserID})");
                return NotFound($"you had entered wrong UserID number ({UserID}), please try again later");
            }

            // list the user queries:
            List<JobQuery> JobQuerys = await _dbContext.JobQueries.Where(qr => qr.UserId == UserID).ToListAsync();

            // check if the user has any query related to his id or not:
            if (!JobQuerys.Any())
            {
                _logger.LogError($"The user with id ({UserID}), does not entered any queries");
                return NotFound("you should enter at least one query to perform this search");
            }

            List<ScrapedJob> DbJobs = new List<ScrapedJob>();
            // then, list the queries details (loop):
            foreach (JobQuery jobQuery in JobQuerys)
            {
                // each job query must has query details to share with the _scraper service (QueryID):
                List<ScrapedJob> jobs = await _scraper.ScrapeWebsiteAsync(jobQuery.QueryId);

                // check if the scraper fetchs any data or not:
                if (jobs != null && jobs.Count > 0)
                {
                    // add the list of scraped jobs froom the query to the big list and store it in the Scraped jobs table:
                    DbJobs.AddRange(jobs);
                }
                else
                {
                    _logger.LogWarning($"there is no any fetched jobs for this query ({jobQuery.QueryId}) that written by user ({UserID})");
                }

            }
            // if there are no found jobs at all, the endpoint should return 404 :
            if (DbJobs.Count() == 0)
            {
                _logger.LogError($"the scraper does not find any work at all for all the job queries comes from the user ({UserID})");
                return NotFound($"there is no jobs found at all for all user ({UserID}) queries");
            }


            // store the big list in the db :
            _dbContext.ScrapedJobs.AddRangeAsync(DbJobs);
            await _dbContext.SaveChangesAsync();

            // log the info then return the success value :
            _logger.LogInformation($"all queries from the user ({UserID}) had fetched data and store it successfully to the db");
            return Ok(DbJobs);
        }
        // New Endpoint: Scrape Single Query (Faster response for specific search)
        [HttpPost("ScrapeSingleQuery/v1/{QueryId}")]
        public async Task<IActionResult> ScrapeSingleQuery(int QueryId)
        {
            try
            {
                // 1. Verify Query Exists
                var query = await _dbContext.JobQueries.FindAsync(QueryId);
                if (query == null)
                {
                    return NotFound($"Query with ID {QueryId} not found.");
                }

                _logger.LogInformation($"Starting single query scrape for QueryId: {QueryId}");

                // 2. Perform Scraping
                List<ScrapedJob> scrapedJobs = await _scraper.ScrapeWebsiteAsync(QueryId);

                // 3. Save Results (if any)
                if (scrapedJobs != null && scrapedJobs.Any())
                {
                    // FIX: Ensure correct SiteId linkage to avoid FK errors
                    // Identify the scraper source (Bayt or Reed)
                    // Currently ScrapeWebsiteAsync wraps correct scraper based on logic, but BaytScraper hardcodes ID 1.
                    // We need to find the REAL ID of "Bayt.com" in the DB.
                    var baytSite = await _dbContext.JobSites.FirstOrDefaultAsync(s => s.SiteName.Contains("Bayt"));
                    var reedSite = await _dbContext.JobSites.FirstOrDefaultAsync(s => s.SiteName.Contains("Reed"));

                    foreach (var job in scrapedJobs)
                    {
                        // Fallback logic: If scraper set ID 1, map it to real Bayt ID.
                        if (job.SiteId == 1 && baytSite != null) job.SiteId = baytSite.SiteId;
                        else if (job.SiteId == 2 && reedSite != null) job.SiteId = reedSite.SiteId;
                        
                        // Safety: If no site found, assign to First available or create one? 
                        // For now, if baytSite is null, we are in trouble (Seeding failed).
                        if (baytSite == null && job.SiteId == 1) 
                        {
                            _logger.LogWarning("Critical: Bayt.com site not found in DB. Seeding issue?");
                        }
                    }

                    _dbContext.ScrapedJobs.AddRange(scrapedJobs);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation($"Successfully scraped and saved {scrapedJobs.Count} jobs for QueryId: {QueryId}");
                    
                    // Project to include SiteName for Frontend
                    var result = scrapedJobs.Select(job => new 
                    {
                        job.JobId,
                        job.JobName,
                        job.JobLocation,
                        job.JobUrl,
                        job.JobDescription,
                        job.JobSalary,
                        job.JobDatePosted,
                        job.JobNotes,
                        job.IsAvailable,
                        job.SiteId,
                        job.QueryId,
                        SiteName = (job.SiteId == baytSite?.SiteId) ? baytSite.SiteName : 
                                   (job.SiteId == reedSite?.SiteId) ? reedSite.SiteName : "Job Site"
                    });

                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning($"No jobs found for QueryId: {QueryId}");
                    return Ok(new List<ScrapedJob>()); // Return empty list instead of 404 to indicate "search completed, no results"
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error scraping single query {QueryId}");
                return StatusCode(500, "Internal server error during scraping.");
            }
        }
    }
}
