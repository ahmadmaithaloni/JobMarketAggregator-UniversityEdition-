using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScraperAPI.Models;

public partial class JobSite
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

    public int SiteId { get; set; }

    [StringLength(500)]
    public string SiteUrl { get; set; } = null!;

    [StringLength(100)]
    public string SiteName { get; set; } = null!;

    public bool IsActive { get; set; }

    [InverseProperty("Site")]
    public virtual ICollection<ScrapedJob> ScrapedJobs { get; set; } = new List<ScrapedJob>();
}
