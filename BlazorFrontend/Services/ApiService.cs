using System.Net.Http.Json;
using BlazorFrontend.Models;

namespace BlazorFrontend.Services
{
    public class ApiService
    {
        private readonly HttpClient _http;
        private const string BaseUrl = "http://localhost:5187"; 

        public UserProfile? CurrentUser { get; private set; }
        public ScrapedJob? SelectedJob { get; set; }
        public List<ScrapedJob> CurrentJobList { get; set; } = new List<ScrapedJob>();

        public ApiService(HttpClient http)
        {
            _http = http;
            // Increase timeout to 5 minutes to allow for long scraping operations
            _http.Timeout = TimeSpan.FromMinutes(5);
        }

        public void Logout()
        {
            CurrentUser = null;
            CurrentJobList.Clear();
            SelectedJob = null;
        }

        public async Task<bool> Login(string email, string password)
        {
            // Endpoint: api/UserManagement/Login
            var path = $"{BaseUrl}/api/UserManagement/Login";
            
            try 
            {
                var response = await _http.PostAsJsonAsync(path, new { Email = email, Password = password });
                if (response.IsSuccessStatusCode)
                {
                    CurrentUser = await response.Content.ReadFromJsonAsync<UserProfile>();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> Register(UserProfile profile)
        {
            // Endpoint: /api/ProfileSettings/CreateUserProfile/: UserProfile
            var path = $"{BaseUrl}/api/ProfileSettings/CreateUserProfile/";
            // anonymous object:
            var RequestBody = new {
                UserName = profile.UserName,
                UserAddress = profile.UserAddress,
                UserEmail = profile.UserEmail,
                UserPhone = profile.UserPhone,
                UserPassword = profile.UserPassword,
                UserMajor = profile.UserMajor
            };
            // send the request in a request body:
            var response = await _http.PostAsJsonAsync(path, RequestBody);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<List<ScrapedJob>> SearchJobs(JobQuery query)
        {
            if (CurrentUser == null) throw new Exception("User not logged in.");

            // Endpoint: /api/JobQuery/CreateJobQuery/v1/...
            var createPath = $"{BaseUrl}/api/JobQuery/CreateJobQuery/v1";
            // anonymous object"
            var RequestBody = new {
                UserID = CurrentUser.UserId,
                JobName = query.JobName,
                JobLocation = query.Location,
                JobStartTime = query.StartTime,
                JobEndTime = query.EndTime,
                JobLowSalary = query.LowSalary,
                JobHighSalary = query.HighSalary
            };
            var createResponse = await _http.PostAsJsonAsync(createPath, RequestBody);
            if (!createResponse.IsSuccessStatusCode)
            {
                var error = await createResponse.Content.ReadAsStringAsync();
                throw new Exception($"Query Failed: {error}");
            }
            
            // optimization: backend now returns the CreatedQuery object which contains the ID
            var createdQuery = await createResponse.Content.ReadFromJsonAsync<JobQuery>();
            if (createdQuery == null) throw new Exception("Failed to retrieve query ID.");
            
            // Use the new Optimized Single Query Endpoint
            var fetchPath = $"{BaseUrl}/api/Scraping/ScrapeSingleQuery/v1/{createdQuery.QueryId}";
            var response = await _http.PostAsJsonAsync(fetchPath, RequestBody);
            if (response.IsSuccessStatusCode)
            {
                var jobs = await response.Content.ReadFromJsonAsync<List<ScrapedJob>>();
                 // Persist state
                CurrentJobList = jobs?.ConvertAll(scrapeJob => scrapeJob) ?? new List<ScrapedJob>();
            }
            else
            {
                CurrentJobList = new List<ScrapedJob>();
            }
            
            return CurrentJobList;
        }

        // --- Update Methods (UserManagement) ---

        public async Task UpdatePassword(string newPassword)
        {
            if (CurrentUser == null) return;
            // api/UserManagement/ChangePassword/v1
            var path = $"{BaseUrl}/api/UserManagement/ChangePassword/v1";
            var RequestBody = new {
                UserID = CurrentUser.UserId,
                UserPassword = CurrentUser.UserPassword,
                UserNewPassword = newPassword
            };
            var res = await _http.PutAsJsonAsync(path, RequestBody);
            res.EnsureSuccessStatusCode();
            CurrentUser.UserPassword = newPassword; // Update local state
        }

        public async Task UpdateUserName(string newName)
        {
            if (CurrentUser == null) return;
            // api/UserManagement/ChangeUserName/v1
            var path = $"{BaseUrl}/api/UserManagement/ChangeUserName/v1";
            var RequestBody = new {
                UserID = CurrentUser.UserId,
                UserPassKey = CurrentUser.UserPassword,
                UserNewName = newName
            };
            var res = await _http.PutAsJsonAsync(path, RequestBody);
            res.EnsureSuccessStatusCode();
            CurrentUser.UserName = newName;
        }

        public async Task UpdateAddress(string newAddress)
        {
            if (CurrentUser == null) return;
            // api/UserManagement/ChangeUserAddress/v1
            var path = $"{BaseUrl}/api/UserManagement/ChangeUserAddress/v1";
            var RequestBody = new {
                UserID = CurrentUser.UserId,
                UserPassWord = CurrentUser.UserPassword,
                UserNewAddress = newAddress
            };
            var res = await _http.PutAsJsonAsync(path, RequestBody);
            res.EnsureSuccessStatusCode();
            CurrentUser.UserAddress = newAddress;
        }

        public async Task UpdateEmail(string newEmail)
        {
            if (CurrentUser == null) return;
            // api/UserManagement/ChangeEmailAddress/v1
            var path = $"{BaseUrl}/api/UserManagement/ChangeEmailAddress/v1";
            var RequestBody = new {
                UserID = CurrentUser.UserId,
                UserPassword = CurrentUser.UserPassword,
                UserNewEmail = newEmail
            };
            var res = await _http.PutAsJsonAsync(path, RequestBody);
            res.EnsureSuccessStatusCode();
            CurrentUser.UserEmail = newEmail;
        }

        public async Task UpdatePhone(string newPhone)
        {
            if (CurrentUser == null) return;
            // api/UserManagement/ChangePhone/v1
            var path = $"{BaseUrl}/api/UserManagement/ChangePhone/v1";
            var RequestBody = new {
                UserID = CurrentUser.UserId,
                UserPassword = CurrentUser.UserPassword,
                UserNewPhone = newPhone
            };
            var res = await _http.PutAsJsonAsync(path, RequestBody);
            res.EnsureSuccessStatusCode();
            CurrentUser.UserPhone = newPhone;
        }

        public async Task UpdateMajor(string newMajor)
        {
            if (CurrentUser == null) return;
            // api/UserManagement/ChangeMajor/v1
            var path = $"{BaseUrl}/api/UserManagement/ChangeMajor/v1";
            var RequestBody = new {
                UserID = CurrentUser.UserId,
                UserPassword = CurrentUser.UserPassword,
                UserNewMajor = newMajor
            };
            var res = await _http.PutAsJsonAsync(path, RequestBody);
            res.EnsureSuccessStatusCode();
            CurrentUser.UserMajor = newMajor;
        }

        // Add other update methods similarly if needed
        public async Task<List<JobQuery>> GetMyQueries()
        {
            if (CurrentUser == null) return new List<JobQuery>();
            
            var path = $"{BaseUrl}/api/JobQuery/GetUserQueries/v1/{CurrentUser.UserId}";
            try
            {
                var queries = await _http.GetFromJsonAsync<List<JobQuery>>(path);
                return queries ?? new List<JobQuery>();
            }
            catch
            {
                return new List<JobQuery>();
            }
        }
        public async Task<List<ScrapedJob>> GetJobsByQueryId(int queryId)
        {
            var path = $"{BaseUrl}/api/JobQuery/GetJobsByQueryId/v1/{queryId}";
            try 
            {
                var jobs = await _http.GetFromJsonAsync<List<ScrapedJob>>(path);
                CurrentJobList = jobs ?? new List<ScrapedJob>();
                return CurrentJobList;
            }
            catch
            {
                return new List<ScrapedJob>();
            }
        }
    }
}
