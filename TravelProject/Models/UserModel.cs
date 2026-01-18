using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TravelProject.Models
{
    [Table("USERS")]
    public class UserModel
    {
        
            [Key]
        [Column("ID")]
        public int Id { get; set; }


            [Required(ErrorMessage = "Username is required")]
            [StringLength(20, MinimumLength = 3, ErrorMessage = "Username must be between 3-20 characters")]
            [Display(Name = "Username")]
        [Column("USERNAME")]

        public string Username { get; set; }

            [Required(ErrorMessage = "Email is required for notifications")]
            [EmailAddress(ErrorMessage = "Invalid Email Address")]
        [Column("EMAIL")]
        public string Email { get; set; }
          

            [Required(ErrorMessage = "Password is required")]
            [DataType(DataType.Password)]
        [Column("PASSWORD")]
        public string Password { get; set; }

            [Required(ErrorMessage = "FirstName is required")]
        [Column("FIRST_NAME")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "LastName is required")]
        [Column("LAST_NAME")]
        public string LastName { get; set; }


        [Column("IS_ADMIN")]
        public bool IsAdmin { get; set; } = false;

    }
}