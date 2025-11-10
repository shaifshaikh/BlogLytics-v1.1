using System.ComponentModel.DataAnnotations;

namespace Registration.Models
{
    public class Comment
    {
        public int CommentId { get; set; }

        public int UserId { get; set; }

        public int BlogId { get; set; }

        [Required(ErrorMessage = "Comment cannot be empty")]
        [StringLength(500, ErrorMessage = "Comment cannot exceed 500 characters")]
        public string Content { get; set; }

        public int? ParentCommentId { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public string UserName { get; set; }
        public string UserProfilePicture { get; set; }
        public List<Comment> Replies { get; set; }
    }
}
