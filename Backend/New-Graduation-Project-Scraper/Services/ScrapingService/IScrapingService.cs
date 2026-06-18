using Microsoft.AspNetCore.Mvc;
using ScraperAPI.Models;
using WebApplication1.Models;

namespace ScraperAPI.Services.Scraping_Service
{
    public interface IScrapingService
    {
        public Task<List<ScrapedJob>> ScrapeWebsiteAsync(int QueryID);
        public Task<List<ScrapedJob>> ScrapeWebsiteAsyncV2(int QueryID);
    }
}
