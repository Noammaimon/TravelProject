using System;
using System.ComponentModel.DataAnnotations;

namespace TravelProject.Models
{
    public class TripReviewModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TripId { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        [StringLength(500)]
        public string CommentText { get; set; }

        public DateTime ReviewDate { get; set; } = DateTime.Now;
    }
}