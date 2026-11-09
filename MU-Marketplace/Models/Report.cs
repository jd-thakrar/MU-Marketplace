using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MU_Marketplace.Models
{
    public class Report
    {
        public int ReportId { get; set; }

        public int ProductId { get; set; }

        public int ReporterId { get; set; }

        public string Reason { get; set; }

        // 0 = Pending, 1 = Resolved, 2 = Ignored
        public byte Status { get; set; }

        public string AdminNotes { get; set; }

        public int? ReviewedBy { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        // Display properties (retrieved using SQL JOIN)
        public string ProductTitle { get; set; }

        public string ReporterName { get; set; }

        public string ReporterEmail { get; set; }
    }
}