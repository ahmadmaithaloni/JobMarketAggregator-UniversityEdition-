using ScraperAPI.Models;
using Microsoft.Playwright;
using ScraperAPI.Services.ScraperService;
using WebApplication1.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ScraperAPI.Services.ScraperService
{
    public class ReedScraper : IScraperService
    {
        public string ScraperName => "Reed";
        private readonly ILogger<ReedScraper> _logger;
        private readonly ScrapingEngineDbContext _dbContext;

        public ReedScraper(ILogger<ReedScraper> logger, ScrapingEngineDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<List<ScrapedJob>> ScraperAsync(JobQuery Query)
        {
            // 1. Setup
            int QueryID = Query.QueryId;
            string QJobName = Query.QjobName;
            string QJobLocation = Query.QjobLocation;

            // 2. Browser Lauch
            using var PlayWright = await Playwright.CreateAsync();
            await using var Browser = await PlayWright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true, 
                Channel = "chrome",
                Args = new[] { "--disable-blink-features=AutomationControlled", "--no-sandbox" }
            });

            var Context = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });
            var page = await Context.NewPageAsync();

            List<ScrapedJob> ScrapedJobs = new List<ScrapedJob>();
            List<string> JobLinks = new List<string>();

            try
            {
                // 3. Direct URL Navigation (V2 Style)
                // Clean inputs: replace spaces with hyphens, lowercase
                string cleanJob = Uri.EscapeDataString(QJobName.ToLower().Trim()).Replace("%20", "-").Replace(" ", "-");
                string cleanLoc = Uri.EscapeDataString(QJobLocation.ToLower().Trim()).Replace("%20", "-").Replace(" ", "-");
                
                string Url = $"https://www.reed.co.uk/jobs/{cleanJob}-jobs-in-{cleanLoc}";
                _logger.LogInformation($"Navigating to Reed URL: {Url}");
                
                await page.GotoAsync(Url);

                // Handle Cookie Banner (if present)
                try 
                {
                     await page.WaitForSelectorAsync("#onetrust-accept-btn-handler", new PageWaitForSelectorOptions { Timeout = 4000 });
                     if (await page.IsVisibleAsync("#onetrust-accept-btn-handler"))
                        await page.ClickAsync("#onetrust-accept-btn-handler");
                }
                catch {}

                // 4. Pagination & Link Collection Strategy (Direct URL Navigation)
                int maxPages = 2; // Exact user request: "not more than 2 sites" and "at least 50 jobs" (25*2)
                
                string baseSearchUrl = page.Url;
                if (baseSearchUrl.Contains("?")) baseSearchUrl = baseSearchUrl.Split('?')[0];

                for (int i = 1; i <= maxPages; i++)
                {
                    string pageUrl = i == 1 ? baseSearchUrl : $"{baseSearchUrl}?pageno={i}";
                    _logger.LogInformation($"Scraping Reed Page {i}: {pageUrl}");

                    try 
                    {
                        if (page.Url != pageUrl)
                        {
                            await page.GotoAsync(pageUrl, new PageGotoOptions { Timeout = 15000, WaitUntil = WaitUntilState.DOMContentLoaded });
                        }

                        // A. Anti-Freeze: Dynamic Wait
                        try {
                            await page.WaitForSelectorAsync("a[href*='/jobs/']", new PageWaitForSelectorOptions { Timeout = 5000 });
                        } catch { 
                            _logger.LogWarning($"Page {i} load wait timeout (proceeding anyway).");
                        }

                        // B. Scroll for Lazy Loading
                        await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
                        await Task.Delay(1000);

                        // C. Robust Link Collection
                        var allAnchors = await page.QuerySelectorAllAsync("a");
                        int newLinksCount = 0;
                        foreach (var anchor in allAnchors)
                        {
                            if(JobLinks.Count >= 60) break; // Buffer limit

                            string? Href = await anchor.GetAttributeAsync("href");
                            if (!string.IsNullOrEmpty(Href) && Href.Contains("/jobs/") && !Href.Contains("/jobs/search"))
                            {
                                if (Regex.IsMatch(Href, @"/jobs/[^/]+/\d+"))
                                {
                                    string FullUrl = Href.StartsWith("http") ? Href : "https://www.reed.co.uk" + Href;
                                    if (!JobLinks.Contains(FullUrl)) 
                                    {
                                        JobLinks.Add(FullUrl);
                                        newLinksCount++;
                                    }
                                }
                            }
                        }
                        _logger.LogInformation($"Page {i} yielded {newLinksCount} new jobs. Total: {JobLinks.Count}");
                        
                        // If no new links found on page 2, break (end of results)
                        if (i > 1 && newLinksCount == 0) break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error on Page {i}: {ex.Message}");
                    }
                }

                _logger.LogInformation($"Collected {JobLinks.Count} job links.");

                // 5. Scrape Details (JSON-LD Extraction)
                int counter = 1;
                // Get Site ID
                var site = await _dbContext.JobSites.FirstOrDefaultAsync(w => w.SiteName.Contains("Reed"));
                int WebsiteID = site?.SiteId ?? 2;

                foreach (var link in JobLinks)
                {
                    try
                    {
                        _logger.LogInformation($"Scraping ({counter}/{JobLinks.Count}): {link}");
                        await page.GotoAsync(link, new PageGotoOptions { Timeout = 20000, WaitUntil = WaitUntilState.DOMContentLoaded });

                        // Default Values
                        string JobTitle = QJobName; 
                        string JobDescription = "Description not found";
                        string LocationValue = QJobLocation;
                        string JobDatePosted = "Not Specified";
                        string JobSalary = "Not Specified";
                        string JobSkills = "See description"; // Will try to extract

                        string? jsonLd = null;

                        // A. Try JSON-LD Extraction
                        try 
                        {
                            jsonLd = await page.EvaluateAsync<string>(@"() => {
                                const scripts = document.querySelectorAll('script[type=""application/ld+json""]');
                                for(let s of scripts) {
                                    if(s.innerText.includes('JobPosting')) return s.innerText;
                                }
                                return null;
                            }");

                            if (!string.IsNullOrEmpty(jsonLd))
                            {
                                using (var doc = System.Text.Json.JsonDocument.Parse(jsonLd))
                                {
                                    var root = doc.RootElement;
                                    
                                    // Title
                                    if(root.TryGetProperty("title", out var t)) JobTitle = t.GetString() ?? JobTitle;
                                    
                                    // Description
                                    if(root.TryGetProperty("description", out var d)) 
                                    {
                                        JobDescription = Regex.Replace(d.GetString() ?? "", "<.*?>", " ").Trim(); // Strip HTML
                                    }

                                    // Location
                                    if (root.TryGetProperty("jobLocation", out var locObj))
                                    {
                                        if (locObj.TryGetProperty("address", out var addrObj))
                                        {
                                            string region = "", locality = "";
                                            if (addrObj.TryGetProperty("addressRegion", out var r)) region = r.GetString();
                                            if (addrObj.TryGetProperty("addressLocality", out var l)) locality = l.GetString();
                                            LocationValue = $"{locality}, {region}".Trim(',').Trim();
                                        }
                                    }

                                    // Date Posted
                                    if (root.TryGetProperty("datePosted", out var dateEl)) JobDatePosted = dateEl.GetString();

                                    // Salary (The Goal!)
                                    if (root.TryGetProperty("baseSalary", out var salaryObj))
                                    {
                                        if (salaryObj.TryGetProperty("value", out var valObj)) 
                                        {
                                            string currency = "GBP";
                                            if (salaryObj.TryGetProperty("currency", out var curr)) currency = curr.GetString();
                                            
                                            string unit = "";
                                            if (valObj.TryGetProperty("unitText", out var u)) unit = u.GetString(); // e.g. "YEAR", "HOUR"

                                            // Handle Object (Min/Max) or Value
                                            if (valObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                                            {
                                                 string min="", max="";
                                                 if (valObj.TryGetProperty("minValue", out var minE)) min = minE.ToString();
                                                 if (valObj.TryGetProperty("maxValue", out var maxE)) max = maxE.ToString();
                                                 if (valObj.TryGetProperty("value", out var vE)) min = vE.ToString(); 
                                                 
                                                 JobSalary = $"{currency} {min} - {max}".Trim();
                                            }
                                            else 
                                            {
                                                JobSalary = $"{currency} {valObj}";
                                            }

                                            if (!string.IsNullOrEmpty(unit)) JobSalary += $" per {unit.ToLower()}";
                                        }
                                    }

                                    // Skills
                                    if (root.TryGetProperty("skills", out var skillsEl))
                                    {
                                         if (skillsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                                            JobSkills = string.Join(", ", skillsEl.EnumerateArray().Select(x => x.GetString()));
                                         else
                                            JobSkills = skillsEl.GetString();
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.LogWarning($"JSON Parse Error: {ex.Message}"); }

                        // B. Fallback/Enhancement: Visual Extraction for Skills 
                        // (If JSON missed it or we want specifically the "Required Skills" list)
                        if (string.IsNullOrEmpty(JobSkills) || JobSkills.Contains("See description"))
                        {
                            try 
                            {
                                // Try to find a header meant for skills
                                var skillsHeader = await page.QuerySelectorAsync("strong:has-text('Skills'), strong:has-text('Requirements'), h3:has-text('Skills'), h3:has-text('Requirements')");
                                if (skillsHeader == null) 
                                {
                                    // Try searching text content via XPath if CSS fails
                                    var xpathHeaders = await page.QuerySelectorAllAsync("//*[contains(text(), 'Required Skills') or contains(text(), 'Requirements') or contains(text(), 'Qualifications')]");
                                    if (xpathHeaders.Any()) skillsHeader = xpathHeaders.Last(); // Often the last one is the main one
                                }

                                if (skillsHeader != null)
                                {
                                    // Look for the next siblings until a UL is found
                                    var nextSibling = await skillsHeader.EvaluateHandleAsync("el => el.nextElementSibling");
                                    var limit = 0;
                                    while (nextSibling != null && limit < 3) 
                                    {
                                        var tagName = await nextSibling.EvaluateAsync<string>("el => el.tagName");
                                        if (tagName == "UL") 
                                        {
                                            var elementHandle = nextSibling.AsElement();
                                            if (elementHandle != null)
                                            {
                                                var items = await elementHandle.QuerySelectorAllAsync("li");
                                                var skillList = new List<string>();
                                                foreach(var item in items) 
                                                {
                                                    skillList.Add(await item.InnerTextAsync());
                                                }
                                                if (skillList.Any()) 
                                                {
                                                    JobSkills = string.Join("; ", skillList.Take(10));
                                                    _logger.LogInformation($"Extracted {skillList.Count} visual skills.");
                                                }
                                            }
                                            break;
                                        }
                                        nextSibling = await nextSibling.EvaluateHandleAsync("el => el.nextElementSibling");
                                        limit++;
                                    }
                                }
                            }
                            catch (Exception px) { _logger.LogWarning($"Visual Skills Scratch Error: {px.Message}"); }
                        }

                        // C. Fallback for Title
                        if (JobTitle == QJobName)
                        {
                            var h1 = await page.QuerySelectorAsync("h1");
                            if (h1 != null) JobTitle = await h1.InnerTextAsync();
                        }

                        // Freshness Check (Active < 1 year)
                        bool isJobActive = true;
                        if (DateTime.TryParse(JobDatePosted, out DateTime pDate))
                        {
                             if (pDate < DateTime.Now.AddYears(-1)) isJobActive = false;
                        }

                        ScrapedJobs.Add(new ScrapedJob
                        {
                            JobName = JobTitle.Trim(),
                            JobUrl = link,
                            JobLocation = LocationValue,
                            JobDescription = JobDescription,
                            JobSalary = JobSalary.Replace("GBP", "£").Trim(),
                            JobDatePosted = JobDatePosted,
                            SiteId = WebsiteID,
                            IsAvailable = isJobActive,
                            QueryId = QueryID,
                            JobNotes = string.IsNullOrEmpty(JobSkills) || JobSkills == "See description" ? "Skills not specified" : JobSkills
                        });

                        counter++;
                    }
                    catch { /* Ignore single job Failures */ }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Reed Scraper Critical Fail: {ex.Message}");
            }

            return ScrapedJobs;
        }
    }
}