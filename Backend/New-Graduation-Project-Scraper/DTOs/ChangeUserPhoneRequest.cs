namespace ScraperAPI.DTOs
{
    public class ChangeUserPhoneRequest
    {
        public int UserID { get; set; }
        public string? UserPassword { get; set; }
        public string? UserNewPhone { get; set; }
    }
}