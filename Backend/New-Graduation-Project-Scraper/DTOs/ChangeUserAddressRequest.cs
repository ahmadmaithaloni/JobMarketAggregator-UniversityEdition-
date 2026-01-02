namespace ScraperAPI.DTOs
{
    public class ChangeUserAddressRequest
    {
        public int UserID { get; set; }
        public string? UserPassWord { get; set; }
        public string? UserNewAddress { get; set; }
    }
}