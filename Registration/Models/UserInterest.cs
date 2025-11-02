using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Registration.Models
{
    public class UserInterest
    {
        public int InterestId { get; set; }
        public int UserId { get; set; }
        public int CategoryId { get; set; }
        public DateTime AddedAt { get; set; }

        // Navigation properties
        public string CategoryName { get; set; }
        public int NewPostsCount { get; set; } // Count of new posts in this category
    }

    [Table("Categories")]
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Category name is required")]
        [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters")]
        [Display(Name = "Category Name")]
        public string CategoryName { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual ICollection<UserInterest> UserInterests { get; set; }

        // These properties are not in database - used only for display purposes in the view
        [NotMapped]
        public string IconClass { get; set; }

        [NotMapped]
        public string ColorClass { get; set; }
    }

    public class InterestSelectionViewModel
    {
        public List<Category> Categories { get; set; }
        public List<int> SelectedCategoryIds { get; set; } = new List<int>();
    }
}
