using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace ScraperAPI.Models;

[Index("QueryId", Name = "IX_ScrapedJobs_QueryId")]
[Index("SiteId", Name = "IX_ScrapedJobs_SiteId")]
public partial class ScrapedJob
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

    public int JobId { get; set; }

    [StringLength(200)]
    public string JobName { get; set; } = null!;

    [StringLength(250)]
    public string JobLocation { get; set; } = null!;

    [StringLength(500)]
    public string JobUrl { get; set; } = null!;

    public int SiteId { get; set; }

    public string JobDescription { get; set; } = null!;

    [StringLength(100)]
    public string? JobSalary { get; set; }

    [StringLength(100)]
    public string? JobDatePosted { get; set; }

    public string? JobNotes { get; set; }

    public bool IsAvailable { get; set; }

    [JsonIgnore] // prevent the circle 
    public int QueryId { get; set; }

    [JsonIgnore]
    [ForeignKey("QueryId")]
    [InverseProperty("ScrapedJobs")]
    public virtual JobQuery Query { get; set; } = null!;

    [JsonIgnore]
    [ForeignKey("SiteId")]
    [InverseProperty("ScrapedJobs")]
    public virtual JobSite Site { get; set; } = null!;
}
