using ScraperAPI.Models;
using Microsoft.Playwright;

namespace ScraperAPI.Services.ScraperService
{
    public class BaytScraper : IScraperService
    {
        public string ScraperName => "Bayt"; // identify the scraper name
        // inject the logger:
        private readonly ILogger<BaytScraper> _logger;
        public BaytScraper(ILogger<BaytScraper> logger)
        {
            _logger = logger;
        }

        // add the scraping task:
        public async Task<List<ScrapedJob>> ScraperAsync(JobQuery Query)
        {
            // the main ScrapingEngine will check the Query so here we assume that the query is perfect:
            // 1. fetch the JobName and JobDetails from the query:
            int QueryID = Query.QueryId;
            string QJobName = Query.QjobName;
            string QJobLocation = Query.QjobLocation;
            // 2 . setup the the playwright browser:
            var PlayWright= await Playwright.CreateAsync();
            var Browser = await PlayWright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = true, // can change to see what happened
                    Channel= "chrome",
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
            // 5. construct the initial search url for bayt.com and navigate to it:
            string? Url = $"https://www.bayt.com/en/";
            await page.GotoAsync(Url);
            await page.WaitForSelectorAsync(".fixed-nav", new PageWaitForSelectorOptions { Timeout = 40000 }); // wait for the body in bayt to appear
            // 6. put the scraping method in try block:
            try
            {
                // 7. locate the job search box from bayt.com and check if it exist or not:
                var SearchInput = page.Locator("input[id='text_search']").First; 
                if (await SearchInput.CountAsync() == 0)
                {
                    // try another way (by placeholders):
                    SearchInput = page.GetByPlaceholder("Search jobs, skills, companies").First;
                }
                // 8. type in the search box:
                _logger.LogInformation($"the searchbox from bayt.com are now under typing process : searched topic : {QJobName}"); 
                // 7. Construct Direct Search URL (Permanent Solution)
                // Pattern: https://www.bayt.com/en/international/jobs/?keyword={name}&country={location}
                string baseUrl = "https://www.bayt.com/en/international/jobs/";
                string searchUrl = $"{baseUrl}?keyword={Uri.EscapeDataString(QJobName)}";
                
                if (!string.IsNullOrWhiteSpace(QJobLocation))
                {
                    // If location is provided, append it. 
                    // Note: Bayt usually filters by country code in the path, but query param 'country' or 'filters[locations][]' 
                    // works in broad searches or we can rely on the 'international' endpoint to handle keywords + location text.
                    // A more robust way for Bayt is simple keyword search which often catches location too, 
                    // or appending it to the keyword if specific filters fail.
                    // Let's try appending location to keyword for broader match if specific param fails, 
                    // but standard 'keyword' usually works best.
                    searchUrl += $"&locations[]={Uri.EscapeDataString(QJobLocation)}";
                }

                _logger.LogInformation($"Navigating directly to search URL: {searchUrl}");

                // 8. Navigate directly to results
                await page.GotoAsync(searchUrl, new PageGotoOptions { Timeout = 60000 });

                // 9. Wait for results to load
                _logger.LogInformation("Waiting for job listings...");
                try 
                {
                    // Wait for the specific job list container or "No results" message
                    await page.WaitForSelectorAsync("#results_inner_card, .t-regular", new PageWaitForSelectorOptions { Timeout = 30000 }); 
                }
                catch
                {
                    _logger.LogWarning("Timeout waiting for results selector. Page might have loaded differently.");
                } 
                // 17. scroll to trigger lazy loading:
                for (int i = 0; i < 5; i++)
                {
                    await page.Mouse.WheelAsync(0, 3000);
                    await Task.Delay(1500); 
                }
                // 18. select the jobs collection as array of objects with backup step:
                var ListItems = await page.QuerySelectorAllAsync("li.has-pointer-d");
                var CardItems = await page.QuerySelectorAllAsync("div.card"); // the backup
                var AllItems = ListItems.Concat(CardItems).ToList();
                _logger.LogInformation($"found ({AllItems.Count}) potintail jobs from bayt.com site");
                foreach (var item in AllItems)
                {
                    // 19. identify the link:
                    var TitleElement = await item.QuerySelectorAsync("h2 a") ?? await item.QuerySelectorAsync("a.jb-title");
                    if (TitleElement != null)
                    {
                        // 20. fetch the job links:
                        string? Href = await TitleElement.GetAttributeAsync("href");
                        if (!string.IsNullOrEmpty(Href))
                        {
                            string FullUrl = Href.StartsWith("http") ? Href : "https://www.bayt.com" + Href;
                            // avoid duplicates:
                            if (!JobLinks.Contains(FullUrl)) 
                            {
                                // 21. add the link to the links list:
                                JobLinks.Add(FullUrl);
                            }
                        }
                    }
                }
                _logger.LogInformation($"phase 1 complete: collected ({JobLinks.Count}) unique job links.");
                // 22. visit each job link and collect the data from it:
                int counter = 1;
                foreach (var link in JobLinks)
                {
                    try
                    {
                        // 23. mark the visited link from all grabed links:
                        _logger.LogInformation($"link ({counter}/{JobLinks.Count}): ({link})");
                        // 24. visit the link:
                        await page.GotoAsync(link);
                        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded); // dynamic timer to wait the content
                        // 25. grab the title:
                        var TitleElement = await page.QuerySelectorAsync("h1.job_title");
                        string JobTitle = TitleElement != null ? await TitleElement.InnerTextAsync() : QJobName ?? "Unknown";
                        // 26. grab the job description:
                        var DescriptionElement = await page.QuerySelectorAsync("div.t-break") ?? await page.QuerySelectorAsync("div.job-descripton");
                        string JobDescription = DescriptionElement != null ? await DescriptionElement.InnerTextAsync() : "description does not found";
                        // 27. grab the job location:
                        var LocationItem = await page.QuerySelectorAsync("ul.list.is-basic li span.t-mute");
                        string LocationValue = LocationItem != null ? await LocationItem.InnerTextAsync() : QJobLocation ?? "Unknown";
                        // 28. replace the dot with comma in JobLocation value (bayt manipulation):
                        if (LocationValue.Contains("."))
                        {
                            LocationValue = LocationValue.Replace(".", ",").Trim();
                        }
                        // 29. add the job to the scraped jobs list:
                        ScrapedJobs.Add(new ScrapedJob
                        {
                            JobName = JobTitle.Trim(),
                            JobUrl = link,
                            JobLocation = LocationValue.Trim(),
                            JobDescription = JobDescription.Trim(), 
                            SiteId = 1,
                            IsAvailable = true,
                            QueryId = QueryID,
                            JobNotes = "Deep Scraped: Full Details Fetched from Bayt.com site"
                        });
                        // 30. update the counter and add delay:
                        counter++;
                        await Task.Delay(900); // the delay can be change to access more links in less time 
                        
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to scrape deep link {link}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to scrape data from Bayt.com: {ex.Message}");
            }
            //31. close the browser and return the jobs:
            await Browser.CloseAsync();
            return ScrapedJobs;
        }

    }
}