namespace ScraperAPI.DTOs
{
    public class ChangeUserNameRequest
    {
        public int UserID { get; set; }
        public string? UserPassKey { get; set; }
        public string? UserNewName { get; set; }
    }
}