using Azure.Messaging;
using HtmlAgilityPack;
using Microsoft.AspNetCore.JsonPatch.Internal;
//using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using ScraperAPI.External_API_Keys;
using ScraperAPI.Models;
using ScraperAPI.Services.LocationMapper_Service;
using SerpApi;
using System.Collections;
using System.Collections.Generic;
using WebApplication1.Models;
namespace ScraperAPI.Services.Scraping_Service
{
    public class ScrapingEngine : IScrapingService
    {
        private readonly ScrapingEngineDbContext _dbContext;
        private readonly ILocationMapperService _locationMapperService;
        private readonly ILogger<ScrapingEngine> _logger;
        //private readonly SerpApiSettings _serp;
        public ScrapingEngine(ScrapingEngineDbContext dbContext, ILocationMapperService locationMapper, ILogger<ScrapingEngine> logger /*SerpApiSettings serp*/)
        {
            _dbContext = dbContext;
            _locationMapperService = locationMapper;
            _logger = logger;
            //_serp = serp;
        }

        public async Task<List<ScrapedJob>> ScrapeWebsiteAsync(int QueryID)
        {
            //check is the query exists in the database: (will changed)
            if(QueryID == null)
            {
                _logger.LogError("The provided QueryID is null."); // log the error
                return new List<ScrapedJob>();
            }

            // fetch the Query Details from the database using the QueryID:
            JobQuery jobQuery = _dbContext.JobQueries.FirstOrDefault(q => q.QueryId == QueryID);

            // check if the jobQuery is null:
            if (jobQuery == null) 
            {
                _logger.LogError($"No JobQuery found for the provided QueryID: {QueryID}"); // log the error
                return new List<ScrapedJob>();
            }

            // Job Query Details:
            string JobDescription = jobQuery.QjobName; // Job Name
            string JobLocation = jobQuery.QjobLocation; // Job Location
            TimeOnly JobStartTime = jobQuery.QjobStartTime; // Job Start Time
            TimeOnly JobEndTime = jobQuery.QjobEndTime; // Job End Time
            decimal LowSalary = jobQuery.QlowSalary; // Low Salary
            decimal HighSalary = jobQuery.QhighSalary; // High Salary


            //HttpClient client = new HttpClient(); // create http client to send requests (remove)
            HtmlWeb web = new HtmlWeb(); // create the web to try to fake the web client 
            // identify the country and the Scraper Name from the JobLocation using the location mapper service:
            string country = _locationMapperService.MapLocationToCountry(JobLocation); // location name
            string scraperName = _locationMapperService.GetTargetScraperKey(JobLocation); // scraper name

            //select the proper URL based on the country:
            if (scraperName == "MENA") // deal with MENA countries
            {
                // construct the search URL based on the country:
                string searchUrl = $"https://www.bayt.com/en/{Uri.EscapeDataString(country)}/jobs/{Uri.EscapeDataString(JobDescription)}-jobs-in-{Uri.EscapeDataString(JobLocation)}/";

                // try to fake the user agent (ao avoid the blocking from the website):
                web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

                // load the HTML content from the search URL:
                //string html = await client.GetStringAsync(searchUrl); // get the html content from the url (using await insted of previous .Result to prevent deadlock)
                //HtmlDocument HtmlDoc = new(); // create html document object
                //HtmlDoc.LoadHtml(html); // load the html content into the document object
                HtmlDocument HtmlDoc = await web.LoadFromWebAsync(searchUrl);

                // DEBUGGING: What page did we actually get?
                string pageTitle = HtmlDoc.DocumentNode.SelectSingleNode("//title")?.InnerText;
                _logger.LogWarning($"[BAYT DEBUG] Page Title: {pageTitle}");
                _logger.LogWarning($"[BAYT DEBUG] Target URL: {searchUrl}");

                // select the job containers from HTML using XPath:

                // select the job containers from HTML using XPath:
                //string xpath = "//ul[@class ='media - list in-card is -spaced is -divided is -reversed has - hover - d has - rects has - free - img bb')]//li[@class = 'has-pointer-d']";
                // Robust XPath: Finds any <ul> containing 'media-list' and then finds <li> children containing 'has-pointer-d'
                string xpath = "//ul[contains(@class, 'media-list')]//li[contains(@class, 'has-pointer-d')]";

                HtmlNodeCollection jobNodes = HtmlDoc.DocumentNode.SelectNodes(xpath); // put the nodes into collection 

                // check if the node collection is empty or null:
                if ( jobNodes == null || jobNodes.Count == 0)
                {
                    _logger.LogError($"There is no fetched Jobs comes from the location ({JobLocation}) and country ({country})"); // log the error
                    return new List<ScrapedJob>();
                }

                // create the List to store the scraped jobs:
                List<ScrapedJob> ScrapedJobs = new List<ScrapedJob>();

                // iterate through each job node and extract the details:
                foreach (var jobNode in jobNodes)
                {
                    string jobTitle = jobNode.SelectSingleNode(".//h2[@class='col u-stretch t-large m0 t-nowrap-d t-trim']/a")?.InnerText.Trim() ?? "N/A";
                    string jobLocation = jobNode.SelectSingleNode(".//a[@class='t-mute']/span")?.InnerText.Trim() ?? "N/A";
                    string jobUrl = jobNode.SelectSingleNode(".//h2[@class='col u-stretch t-large m0 t-nowrap-d t-trim']/a")?.GetAttributeValue("href", "N/A") ?? "N/A";
                    int SiteID = _dbContext.JobSites.FirstOrDefault(s => s.SiteName== "Bayt").SiteId;
                    string jobDescription = jobNode.SelectSingleNode(".//div[@class='jb-descr m10t t-small']")?.InnerText.Trim() ?? "N/A";
                    string jobNotes = $"Scraped from MENA for location {JobLocation}";
                    string isAvailable = "true";
                    int queryId = _dbContext.JobQueries.FirstOrDefault(q => q.QjobName == JobDescription).QueryId;

                    // create a new ScrapedJob object and add it to the list:
                    ScrapedJob scrapedJob = new ScrapedJob
                    {
                        JobName = jobTitle,
                        JobLocation = jobLocation,
                        JobUrl = jobUrl,
                        SiteId = SiteID,
                        JobDescription = jobDescription,
                        JobNotes = jobNotes,
                        IsAvailable = bool.Parse(isAvailable),
                        QueryId = queryId
                    };
                    
                    // add the scraped job to the list:
                    ScrapedJobs.Add(scrapedJob);
                }

                // return the scraped jobs:
                return ScrapedJobs;
            }
            else if (scraperName== "UK") // deal with UK countries
            {
                // construct the search URL for UK:
                string SearchURL = $"http://reed.co.uk/jobs/{Uri.EscapeDataString(JobDescription)}-jobs-in-{Uri.UnescapeDataString(JobLocation)}?salaryFrom={LowSalary}&salaryTo={HighSalary}";

                // load the HTML content from the search URL:
                //string html = await client.GetStringAsync(SearchURL);
                //HtmlDocument HtmlDoc = new(); // create html document object
                //HtmlDoc.LoadHtml(html); // load the html content into the document object
                HtmlDocument HtmlDoc = await web.LoadFromWebAsync(SearchURL);

                // select the job containers from HTML using XPath:
                string xpath = "//script[@id='__NEXT_DATA__']";

                HtmlNode JobNode = HtmlDoc.DocumentNode.SelectSingleNode(xpath); // load the script from the specified node (script that has id attribute)

                // this website deals with Next.JS so the content collected in one json file placed in the (xpath) variable path, so we have to deal with newtonsoft:

                // check if the node has data or not:
                if (JobNode != null)
                {
                    // fetch the inner text and put it in row string variable: 
                    string JsonString= JobNode.InnerText;
                    JObject JobData = JObject.Parse(JsonString); // parse it to json text
                                                                  
                    JArray? JobDataArray = (JArray)JobData["props"]?["pageProps"]?["searchResults"]?["jobs"]; // fetch all data from the key "SearchResults"

                    // check if the data exist or not:
                    if (JobDataArray == null)
                    {
                        _logger.LogError($"there is no fetched data from the query ({QueryID})");
                        return new List<ScrapedJob>();
                    }

                    // take the data from the nodes and save it in an (ScrapedJob) list:

                    List<ScrapedJob> scrapedJobs= new List<ScrapedJob>();
                    int site = _dbContext.JobSites.FirstOrDefault(s => s.SiteName == "Reed.co.uk").SiteId;
                    int UksiteID = site;
                    int ExistedJobs = _dbContext.ScrapedJobs.Count(); // the existed jobs
                    int jobId = ExistedJobs;
                    foreach (JObject Job in JobDataArray)
                    {
                        // access the inner tokens for job details:
                        var detail = Job["jobDetail"];
                        if (detail == null) continue;

                        // fetch the job details and put it in the created variables:
                        
                        string? jobName = detail["jobTitle"]?.ToString();
                        string? jobLocation = detail["countyLocation"]?.ToString();
                        jobId += 1;
                        // process the url:
                        string? rawUrl = detail["externalUrl"]?.ToString();
                        string jobUrl = "";
                        if (rawUrl.StartsWith("http"))
                        {
                            jobUrl = rawUrl;
                        }
                        else
                        {
                            jobUrl = "https://www.reed.co.uk" + rawUrl;
                        }

                        int siteID = UksiteID;
                        string? jobDescription = detail["jobDescription"]?.ToString();
                        string jobNotes = $"this job has salary starts from ({detail["salaryFrom"]} euro , and ends to ({detail["salaryTo"]}) euro, )";
                        bool availabity = false;
                        DateTime expiryDate = DateTime.Parse(detail["expiryDate"].ToString()); // to make comparisom between tis momment and the job expiry date

                        // check if the job available at this momment:
                        if (DateTime.Now < expiryDate)
                        {
                           availabity= true; // the job is set to be available
                        }
                        int queryId = QueryID;

                        ScrapedJob Dbjob = new ScrapedJob // create an job object 
                        {
                            JobId = jobId,
                            JobName = jobName,
                            JobLocation = jobLocation,
                            JobUrl = jobUrl,
                            JobDescription = jobDescription,
                            JobNotes = jobNotes,
                            SiteId = siteID,
                            IsAvailable = availabity,
                            QueryId = queryId,
                        };

                        // add the object to the jobs list:
                        scrapedJobs.Add(Dbjob);

                    }
                    // log the events:
                    _logger.LogInformation("the list of scraped jobs is returned successfullty");
                    return scrapedJobs;
                }

                //log that the node does not return any jobs:
                _logger.LogError("the UK scraped does not found any results");
                return new List<ScrapedJob>();
            }


            return new List<ScrapedJob>();
        }
        
