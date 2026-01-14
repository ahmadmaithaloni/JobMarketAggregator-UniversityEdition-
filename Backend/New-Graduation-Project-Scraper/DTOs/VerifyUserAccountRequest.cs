namespace ScraperAPI.DTOs
{
    public class VerifyUserAccountRequest
    {
        public string? Email { get; set; }
        public string? Code {get; set; }
    }
}