namespace ScraperAPI.DTOs
{
    public class ChangeUserMajorRequest
    {
        public int UserID { get; set; }
        public string? UserPassword { get; set; }
        public string? UserNewMajor { get; set; }
    }
}