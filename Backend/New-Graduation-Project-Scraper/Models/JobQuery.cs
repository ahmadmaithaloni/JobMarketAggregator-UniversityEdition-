using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScraperAPI.Models;

[Index("UserId", Name = "IX_JobQueries_UserId")]
public partial class JobQuery
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

    public int QueryId { get; set; }

    public int UserId { get; set; }

    [StringLength(1000)]
    public string QueryDescription { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreationDate { get; set; }

    [Column("QJobName")]
    [StringLength(200)]
    public string QjobName { get; set; } = null!;

    [Column("QJobLocation")]
    [StringLength(150)]
    public string QjobLocation { get; set; } = null!;

    [Column("QJobStartTime")]
    public TimeOnly QjobStartTime { get; set; }

    [Column("QJobEndTime")]
    public TimeOnly QjobEndTime { get; set; }

    [Column("QLowSalary", TypeName = "money")]
    public decimal QlowSalary { get; set; }

    [Column("QHighSalary", TypeName = "money")]
    public decimal QhighSalary { get; set; }

    [InverseProperty("Query")]
    public virtual ICollection<ScrapedJob> ScrapedJobs { get; set; } = new List<ScrapedJob>();

    [ForeignKey("UserId")]
    [InverseProperty("JobQueries")]
    public virtual User User { get; set; } = null!;
}
