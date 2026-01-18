using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using TravelProject.Models;

public class TripInstanceModel
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int TripModelId { get; set; }

    [Required(ErrorMessage = "חובה להזין תיאור למועד זה")]
    [Display(Name = "תיאור המסלול")]
    public string Description { get; set; }

    [Required(ErrorMessage = "חובה לבחור סוג נופש")]
    [Display(Name = "סוג נופש")]
    [Column("TRIPTYPE")]
    public string TripType { get; set; }   

    [Required]
    [Display(Name = "תאריך יציאה")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [Required]
    [Display(Name = "תאריך חזרה")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [Required]
    [Display(Name = "מחיר")]
    public decimal Price { get; set; }

    [Display(Name = "מחיר מקורי")]
    [Column("ORIGINAL_PRICE")]
    public decimal? OriginalPrice { get; set; }

    [Display(Name = "תאריך סיום הנחה")]
    [Column("DISCOUNT_EXPIRATION")]
    public DateTime? DiscountExpiration { get; set; }

    [Required]
    [Display(Name = "חדרים זמינים")]
    public int RoomsAvailable { get; set; }

    [ForeignKey("TripModelId")]
    public virtual TripModel TripTemplate { get; set; }

    [Column("POPULARITY")]
    public int PopularityCount { get; set; }

    [Display(Name = "נתיב מסלול טיול")]
    [Column("FILE_PATH")]
    public string? FilePath { get; set; }

    [Display(Name = "ממתינים בתור")]
    [NotMapped]
    public int WaitingCount { get; set; }

    [Display(Name = "מגבלת גיל")]
    [Range(0, 120, ErrorMessage = "נא להזין גיל תקין (0-120)")]
    public int AgeLimitation { get; set; }

    [NotMapped]
    public int OrdersCount { get; set; }
    [NotMapped]
    public bool IsOnDiscount => OriginalPrice.HasValue &&
                               OriginalPrice > Price &&
                               (!DiscountExpiration.HasValue || DiscountExpiration >= DateTime.Now);
}