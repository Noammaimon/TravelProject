namespace TravelProject.Models
{
    public class SiteReviewModel
    {
        public string Username { get; set; }
        public int Rating { get; set; }
        public string CommentText { get; set; }
        public DateTime ReviewDate { get; set; }
    }
}