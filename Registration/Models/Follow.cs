namespace Registration.Models
{
    public class Follow
    {
        public int FollowId { get; set; }
        public int FollowerId { get; set; } // Who is following
        public int FollowingId { get; set; } // Who is being followed
        public DateTime CreatedAt { get; set; }
    }
}
