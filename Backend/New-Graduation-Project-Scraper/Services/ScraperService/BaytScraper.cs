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
                // 7. locate the job search box from bayt.com
                var SearchInput = page.Locator("input[id='text_search']").First; 
                if (await SearchInput.CountAsync() == 0)
                {
                    // try another way (by placeholders):
                    SearchInput = page.GetByPlaceholder("Search jobs, skills, companies").First;
                }

                // 8. Type in the search box - Strategy: Combined "Keyword Location" for best relevance
                string combinedQuery = string.IsNullOrWhiteSpace(QJobLocation) ? QJobName : $"{QJobName} {QJobLocation}";
                _logger.LogInformation($"Typing combined query in search box: {combinedQuery}"); 
                await SearchInput.FillAsync(combinedQuery);
                
                // 9. Submit Search
                _logger.LogInformation("Submitting search...");
                await SearchInput.PressAsync("Enter");
                
                // 10. Wait for results to load
                _logger.LogInformation("Waiting for job listings...");
                try 
                {
                    // Increase timeout for initial search result load
                    await page.WaitForSelectorAsync("#results_inner_card, .t-regular, li.has-pointer-d", new PageWaitForSelectorOptions { Timeout = 60000 }); 
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
                        
                        // 27. grab the job location early to validate:
                        var LocationItem = await page.QuerySelectorAsync("ul.list.is-basic li span.t-mute")
                                         ?? await page.QuerySelectorAsync(".job-detail-header .t-mute");
                        string LocationValue = LocationItem != null ? await LocationItem.InnerTextAsync() : "Unknown";
                        
                        // Client-Side Filtering: STRICT LOCATION CHECK
                        if (!string.IsNullOrWhiteSpace(QJobLocation))
                        {
                            // If user asked for a location, and this job is NOT in that location, SKIP IT.
                            if (!LocationValue.Contains(QJobLocation, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation($"Skipping job '{link}' - Location '{LocationValue}' does not match requested '{QJobLocation}'");
                                counter++;
                                continue;
                            }
                        }

                        // 25. grab the title:
                        var TitleElement = await page.QuerySelectorAsync("#job_title") 
                                         ?? await page.QuerySelectorAsync("h1")
                                         ?? await page.QuerySelectorAsync(".job-view-header h1");
                                         
                        string JobTitle = TitleElement != null ? await TitleElement.InnerTextAsync() : null;
                        
                        if (string.IsNullOrWhiteSpace(JobTitle))
                        {
                            // Fallback: If we can't find title on the page, use a generic one or try to infer from metadata
                            _logger.LogWarning($"Could not find Job Title on page: {link}. Using Search Query Name as fallback.");
                            JobTitle = QJobName; 
                        }

                        // 26. grab the job description:
                        // Fix typo: job-descripton -> job-description
                        var DescriptionElement = await page.QuerySelectorAsync("div.job-description") 
                                               ?? await page.QuerySelectorAsync("div.t-break")
                                               ?? await page.QuerySelectorAsync("#job_description");
                                               
                        string JobDescription = DescriptionElement != null ? await DescriptionElement.InnerTextAsync() : "Description not found";
                        
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