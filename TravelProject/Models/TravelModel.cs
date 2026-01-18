
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace TravelProject.Models
{
    public class TravelModel
    {
        [Required(ErrorMessage = "USER_ID is Required")]
        [StringLength(6, MinimumLength = 3, ErrorMessage = "USER_ID must be exactly 3 characters")]
        public string USER_ID { get; set; }

        [Required(ErrorMessage = "FIRST_NAME is Required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "FIRST_NAME must be between 2 and 50 letters")]
        public string FIRST_NAME { get; set; }

        [Required(ErrorMessage = "LAST_NAME is Required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "LAST_NAME must be between 2 and 50 letters")]
        public string LAST_NAME { get; set; }

        [NotMapped]
        [Range(0, 100, ErrorMessage = "אחוז הנחה חייב להיות בין 0 ל-100")]
        [Display(Name = "אחוז הנחה (%)")]
        public int? DiscountPercentage { get; set; }
    }
}
