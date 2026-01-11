using ScraperAPI.Models;
using Microsoft.Playwright;
using ScraperAPI.Services.LocationMapper_Service;
using WebApplication1.Models;
using Microsoft.EntityFrameworkCore;

namespace ScraperAPI.Services.ScraperService
{
    public class BaytScraperV2 : IScraperService
    {
        public string ScraperName => "bayt";
        // inject the logger:
        private readonly ILogger<BaytScraperV2> _logger;
        private readonly ILocationMapperService _location;
        private readonly ScrapingEngineDbContext _dbContext;
        public BaytScraperV2(ILogger<BaytScraperV2> logger, ILocationMapperService location, ScrapingEngineDbContext dbContext)
        {
            _logger = logger;
            _location = location;
            _dbContext = dbContext;
        }

        // scraping task:
        public async Task<List<ScrapedJob>> ScraperAsync(JobQuery Query)
        {
            // job query details:
            int QueryID = Query.QueryId;
            string QJobName = Query.QjobName;
            string QJobLocation = Query.QjobLocation;

            // specify the country from the location:
            string CountryName = _location.MapLocationToCountry(QJobLocation);

            // playwright browser setup:
            var PlayWright= await Playwright.CreateAsync();
            var Browser = await PlayWright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = false, 
                    Channel= "chrome",
                    Args = new[] 
                    {
                        "--disable-blink-features=AutomationControlled", 
                        "--no-sandbox"
                    }
                }
            );

