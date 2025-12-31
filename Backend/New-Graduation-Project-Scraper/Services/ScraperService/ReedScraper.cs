using ScraperAPI.Models;
using Microsoft.Playwright;
using ScraperAPI.Services.ScraperService;

namespace ScraperAPI.Services.ScraperService
{
    public class ReedScraper : IScraperService
    {
        public string ScraperName => "Reed"; // scraper name
        // inject the logger:
        private readonly ILogger<ReedScraper> _logger;
        public ReedScraper(ILogger<ReedScraper> logger)
        {
            _logger = logger;
        }

        // Scraping Method:
        public async Task<List<ScrapedJob>> ScraperAsync(JobQuery Query)
        {
            // 1. fetch the JobName and JobDetails from the query:
            int QueryID = Query.QueryId;
            string QJobName = Query.QjobName;
            string QJobLocation = Query.QjobLocation;
            
            // 2 . setup the the playwright browser:
            using var PlayWright = await Playwright.CreateAsync();
            await using var Browser = await PlayWright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = false, // can change to see what happened
                    Channel = "chrome",
                    Args = new[] 
                    {
                        // disable AutomationControlled feature that can detect playwright:
                        "--disable-blink-features=AutomationControlled", 
                        // disable browser isolation:
                        "--no-sandbox"
                    }
                }
            );
            // 3. initial the browser context with spoofed user agent:
            var Context = await Browser.NewContextAsync(
                new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                }
            );
            var page = await Context.NewPageAsync();
            // 4. create empty Jobs List to fill it up with scraped jobs & list of string job links:
            List<ScrapedJob> ScrapedJobs = new List<ScrapedJob>();
            List<string> JobLinks = new List<string>();
            // 5. put the scraping logic in try block:
            try
            {
                // 6. construct the initial search url for Reed.co.uk and navigate to it:
                string? Url = $"https://www.reed.co.uk/";
                await page.GotoAsync(Url);
                // 7. locate the accept button if exist or not:
                try 
                {
                    // Wait briefly for the banner to appear
                    await page.WaitForSelectorAsync("#onetrust-accept-btn-handler", new PageWaitForSelectorOptions { Timeout = 5000 });
                    var AcceptTermsBtn = page.Locator("#onetrust-accept-btn-handler");
                    
                    if (await AcceptTermsBtn.CountAsync() > 0 && await AcceptTermsBtn.IsVisibleAsync())
                    {
                        // 8. click to pass the test:
                        await AcceptTermsBtn.ClickAsync();
                        await Task.Delay(1000); // Wait for banner to disappear
                    }
                }
                catch { _logger.LogError($"Banner did not appear, continue");}
                // 9. locate the job search box and check if it exist or not:
                // Wait for the search box to be clickable (after banner is gone)
                try {
                    await page.WaitForSelectorAsync("#main-keywords", new PageWaitForSelectorOptions { Timeout = 10000 });
                } catch { _logger.LogWarning("Search box detection timed out"); }
                var SearchInput = page.Locator("input[id='main-keywords']").First; 
                // 10. type in the search box:
                _logger.LogInformation($"the searchbox from Reed.co.uk are now under typing process : searched topic : {QJobName}"); 
                await SearchInput.ClickAsync(); // click to activate the job search box
                await SearchInput.FillAsync(QJobName); // inject the user query in the search text
                // 11. check if the QJobLocation is empty or not:
                if (string.IsNullOrEmpty(QJobLocation))
                {
                    _logger.LogError("the job location field is empty, this field needed for Reed.co.uk");
                    return ScrapedJobs; // empty list 
                }
                // 12. locate the country selection box:
                var LocationInput = page.Locator("input[id='main-location']").First;
                // 13. check if it is empty, and try with placeholder: 
                if (await LocationInput.CountAsync() == 0)
                {
                    LocationInput = page.GetByPlaceholder("town or postcode");
                }
                await LocationInput.ClickAsync(); // activate the country selection box
                await LocationInput.ClearAsync(); // Clear existing text first
                await Task.Delay(500); // add behavioural delay to mimic the user actions
                await page.Keyboard.TypeAsync(QJobLocation); // keyboard using treck...
                await Task.Delay(1000); // add another delay 
                // 14. press enter:
                await page.Keyboard.PressAsync("Enter");
                // 15. another enter after delay to confirm the search:
                await Task.Delay(500);
                // 16. wait for the results:
                _logger.LogInformation("Search submitted. Waiting for results...");
                try {
                    await page.WaitForSelectorAsync("article.job-result", new PageWaitForSelectorOptions { Timeout = 15000 });
                } catch { _logger.LogWarning("No job cards found."); }
                // 17. scroll to trigger lazy loading:
                for (int i = 0; i < 5; i++)
                {
                    await page.Keyboard.PressAsync("PageDown"); // Keyboard is better than Mouse.Wheel here
                    await Task.Delay(1000); 
                }                
                // 18. collect job links in one collection of link elements (array):
                var LinkElements = await page.QuerySelectorAllAsync("article.job-result h3.title a");
                if (LinkElements.Count == 0) LinkElements = await page.QuerySelectorAllAsync("h2 a"); // Fallback
                foreach (var element in LinkElements)
                {
                    string? Href = await element.GetAttributeAsync("href");
                    if (!string.IsNullOrEmpty(Href))
                    {
                        // Extract full URL (if needed):
                        string FullUrl = Href.StartsWith("http") ? Href : "https://www.reed.co.uk" + Href;
                        // prevent duplication:
                        if (!JobLinks.Contains(FullUrl)) JobLinks.Add(FullUrl);
                    }
                }
                _logger.LogInformation($"Found {JobLinks.Count} links on Reed.");
                // 19. visit each job link and collect the data from it:
                int counter = 1;
                foreach (var link in JobLinks)
                {
                    try
                    {
                        // 20. mark the visited link from all grabed links:
                        _logger.LogInformation($"link ({counter}/{JobLinks.Count}): ({link})");
                        // 21. visit the link:
                        await page.GotoAsync(link);
                        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded); // dynamic timer to wait the content   
                        // 22. grab the title:
                        var TitleElement = await page.QuerySelectorAsync("header h1") ?? await page.QuerySelectorAsync("h1");
                        string JobTitle = TitleElement != null ? await TitleElement.InnerTextAsync() : QJobName ?? "Unknown";
                        // 23. grab the job description:
                        var DescriptionElement = await page.QuerySelectorAsync("div[class*='description']") ?? await page.QuerySelectorAsync("#jobDescription");
                        string JobDescription = DescriptionElement != null ? await DescriptionElement.InnerTextAsync() : "description does not found";
                        // 24. grab the job location:
                        var LocationItem = await page.QuerySelectorAsync("span[itemprop='addressLocality']") ?? await page.QuerySelectorAsync("div[class*='location'] span");
                        string LocationValue = LocationItem != null ? await LocationItem.InnerTextAsync() : QJobLocation;
                        // 25. replace the dot with comma in JobLocation value (bayt manipulation):
                        if (LocationValue.Contains("."))
                        {
                            LocationValue = LocationValue.Replace(".", ",").Trim();
                        }
                        // 26. add the job to the scraped jobs list:
                        ScrapedJobs.Add(new ScrapedJob
                        {
                            JobName = JobTitle.Trim(),
                            JobUrl = link,
                            JobLocation = LocationValue.Trim(),
                            JobDescription = JobDescription.Trim(), 
                            SiteId = 7, // Ensure this matches your DB ID for Reed
                            IsAvailable = true,
                            QueryId = QueryID,
                            JobNotes = "Deep Scraped: Full Details Fetched from Reed.co.uk site"
                        });
                        // 27. update the counter and add delay:
                        counter++;
                        await Task.Delay(1500); // the delay can be change to access more links in less time 
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Reed Scraper Critical Error : {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to scrape data from Reed.co.uk: {ex.Message}");
            }
            //28. close the browser (automatic baeause of (using) keyword in browser implementation) and return the jobs:
            return ScrapedJobs;
        }
    }
}