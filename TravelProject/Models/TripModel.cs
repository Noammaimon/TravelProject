using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace TravelProject.Models
{
    public class TripModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "חובה להזין יעד")]
        [Display(Name = "יעד")]
        public string Destination { get; set; }

        [Required(ErrorMessage = "חובה להזין מדינה")]
        [Display(Name = "מדינה")]
        public string Country { get; set; }

        [Required(ErrorMessage = "חובה לבחור סוג נופש")]
        [Display(Name = "סוג חבילה")]
        public string? TripType { get; set; }

        [Required(ErrorMessage = "חובה להזין תיאור למועד זה")]
        [Display(Name = "תיאור החבילה")]
        public string Description { get; set; }

        [Display(Name = "נתיבי תמונות")]
        public string? ImagePaths { get; set; }

        [NotMapped]
        public List<IFormFile>? ImageFiles { get; set; }
        

        [NotMapped]
        public string[] ImageList => !string.IsNullOrEmpty(ImagePaths)
            ? ImagePaths.Split(',')
            : Array.Empty<string>();

        [NotMapped] 
        public decimal Price { get; set; }

        public string? FilePath { get; set; }
        public int? DiscountPercentage { get; set; }

        [NotMapped]
        public DateTime StartDate { get; set; }

        [NotMapped]
        public DateTime EndDate { get; set; }

        [NotMapped]
        public int RoomsAvailable { get; set; }

        [NotMapped]
        public bool IsOnDiscount => OriginalPrice.HasValue && OriginalPrice > Price && (!DiscountExpiration.HasValue || DiscountExpiration > DateTime.Now);

        [NotMapped]
        public decimal? OriginalPrice { get; set; }

        [NotMapped]
        public DateTime? DiscountExpiration { get; set; }

        [NotMapped]
        public int InstanceId { get; set; }

        [Display(Name = "הגבלת גיל")]
        [NotMapped] 
        public int? AgeLimit { get; set; }
        public virtual ICollection<TripInstanceModel> Instances { get; set; } = new List<TripInstanceModel>();
    }
}