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
                    Headless = false, // can change to see what happened
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
                // 7. locate the job search box (Keyword)
                var SearchInput = page.Locator("input[id='text_search']").First; 
                if (await SearchInput.CountAsync() == 0)
                {
                    SearchInput = page.GetByPlaceholder("Search jobs, skills, companies").First;
                }

                // 8. Locate the Location box
                // Strategy: Common placeholders or Name attributes for Bayt
                var LocationInput = page.Locator("input[id='search_country__r']").First; // Try by ID first
                if (await LocationInput.CountAsync() == 0)
                {
                    // Fallback to placeholders if ID fails
                    LocationInput = page.GetByPlaceholder("City, country or region").First; 
                }
                // 9. Fill Inputs
                _logger.LogInformation($"Filling Search Form: Keyword='{QJobName}', Location='{QJobLocation}'");
                await SearchInput.FillAsync(QJobName);
                //await SearchInput.PressAsync("Tab");
                if (!string.IsNullOrWhiteSpace(QJobLocation) && await LocationInput.CountAsync() > 0)
                {
                    await LocationInput.ClickAsync();
                    //await LocationInput.ClearAsync();
                    await page.Keyboard.TypeAsync(QJobLocation, new KeyboardTypeOptions { Delay = 100 });
                    
                    // Wait for the dropdown suggestion to appear
                    try {
                        await page.WaitForSelectorAsync("ul.options is-autosize, .autocomplete-suggestion", new PageWaitForSelectorOptions { Timeout = 5000 });
                        await page.Keyboard.PressAsync("ArrowDown");
                        await page.Keyboard.PressAsync("Enter");
                    } catch {
                        await LocationInput.PressAsync("Enter");
                    }
                    await Task.Delay(1000); 
                }
                
                // 10. Submit Search explicitly via Button
                _logger.LogInformation("Submitting search via Button...");
                
                // Try to find the search button
                var searchButton = page.Locator("button[data-js-id='search-button'], button.is-primary, button[type='submit']").First;
                
                if (await searchButton.CountAsync() > 0 && await searchButton.IsVisibleAsync())
                {
                    await searchButton.ClickAsync();
                }
                else
                {
                    // Fallback to Enter ONLY if button is missing
                    _logger.LogWarning("Search button not found, falling back to Enter key on Keyword field.");
                    await SearchInput.PressAsync("Enter");
                }

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
                // 17. Pagination Loop
                int pagesScraped = 0;
                int maxPages = 2; // User requested limit
                bool hasNextPage = true;

                while (hasNextPage && pagesScraped < maxPages)
                {
                    pagesScraped++;
                    _logger.LogInformation($"Scraping page {pagesScraped}...");

                    // Scroll to trigger any lazy loading
                    await page.Mouse.WheelAsync(0, 3000);
                    await Task.Delay(1000);

                    // Collect Jobs from Current Page
                    var ListItems = await page.QuerySelectorAllAsync("li.has-pointer-d");
                    var CardItems = await page.QuerySelectorAllAsync("div.card[data-js-job-id]"); // Improved selector
                    var AllItems = ListItems.Concat(CardItems).ToList();

                    _logger.LogInformation($"Found ({AllItems.Count}) potential jobs on page {pagesScraped}");

                    foreach (var item in AllItems)
                    {
                        var TitleElement = await item.QuerySelectorAsync("h2 a") ?? await item.QuerySelectorAsync("a.jb-title");
                        if (TitleElement != null)
                        {
                            string? Href = await TitleElement.GetAttributeAsync("href");
                            if (!string.IsNullOrEmpty(Href))
                            {
                                string FullUrl = Href.StartsWith("http") ? Href : "https://www.bayt.com" + Href;
                                if (!JobLinks.Contains(FullUrl))
                                {
                                    JobLinks.Add(FullUrl);
                                }
                            }
                        }
                    }

                    // Check for Next Button
                    // Bayt usually has a pagination section at the bottom
                    // Selector might be: a.pagination-next or similar. 
                    // Let's look for a generic "next" link or specific class.
                    var nextButton = await page.QuerySelectorAsync("a[data-js-id='pagination-next']"); 
                    
                    // Fallback selectors if ID changes
                    if (nextButton == null) nextButton = await page.QuerySelectorAsync("a.js-pagination-next");
                    if (nextButton == null) nextButton = await page.QuerySelectorAsync("li.pagination-next a");

                    if (nextButton != null && await nextButton.IsVisibleAsync())
                    {
                        string nextUrl = await nextButton.GetAttributeAsync("href");
                        if (!string.IsNullOrEmpty(nextUrl) && nextUrl != "#")
                        {
                            try 
                            {
                                _logger.LogInformation("Navigating to next page...");
                                // Force click to bypass potential overlays/loaders
                                // FIX: Use ElementHandleClickOptions instead of LocatorClickOptions because nextButton is IElementHandle
                                await nextButton.ClickAsync(new ElementHandleClickOptions { Force = true, Timeout = 5000 });
                                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                                await Task.Delay(2000); // Wait for results to render
                            }
                            catch (Exception navEx)
                            {
                                _logger.LogWarning($"Pagination navigation failed: {navEx.Message}. Stopping pagination.");
                                hasNextPage = false;
                            }
                        }
                        else
                        {
                            hasNextPage = false;
                        }
                    }
                    else
                    {
                        hasNextPage = false;
                        _logger.LogInformation("No next page found.");
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
                        var TitleElement = await page.QuerySelectorAsync("#job_title") 
                                         ?? await page.QuerySelectorAsync("h1")
                                         ?? await page.QuerySelectorAsync(".job-view-header h1");
                                         
                        string? JobTitle = TitleElement != null ? await TitleElement.InnerTextAsync() : null;
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
                        // --- New: Extract Salary and Date Posted ---
                        string? JobSalary = "Not Specified";
                        string? JobDatePosted = "Not Specified";
                        // Try to find the list of details (usually contains Location, Company, Date, Salary)
                        var MetaListItems = await page.QuerySelectorAllAsync("ul.list.is-basic li");
                        // 1. Iterate through items to find keywords
                        foreach (var metaItem in MetaListItems)
                        {
                            var text = await metaItem.InnerTextAsync();
                            if (string.IsNullOrWhiteSpace(text)) continue;

                            if (text.Contains("Date Posted", StringComparison.OrdinalIgnoreCase))
                            {
                                JobDatePosted = text.Replace("Date Posted:", "").Trim();
                            }
                            else if (text.Contains("Monthly Salary", StringComparison.OrdinalIgnoreCase))
                            {
                                JobSalary = text.Replace("Monthly Salary:", "").Trim();
                            }
                        }
                        // Fallback: Check for dl/dt structure if ul list fails or for specific templates
                        if (JobSalary == "Not Specified")
                        {
                            var salaryElement = await page.QuerySelectorAsync("dt:has-text('Monthly Salary') + dd");
                            if (salaryElement != null) JobSalary = await salaryElement.InnerTextAsync();
                        }
                        if (JobDatePosted == "Not Specified")
                        {
                            var dateElement = await page.QuerySelectorAsync("dt:has-text('Date Posted') + dd");
                            if (dateElement != null) JobDatePosted = await dateElement.InnerTextAsync();
                        }
                        // 28. replace the dot with comma in JobLocation value (bayt manipulation):
                        if (LocationValue.Contains("."))
                        {
                            LocationValue = LocationValue.Replace(".", ",").Trim();
                        }
                        ScrapedJobs.Add(new ScrapedJob
                        {
                            JobName = JobTitle.Trim(),
                            JobUrl = link,
                            JobLocation = LocationValue.Trim(),
                            JobDescription = JobDescription.Trim(), 
                            JobSalary = JobSalary,
                            JobDatePosted = JobDatePosted,
                            SiteId = 1, // RE-VERIFY: Seeding MUST ensure ID 1 is Bayt.
                            IsAvailable = true,
                            QueryId = QueryID,
                            JobNotes = "Deep Scraped: Full Details Fetched from Bayt.com site with pagination feature",
                            // CreationDate = DateTime.Now // REMOVED to prevent crash
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