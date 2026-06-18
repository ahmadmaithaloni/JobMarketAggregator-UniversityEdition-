namespace ScraperAPI.Options
{
    public class EmailSettingsOptions
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; }
        public string SenderEmail { get; set; } = string.Empty;
        public string AppPassword { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
    }
}