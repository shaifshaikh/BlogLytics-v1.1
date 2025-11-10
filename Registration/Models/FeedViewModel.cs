namespace Registration.Models
{
    public class FeedViewModel
    {
        public List<Blog> Blogs { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public string FilterType { get; set; } // "all", "following", "category"
    }

    public class BlogDetailsViewModel
    {
        public Blog Blog { get; set; }
        public List<Comment> Comments { get; set; }
        public bool IsLiked { get; set; }
        public bool IsFollowing { get; set; }
    }
}