        // v2 of the scraper: since the v1 cannot handle (cloudflare captcha), the new scraper will fetch the jobs directly using thier links from serp api 
        public async Task<List<ScrapedJob>> ScrapeWebsiteAsyncV2(int QueryID)
        {
            // check if the query exist of not:
            bool QueryExist = await _dbContext.JobQueries.AnyAsync(q => q.QueryId == QueryID);
            if (!QueryExist) 
            {
                _logger.LogError($"the query with ({QueryID}) is not exist in the db");
                return new List<ScrapedJob>();
            }

            // fetch the user query to start the process:
            JobQuery? userQuery = await _dbContext.JobQueries.FirstOrDefaultAsync(q => q.QueryId == QueryID);
            if (userQuery == null) 
            {
                _logger.LogError($"the query ({QueryID}) has no details");
                return new List<ScrapedJob>(); // insted of return null and may cause crash to the system -> return empty list  
            }

            // select the proper scraper based on the query details:
            string queryLocation = userQuery.QjobLocation;
            string country = _locationMapperService.MapLocationToCountry(queryLocation); // decide the country
            string ScraperName = _locationMapperService.GetTargetScraperKey(queryLocation); // select the scraper based on location that will converted to the country

            // select the proper scraper:
            if (ScraperName == "MENA")
            {
                // construct the query:
                string query = $"site:bayt.com \"{userQuery.QjobName}\" {userQuery.QjobLocation}"; // select the job name and location from the JobQuery Table

                // deal with serp api:
                //string key = _serp.ApiKey;
                string key = "27126251a9a7362f541988c20d933c3ea6e74e94c2e2b215ccacd9630a2cf92e";
                Hashtable ht = new Hashtable();
                ht.Add("q", query);
                ht.Add("engine", "google");
                ht.Add("num", "30"); // number of fetched results
                ht.Add("hl", "en");  // English results

                // construct the job list:
                List<ScrapedJob> jobs = new List<ScrapedJob>();

                try
                {
                    GoogleSearch search = new GoogleSearch(ht, key);
                    JObject data = search.GetJson();
                    JArray results = (JArray)data["organic_results"];

                    

                    // check if the result is empty or not:
                    if (results == null)
                    {
                        _logger.LogWarning($"the serp api query for the query ({QueryID}) has return nothing (empty list)");
                        return jobs; // return empty list 
                    }

                    //define the available jobs and SiteName to specify the id before the loop (to avoid consume the resources):
                    JobSite? site = await _dbContext.JobSites.FirstOrDefaultAsync(s => s.SiteName == "Bayt.com");
                    int siteID = site != null ? site.SiteId : 1; // to not get null exception and crash the code, i had used null conditional operator
                    foreach (JObject result in results)
                    {
                        
                        // construct the form to fetch the data from the query results:
                        string? title = result["title"]?.ToString();
                        string? link = result["link"]?.ToString();
                        string? snippet = result["snippet"]?.ToString();

                        // check if the link value represent the real job link or not:
                        if (string.IsNullOrEmpty(link) || !link.Contains("/jobs/")) continue; // pass to the text result

                        // cinstruct the job var to store the data into it:
                        ScrapedJob NewJob = new ScrapedJob
                        {
                            JobName = title,
                            JobLocation = queryLocation,
                            JobUrl = link,
                            JobDescription = snippet,
                            JobNotes = "this job comes from Bayt.com",
                            SiteId = siteID,
                            IsAvailable = true,
                            QueryId = userQuery.QueryId
                        };

                        jobs.Add(NewJob);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"the scraper can't get the results");
                }
                return jobs;
            }

            return new List<ScrapedJob>(); 
        }
    }
}
