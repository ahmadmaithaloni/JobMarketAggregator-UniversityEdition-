namespace ScraperAPI.DTOs
{
    public class CreateUserProfileRequest
    {
        public string? UserName { get; set; }
        public string?  UserEmail { get; set; }
        public string? UserAddress { get; set; }
        public string? UserPhone { get; set; }
        public string? UserMajor { get; set; }
        public string? UserPassword { get; set; }
    }
}