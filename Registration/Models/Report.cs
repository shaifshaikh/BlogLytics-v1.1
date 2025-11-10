using System.ComponentModel.DataAnnotations;

namespace Registration.Models
{
    public class Report
    {
        public int ReportId { get; set; }

        public int ReporterId { get; set; }

        public int? BlogId { get; set; }
        public int? CommentId { get; set; }

        public int ReportedUserId { get; set; }

        [Required(ErrorMessage = "Please select a reason")]
        public string Reason { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public string Status { get; set; } = "pending";

        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedBy { get; set; }
    }

    public class ReportViewModel
    {
        public int? BlogId { get; set; }
        public int? CommentId { get; set; }
        public int ReportedUserId { get; set; }

        [Required(ErrorMessage = "Please select a reason")]
        public string Reason { get; set; }

        public string Description { get; set; }
    }
}