            // initial the browser context with spoofed user agent:
            var Context = await Browser.NewContextAsync(
                new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                }
            );
            var page = await Context.NewPageAsync();

            // create empty Jobs List to fill it up with scraped jobs & list of string job links:
            List<ScrapedJob> ScrapedJobs = new List<ScrapedJob>();
            List<string> JobLinks = new List<string>();

            // try the direct path strategy:
            // Ensure spaces are replaced with hyphens for Bayt URL structure
            string CleanCountry = Uri.EscapeDataString(CountryName.ToLower()).Replace("%20", "-");
            string CleanJob = Uri.EscapeDataString(QJobName.ToLower()).Replace("%20", "-");
            string? Url = $"https://www.bayt.com/en/{CleanCountry}/jobs/{CleanJob}-jobs/";

            // navigate to the page:
            await page.GotoAsync(Url);

             _logger.LogInformation("Waiting for job listings...");
            try 
            {
                await page.WaitForSelectorAsync("#results_inner_card, li.has-pointer-d", new PageWaitForSelectorOptions { Timeout = 30000 }); 
            }
            catch
            {
                _logger.LogWarning("Timeout waiting for results selector.");
            }

            // 17. Pagination Loop
            int pagesScraped = 0;
            int maxPages = 2; // FIXED: strictly 2 pages as requested
            bool hasNextPage = true;

            while (hasNextPage && pagesScraped < maxPages)
            {
                pagesScraped++;
                _logger.LogInformation($"Scraping page {pagesScraped}...");

                await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
                await Task.Delay(1000);

                // Collect Jobs from Current Page
                var AllItems = await page.QuerySelectorAllAsync("li.has-pointer-d, div.card[data-js-job-id]");

                foreach (var item in AllItems)
                {
                    // FIXED: limit the collection to 60 jobs strictly
                    if (JobLinks.Count >= 60) break;

                    var TitleElement = await item.QuerySelectorAsync("h2 a") ?? await item.QuerySelectorAsync("a.jb-title");
                    if (TitleElement != null)
                    {
                        string? Href = await TitleElement.GetAttributeAsync("href");
                        if (!string.IsNullOrEmpty(Href))
                        {
                            string FullUrl = Href.StartsWith("http") ? Href : "https://www.bayt.com" + Href;
                            if (!JobLinks.Contains(FullUrl)) JobLinks.Add(FullUrl);
                        }
                    }
                }

                if (pagesScraped < maxPages && JobLinks.Count < 60)
                {
                    var nextButton = await page.QuerySelectorAsync("li.pagination-next a") 
                                  ?? await page.QuerySelectorAsync("a[data-js-id='pagination-next']");

                    if (nextButton != null && await nextButton.IsVisibleAsync())
                    {
                        await nextButton.ClickAsync(new ElementHandleClickOptions { Force = true });
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        await Task.Delay(2000);
                    }
                    else hasNextPage = false;
                }
                else hasNextPage = false;
            }

            _logger.LogInformation($"phase 1 complete: collected ({JobLinks.Count}) unique job links.");

            // 22. visit each job link and collect the data from it:
            int counter = 1;
            // FIXED: Await the async call and access correct SiteId property
            var site = await _dbContext.JobSites.FirstOrDefaultAsync(w => w.SiteName.Contains("Bayt"));
            int WebsiteID = site?.SiteId ?? 1; // Default to 1 or handle error
            foreach (var link in JobLinks)
            {
                try
                {
                    _logger.LogInformation($"link ({counter}/{JobLinks.Count}): ({link})");
                    
                    // FIXED: reduced timeout and added "wait until commit" to avoid long hangs
                    await page.GotoAsync(link, new PageGotoOptions { 
                        WaitUntil = WaitUntilState.DOMContentLoaded, 
                        Timeout = 15000 // Reduced from 30000 to fail faster if site is slow
                    });
                    
                    var LocationItem = await page.QuerySelectorAsync("ul.list.is-basic li span.t-mute")
                                     ?? await page.QuerySelectorAsync(".job-detail-header .t-mute");
                    string LocationValue = LocationItem != null ? await LocationItem.InnerTextAsync() : "Unknown";

                    var TitleElement = await page.QuerySelectorAsync("#job_title") ?? await page.QuerySelectorAsync("h1");
                    string? JobTitle = TitleElement != null ? await TitleElement.InnerTextAsync() : QJobName;

                    var DescriptionElement = await page.QuerySelectorAsync("div.job-description, div.t-break");  
                    string JobDescription = DescriptionElement != null ? await DescriptionElement.InnerTextAsync() : "Description not found";

                    string? JobSalary = "Not Specified";
                    string? JobDatePosted = "Not Specified";

                    // STRATEGY 1: JSON-LD (Structured Data) - The "Pro" Way
                    try 
                    {
                        var jsonLd = await page.EvaluateAsync<string>(@"() => {
                            const script = document.querySelector('script[type=""application/ld+json""]');
                            return script ? script.innerText : null;
                        }");

                        if (!string.IsNullOrEmpty(jsonLd))
                        {
                            using (var doc = System.Text.Json.JsonDocument.Parse(jsonLd))
                            {
                                if (doc.RootElement.TryGetProperty("datePosted", out var dateElement))
                                {
                                    JobDatePosted = dateElement.GetString(); // Returns "2025-01-11"
                                    _logger.LogInformation($"JSON-LD Date Found: {JobDatePosted}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"JSON-LD parsing failed: {ex.Message}");
                    }

                    // Fallback to Visual Scraping if JSON-LD failed
                    if (JobDatePosted == "Not Specified")
                    {
                         var MetaListItems = await page.QuerySelectorAllAsync("ul.list.is-basic li");
                         foreach (var metaItem in MetaListItems)
                         {
                             var text = await metaItem.InnerTextAsync();
                             if (text.Contains("Date Posted")) JobDatePosted = text.Replace("Date Posted:", "").Trim();
                             else if (text.Contains("Monthly Salary")) JobSalary = text.Replace("Monthly Salary:", "").Trim();
                         }
                    }

                    // Dynamic Status Logic: Mark as Closed if older than 1 year
                    bool isJobActive = true;
                    if (DateTime.TryParse(JobDatePosted, out DateTime parsedDate))
                    {
                        if (parsedDate < DateTime.Now.AddYears(-1))
                        {
                            isJobActive = false;
                        }
                    }

                    ScrapedJobs.Add(new ScrapedJob
                    {
                        JobName = JobTitle.Trim(),
                        JobUrl = link,
                        JobLocation = LocationValue.Trim(),
                        JobDescription = JobDescription.Trim(), 
                        JobSalary = JobSalary,
                        JobDatePosted = JobDatePosted, // Now holds "2025-01-11" or fallback

                        SiteId = WebsiteID,
                        IsAvailable = isJobActive,
                        QueryId = QueryID,
                        JobNotes = "Deep Scraped via Direct Link strategy",
                    });

                    counter++;
                }
                catch (Exception ex)
                {
                    // FIXED: log failure and continue immediately to next job
                    _logger.LogError($"Timeout/Error on link {counter}: skipping to next...");
                }
            }

            await Browser.CloseAsync();
            return ScrapedJobs;
        }
    }
}