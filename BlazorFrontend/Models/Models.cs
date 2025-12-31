using System;

namespace BlazorFrontend.Models
{
    public class UserProfile
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserAddress { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string UserMajor { get; set; } = string.Empty;
        public string UserPassword { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
    }

    public class JobQuery
    {
        public int QueryId { get; set; }
        public string JobName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string StartTime { get; set; } = "09:00:00";
        public string EndTime { get; set; } = "17:00:00";
        public decimal LowSalary { get; set; }
        public decimal HighSalary { get; set; }

        // Backend Compat
        public string QjobName { get; set; } = string.Empty;
        public string QjobLocation { get; set; } = string.Empty;
        public TimeOnly QjobStartTime { get; set; }
        public TimeOnly QjobEndTime { get; set; }
        public decimal QlowSalary { get; set; }
        public decimal QhighSalary { get; set; }
    }

    public class ScrapedJob
    {
        public int JobId { get; set; }
        public string JobName { get; set; } = string.Empty;
        public string JobLocation { get; set; } = string.Empty;
        public string SiteName { get; set; } = "Job Site"; // Default
        public string JobUrl { get; set; } = string.Empty;
        public string JobDescription { get; set; } = string.Empty;
        public string JobNotes { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public int SiteId { get; set; }
        public int QueryId { get; set; }
    }
}
