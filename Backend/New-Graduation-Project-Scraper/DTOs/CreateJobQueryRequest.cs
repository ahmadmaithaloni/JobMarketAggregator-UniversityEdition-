namespace ScraperAPI.DTOs
{
    public class CreateJobQueryRequest
    {
        public int UserID { get; set; }
        public string? JobName { get; set; }
        public string? JobDescription { get; set; }
        public string? JobLocation { get; set; }
        public TimeOnly JobStartTime { get; set; }
        public TimeOnly JobEndTime { get; set; }
        public decimal JobLowSalary { get; set; }
        public decimal JobHighSalary { get; set; }
    }
}