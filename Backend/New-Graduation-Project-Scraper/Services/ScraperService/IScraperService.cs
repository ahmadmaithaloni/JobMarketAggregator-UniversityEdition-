using ScraperAPI.Models;

namespace ScraperAPI.Services.ScraperService

{
    public interface IScraperService
    {
        string ScraperName {get;} // identify the scraper name (bayt, tanqeeb, randstad ...)
        public Task<List<ScrapedJob>> ScraperAsync(JobQuery Query); // accept the db object Query itself
    }
}