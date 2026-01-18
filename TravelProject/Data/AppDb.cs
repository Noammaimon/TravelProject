using Microsoft.EntityFrameworkCore;
using TravelProject.Models; 

namespace TravelProject.Data
{
    public class AppDb : DbContext
    {
        public AppDb(DbContextOptions<AppDb> options) : base(options)
        {
        }

        public DbSet<UserModel> Users { get; set; }

        public DbSet<TripModel> Trips { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserModel>()
                .Property(u => u.Id)
                .UseHiLo("USERS_SEQ");

            modelBuilder.HasSequence("USERS_SEQ");

           
            //modelBuilder.Entity<UserModel>()
            //    .Property(u => u.IsAdmin)
            //    .HasConversion(
            //        val => val ? 1 : 0, 
            //        val => val == 1      
            //    );

            //var tripEntity = modelBuilder.Entity<TripModel>();
            //tripEntity.HasKey(e => e.Id);
            //tripEntity.ToTable("TRIPS", "TRAVEL_PROJECT");
            //tripEntity.Property(e => e.Id).UseHiLo("TRIPS_SEQ");
            //modelBuilder.HasSequence("TRIPS_SEQ");

            
            //foreach (var property in tripEntity.Metadata.GetProperties())
            //{
            //    property.SetColumnName(property.Name.ToUpper());
            //}

        }
    }

}




