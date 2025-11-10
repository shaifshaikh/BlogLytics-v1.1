using System.ComponentModel.DataAnnotations;

namespace Registration.Models
{
    public class Blog
    {
        public int BlogId { get; set; }

        public int UserId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; }

        public string ImageUrl { get; set; }

        [Required]
        public int CategoryId { get; set; }

        public string Tags { get; set; } // JSON string: ["cricket", "IPL"]

        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int ShareCount { get; set; }
        public int ViewCount { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation properties (for display only)
        public string AuthorName { get; set; }
        public string AuthorProfilePicture { get; set; }
        public string CategoryName { get; set; }
        public bool IsLikedByCurrentUser { get; set; }
        public bool IsFollowingAuthor { get; set; }
    }
}


