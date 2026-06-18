namespace ScraperAPI.DTOs
{
    //{UserID}/{UserPassword}/{UserNewPassword}
    public class ChangeUserPasswordRequest
    {
        public int UserID { get; set; }
        public string? UserPassword { get; set; }
        public string? UserNewPassword { get; set; }
    }
}