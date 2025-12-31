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
            // Endpoint: /api/ProfileSettings/CreateUserProfile/...
            var path = $"{BaseUrl}/api/ProfileSettings/CreateUserProfile/" +
                       $"{Uri.EscapeDataString(profile.UserName)}/" +
                       $"{Uri.EscapeDataString(profile.UserAddress)}/" +
                       $"{Uri.EscapeDataString(profile.UserEmail)}/" +
                       $"{Uri.EscapeDataString(profile.UserPhone)}/" +
                       $"{Uri.EscapeDataString(profile.UserMajor)}/" +
                       $"{Uri.EscapeDataString(profile.UserPassword)}";

            var response = await _http.PostAsync(path, null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<List<ScrapedJob>> SearchJobs(JobQuery query)
        {
            if (CurrentUser == null) throw new Exception("User not logged in.");

            // Endpoint: /api/JobQuery/CreateJobQuery/v1/...
            var createPath = $"{BaseUrl}/api/JobQuery/CreateJobQuery/v1/{CurrentUser.UserId}/" +
                             $"{Uri.EscapeDataString(query.JobName)}/" +
                             $"{Uri.EscapeDataString(query.Location)}/" +
                             $"{Uri.EscapeDataString(query.StartTime)}/" +
                             $"{Uri.EscapeDataString(query.EndTime)}/" +
                             $"{query.LowSalary}/" +
                             $"{query.HighSalary}";

            var createResponse = await _http.PostAsync(createPath, null);
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
            var response = await _http.PostAsync(fetchPath, null);
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
             // api/UserManagement/ChangePassword/v1/{UserID}/{UserPassword}/{UserNewPassword}
             var path = $"{BaseUrl}/api/UserManagement/ChangePassword/v1/{CurrentUser.UserId}/{Uri.EscapeDataString(CurrentUser.UserPassword)}/{Uri.EscapeDataString(newPassword)}";
             var res = await _http.PutAsync(path, null);
             res.EnsureSuccessStatusCode();
             CurrentUser.UserPassword = newPassword; // Update local state
        }

        public async Task UpdateUserName(string newName)
        {
             if (CurrentUser == null) return;
             // api/UserManagement/ChangeUserName/v1/{UserID}/{UserPassKey}/{UserNewName}
             var path = $"{BaseUrl}/api/UserManagement/ChangeUserName/v1/{CurrentUser.UserId}/{Uri.EscapeDataString(CurrentUser.UserPassword)}/{Uri.EscapeDataString(newName)}";
             var res = await _http.PutAsync(path, null);
             res.EnsureSuccessStatusCode();
             CurrentUser.UserName = newName;
        }

        public async Task UpdateAddress(string newAddress)
        {
             if (CurrentUser == null) return;
             // api/UserManagement/ChangeUserAddress/v1/{UserID}/{UserPassWord}/{UserNewAddress}
             var path = $"{BaseUrl}/api/UserManagement/ChangeUserAddress/v1/{CurrentUser.UserId}/{Uri.EscapeDataString(CurrentUser.UserPassword)}/{Uri.EscapeDataString(newAddress)}";
             var res = await _http.PutAsync(path, null);
             res.EnsureSuccessStatusCode();
             CurrentUser.UserAddress = newAddress;
        }

        public async Task UpdateEmail(string newEmail)
        {
             if (CurrentUser == null) return;
             // api/UserManagement/ChangeEmailAddress/v1/{UserID}/{UserPassword}/{UserNewEmail}
             var path = $"{BaseUrl}/api/UserManagement/ChangeEmailAddress/v1/{CurrentUser.UserId}/{Uri.EscapeDataString(CurrentUser.UserPassword)}/{Uri.EscapeDataString(newEmail)}";
             var res = await _http.PutAsync(path, null);
             res.EnsureSuccessStatusCode();
             CurrentUser.UserEmail = newEmail;
        }

        public async Task UpdatePhone(string newPhone)
        {
             if (CurrentUser == null) return;
             // api/UserManagement/ChangePhone/v1/{UserID}/{UserPassword}/{UserNewPhone}
             var path = $"{BaseUrl}/api/UserManagement/ChangePhone/v1/{CurrentUser.UserId}/{Uri.EscapeDataString(CurrentUser.UserPassword)}/{Uri.EscapeDataString(newPhone)}";
             var res = await _http.PutAsync(path, null);
             res.EnsureSuccessStatusCode();
             CurrentUser.UserPhone = newPhone;
        }

        public async Task UpdateMajor(string newMajor)
        {
             if (CurrentUser == null) return;
             // api/UserManagement/ChangeMajor/v1/{UserID}/{UserPassword}/{UserNewMajor}
             var path = $"{BaseUrl}/api/UserManagement/ChangeMajor/v1/{CurrentUser.UserId}/{Uri.EscapeDataString(CurrentUser.UserPassword)}/{Uri.EscapeDataString(newMajor)}";
             var res = await _http.PutAsync(path, null);
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
