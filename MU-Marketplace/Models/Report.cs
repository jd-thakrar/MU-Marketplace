using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MU_Marketplace.Models
{
    public class Report
    {
        public int ReportId { get; set; }

        [Index("IX_Reports_ProductId")]
        public int ProductId { get; set; }

        public int ReporterId { get; set; }

        [Required, StringLength(500)]
        public string Reason { get; set; }

        // 0 = Pending, 1 = Resolved, 2 = Ignored
        [Index("IX_Reports_Status_CreatedAt", 1)]
        public byte Status { get; set; }

        [StringLength(500)]
        public string AdminNotes { get; set; }

        public int? ReviewedBy { get; set; }

        public DateTime? ReviewedAt { get; set; }

        [Index("IX_Reports_Status_CreatedAt", 2)]
        public DateTime CreatedAt { get; set; }

        public virtual Product Product { get; set; }
        public virtual User Reporter { get; set; }
        public virtual User Reviewer { get; set; }

        public Report()
        {
            Status = 0;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
