
namespace TravelProject.Models
{
    public class OrderModel
    {
        public int TripId { get; set; }
        public int Id { get; set; }
        public int UserId { get; set; }
        public int InstanceId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public string? FilePath { get; set; }

        public string? Destination { get; set; }
        public DateTime StartDate { get; set; }
    }
}