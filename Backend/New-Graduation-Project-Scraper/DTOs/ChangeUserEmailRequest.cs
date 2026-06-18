namespace ScraperAPI.DTOs
{
    public class ChangeUserEmailRequest
    {
        public int UserID { get; set; }
        public string? UserPassword { get; set; }
        public string? UserNewEmail { get; set; }
    }
}