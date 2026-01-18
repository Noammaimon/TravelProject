using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using TravelProject.Data;
using TravelProject.Models;
using System.Linq;
using System;
using Oracle.ManagedDataAccess.Client;

namespace TravelProject.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration config)
        {
            _logger = logger;
            _configuration = config.GetConnectionString("TravelAgencyDB");

        }

        public async Task<IActionResult> Index(string searchString, string tripType, string sortOrder, decimal? minPrice, decimal? maxPrice, DateTime? travelDate)
        {
            var tripsMap = new Dictionary<int, TripModel>();
            var tripReviews = new List<TripReviewModel>();
            var siteReviewsOnly = new List<SiteReviewModel>(); 
            string connectionString = _configuration;

            using (OracleConnection connection = new OracleConnection(connectionString))
            {
                string sqlQuery = @"
            SELECT t.*, 
                   i.ID AS INSTANCE_ID, i.START_DATE, i.END_DATE, i.PRICE, 
                   i.ROOMS_AVAILABLE, i.ORIGINAL_PRICE, i.DISCOUNT_EXPIRATION,
                   (SELECT COUNT(*) FROM TRAVEL_PROJECT.ORDERS o WHERE o.INSTANCE_ID = i.ID) as POPULARITY
            FROM TRAVEL_PROJECT.TRIPS t
            LEFT JOIN TRAVEL_PROJECT.TRIP_INSTANCES i ON t.ID = i.TRIP_MODEL_ID
            WHERE 1=1";

                using (OracleCommand command = new OracleCommand())
                {
                    command.BindByName = true;
                    if(!string.IsNullOrEmpty(searchString))
{
                        sqlQuery += @" AND (UPPER(t.DESTINATION) LIKE :search 
                   OR UPPER(t.COUNTRY) LIKE :search 
                   OR UPPER(t.DESCRIPTION) LIKE :search)";
                        command.Parameters.Add(":search", $"%{searchString.ToUpper()}%");
                    }
                    if (!string.IsNullOrEmpty(tripType))
                    {
                        sqlQuery += " AND UPPER(TRIM(i.TRIPTYPE)) = UPPER(TRIM(:type))";
                        command.Parameters.Add(":type", tripType);
                    }

                    if (minPrice.HasValue)
                    {
                        sqlQuery += " AND i.PRICE >= :minP";
                        command.Parameters.Add(":minP", minPrice.Value);
                    }

                    if (maxPrice.HasValue)
                    {
                        sqlQuery += " AND i.PRICE <= :maxP";
                        command.Parameters.Add(":maxP", maxPrice.Value);
                    }

                    if (travelDate.HasValue)
                    {
                        sqlQuery += " AND TRUNC(i.START_DATE) = TRUNC(:tDate)";
                        command.Parameters.Add(":tDate", travelDate.Value);
                    }


                    if (!string.IsNullOrEmpty(sortOrder))
                    {
                        if (sortOrder == "discount")
                        {
                            sqlQuery += " AND i.ORIGINAL_PRICE > i.PRICE";
                        }
                        switch (sortOrder)
                        {
                            case "popular":
                                sqlQuery += " ORDER BY POPULARITY DESC";
                                break;
                            
                            case "price_asc":
                                sqlQuery += " ORDER BY i.PRICE ASC";
                                break;
                            case "price_desc":
                                sqlQuery += " ORDER BY i.PRICE DESC";
                                break;
                            default:
                                sqlQuery += " ORDER BY i.START_DATE ASC"; 
                                break;
                        }
                    }
                    else
                    {
                        sqlQuery += " ORDER BY i.START_DATE ASC";
                    }
                    command.CommandText = sqlQuery;
                    command.Connection = connection;
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int tripId = Convert.ToInt32(reader["ID"]);
                            if (!tripsMap.ContainsKey(tripId))
                            {
                                tripsMap[tripId] = new TripModel
                                {
                                    Id = tripId,
                                    Destination = reader["DESTINATION"].ToString(),
                                    Country = reader["COUNTRY"].ToString(),
                                    TripType = reader["TRIPTYPE"].ToString(),
                                    ImagePaths = reader["IMAGEPATHS"]?.ToString(),
                                    Description = reader["DESCRIPTION"]?.ToString(),
                                    Instances = new List<TripInstanceModel>()
                                };
                            }
                            if (reader["INSTANCE_ID"] != DBNull.Value)
                            {
                                var instance = new TripInstanceModel
                                {
                                    Id = Convert.ToInt32(reader["INSTANCE_ID"]),
                                    StartDate = reader["START_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["START_DATE"]) : DateTime.MinValue,
                                    EndDate = reader["END_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["END_DATE"]) : DateTime.MinValue,
                                    Price = Convert.ToDecimal(reader["PRICE"]),
                                    RoomsAvailable = Convert.ToInt32(reader["ROOMS_AVAILABLE"]),
                                    OriginalPrice = reader["ORIGINAL_PRICE"] != DBNull.Value ? Convert.ToDecimal(reader["ORIGINAL_PRICE"]) : null
                                };
                                
                                tripsMap[tripId].Instances.Add(instance);
                            }
                        }
                    }
                }

                string reviewQuery = @"
            SELECT * FROM (
                SELECT r.RATING, r.COMMENT_TEXT, r.REVIEW_DATE, u.USERNAME 
                FROM TRAVEL_PROJECT.TRIP_REVIEWS r
                JOIN TRAVEL_PROJECT.USERS u ON r.USER_ID = u.ID
                ORDER BY r.REVIEW_DATE DESC
            ) WHERE ROWNUM <= 3";

                using (OracleCommand revCommand = new OracleCommand(reviewQuery, connection))
                using (var revReader = await revCommand.ExecuteReaderAsync())
                {
                    while (await revReader.ReadAsync())
                    {
                        tripReviews.Add(new TripReviewModel
                        {
                            Rating = Convert.ToInt32(revReader["RATING"]),
                            CommentText = revReader["COMMENT_TEXT"].ToString(),
                            ReviewDate = Convert.ToDateTime(revReader["REVIEW_DATE"]),
                            Username = revReader["USERNAME"].ToString()
                        });
                    }
                }

                string siteReviewQuery = @"
            SELECT * FROM (
                SELECT RATING, COMMENT_TEXT, CREATED_AT, USER_NAME
                FROM TRAVEL_PROJECT.SITE_REVIEWS
                ORDER BY CREATED_AT DESC
            ) WHERE ROWNUM <= 5";

                using (OracleCommand siteRevCmd = new OracleCommand(siteReviewQuery, connection))
                using (var siteRevReader = await siteRevCmd.ExecuteReaderAsync())
                {
                    while (await siteRevReader.ReadAsync())
                    {
                        siteReviewsOnly.Add(new SiteReviewModel
                        {
                            Rating = Convert.ToInt32(siteRevReader["RATING"]),
                            CommentText = siteRevReader["COMMENT_TEXT"].ToString(),
                            ReviewDate = Convert.ToDateTime(siteRevReader["CREATED_AT"]),
                            Username = siteRevReader["USER_NAME"].ToString()
                        });
                    }
                }
            }

            ViewBag.TripTypes = new List<string> { "משפחה", "ירח דבש", "הרפתקאות", "שיט", "יוקרה" };
            ViewBag.SiteReviews = siteReviewsOnly;

            ViewBag.TripReviews = tripReviews;

            var result = tripsMap.Values.ToList();

            result = sortOrder switch
            {
                "price_asc" => result.OrderBy(t => t.Instances.Any() ? t.Instances.Min(i => i.Price) : 0).ToList(),
                "price_desc" => result.OrderByDescending(t => t.Instances.Any() ? t.Instances.Min(i => i.Price) : 0).ToList(),
                "category" => result.OrderBy(t => t.TripType).ToList(),
                "date" => result.OrderBy(t => t.Instances.Any() ? t.Instances.Min(i => i.StartDate) : DateTime.MaxValue).ToList(),
                "popular" => result.OrderByDescending(t => t.Instances.Count).ToList(), // מיון פשוט לפי כמות מופעים או הזמנות
                _ => result.OrderBy(t => t.Instances.Any() ? t.Instances.Min(i => i.StartDate) : DateTime.MaxValue).ToList()
            };

            return View(result);
        }

       

        [HttpPost]
        public async Task<IActionResult> SiteReviews(int rating, string comment)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string username = HttpContext.Session.GetString("Username") ?? "אורח";

            using (OracleConnection connection = new OracleConnection(_configuration))
            {
                string sql = @"INSERT INTO TRAVEL_PROJECT.SITE_REVIEWS 
                       (USER_ID, USER_NAME, RATING, COMMENT_TEXT, CREATED_AT) 
                       VALUES (:p_uid, :p_uname, :p_rate, :p_comm, CURRENT_TIMESTAMP)";

                using (OracleCommand cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add(":p_uid", userId ?? (object)DBNull.Value);
                    cmd.Parameters.Add(":p_uname", username);
                    cmd.Parameters.Add(":p_rate", rating);
                    cmd.Parameters.Add(":p_comm", comment);

                    await connection.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
