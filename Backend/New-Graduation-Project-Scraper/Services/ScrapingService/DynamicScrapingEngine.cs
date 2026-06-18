using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using ScraperAPI.Models;
using ScraperAPI.Services.LocationMapper_Service;
using ScraperAPI.Services.ScraperService;
using System.Diagnostics.Metrics;
using WebApplication1.Models;

namespace ScraperAPI.Services.Scraping_Service
{
    public class DynamicScrapingEngine : IScrapingService
    {
        private readonly ScrapingEngineDbContext _dbContext;
        private readonly ILogger<DynamicScrapingEngine> _logger;
        private readonly ILocationMapperService _locationMapperService;
        private readonly IEnumerable<IScraperService> _scraper;

        public DynamicScrapingEngine(ScrapingEngineDbContext dbContext, ILogger<DynamicScrapingEngine> logger, ILocationMapperService locationMapper, IEnumerable<IScraperService> scraper)
        {
            _dbContext = dbContext;
            _logger = logger;
            _locationMapperService = locationMapper;
            _scraper = scraper;
        }

        // Primary Scraping Logic (Strategy Pattern)
        public async Task<List<ScrapedJob>> ScrapeWebsiteAsync(int QueryID)
        {
            // Validation Phase 1: Check Query ID
            if (QueryID < 0)
            {
                _logger.LogError($"The query id ({QueryID}) is not valid.");
                return new List<ScrapedJob>();
            }

            // Validation Phase 2: Check Query Existence
            JobQuery? Query = await _dbContext.JobQueries.FindAsync(QueryID);
            if (Query == null)
            {
                _logger.LogError($"Query {QueryID} does not exist in the queries table");
                return new List<ScrapedJob>();
            }

            string? JobLocation = Query.QjobLocation;
            
            // 2. Create empty Jobs List
            List<ScrapedJob> ScrapedJobs = new List<ScrapedJob>();

            // 3. Identify the active scraper based on User Query Location
            string? Country = JobLocation != null ? _locationMapperService?.MapLocationToCountry(JobLocation) : null; 
            string? ScraperKey = JobLocation != null ? _locationMapperService?.GetTargetScraperKey(JobLocation) : null; 

            // 4. Select and Execute Scraper Strategy
            _logger.LogInformation($"Resolved Scraper Key: {ScraperKey}, Country: {Country}");
            
            IScraperService? SelectedScraper = null;

            if (ScraperKey == "MENA")
            {
                SelectedScraper = _scraper.FirstOrDefault(s => s.ScraperName == "bayt");
            }
            else if (ScraperKey == "UK")
            {
                SelectedScraper = _scraper.FirstOrDefault(s => s.ScraperName == "Reed");
            }

            if (SelectedScraper == null)
            {
                _logger.LogWarning($"No scraper strategy found for Key: {ScraperKey}");
                return ScrapedJobs;
            }

            _logger.LogInformation($"Executing Scraper Strategy: {SelectedScraper.ScraperName}");
            ScrapedJobs = await SelectedScraper.ScraperAsync(Query);
            
            return ScrapedJobs;
        }

        // Removed legacy V2 method signature as it is now the primary method.
        public Task<List<ScrapedJob>> ScrapeWebsiteAsyncV2(int QueryID) => ScrapeWebsiteAsync(QueryID);
    }
}
