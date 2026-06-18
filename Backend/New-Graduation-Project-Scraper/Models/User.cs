using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScraperAPI.Models;

[Index("UserEmail", Name = "IX_Users_Email")]
[Index("UserEmail", Name = "UQ_Users_Email", IsUnique = true)]
public partial class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

    public int UserId { get; set; }

    [StringLength(250)]
    public string UserName { get; set; } = null!;

    [StringLength(500)]
    public string UserAddress { get; set; } = null!;

    [StringLength(100)]
    public string UserEmail { get; set; } = null!;

    [StringLength(15)]
    [Unicode(false)]
    public string UserPhone { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string UserMajor { get; set; } = null!;

    [StringLength(256)]
    public string UserPassword { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreationDate { get; set; }
    public string? VerificationCode {get; set; }
    public bool IsVerified { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<JobQuery> JobQueries { get; set; } = new List<JobQuery>();
}
