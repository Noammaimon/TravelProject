using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TravelProject.Data;
using TravelProject.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace TravelProject.Controllers
{
    public class TripsController : Controller
    {
        private readonly string _configuration;
        public TripsController(IConfiguration config)
        {
            _configuration = config.GetConnectionString("TravelAgencyDB");
        }

        public async Task<IActionResult> Index(string searchString, string tripType, string sortOrder)
        {
            var trips = new List<TripModel>();
            string connectionString = _configuration;

            using (OracleConnection connection = new OracleConnection(connectionString))
            {
                string sqlQuery = @"
            SELECT T.ID, T.DESTINATION, T.COUNTRY, T.IMAGEPATHS,
                   I.ID AS INSTANCE_ID, I.PRICE, I.ROOMS_AVAILABLE, I.START_DATE, I.END_DATE, 
                   I.DESCRIPTION, I.TRIPTYPE, I.ORIGINAL_PRICE,I.AgeLimitation
            FROM TRAVEL_PROJECT.TRIPS T
            INNER JOIN TRAVEL_PROJECT.TRIP_INSTANCES I ON T.ID = I.TRIP_MODEL_ID
            WHERE I.START_DATE >= CURRENT_DATE";



                sqlQuery += " GROUP BY T.ID, T.DESTINATION, T.COUNTRY, T.TRIPTYPE, T.IMAGEPATHS, T.DESCRIPTION";

                if (sortOrder == "price_asc") sqlQuery += " ORDER BY PRICE ASC";
                else if (sortOrder == "price_desc") sqlQuery += " ORDER BY PRICE DESC";

                using (OracleCommand command = new OracleCommand())
                {
                    if (!string.IsNullOrEmpty(searchString))
                    {
                        string searchLower = searchString.ToLower();

                        sqlQuery += " AND (LOWER(t.DESTINATION) LIKE :search OR LOWER(t.DESCRIPTION) LIKE :search OR LOWER(t.COUNTRY) LIKE :search)";
                        command.Parameters.Add(":search", $"%{searchLower}%");
                    }

                    if (!string.IsNullOrEmpty(tripType))
                    {
                        sqlQuery += " AND TRIPTYPE = :type";
                        command.Parameters.Add(":type", tripType);
                    }

                    if (sortOrder == "price_asc") sqlQuery += " ORDER BY PRICE ASC";
                    else if (sortOrder == "price_desc") sqlQuery += " ORDER BY PRICE DESC";

                    command.CommandText = sqlQuery;
                    command.Connection = connection;
                    await connection.OpenAsync();

                    ViewBag.TripTypes = new List<string> { "משפחות", "זוגות", "בטן גב", "טיולי טבע" };

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            trips.Add(new TripModel
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                Destination = reader["DESTINATION"]?.ToString(),
                                Country = reader["COUNTRY"]?.ToString(),

                                Price = reader["PRICE"] != DBNull.Value ? Convert.ToDecimal(reader["PRICE"]) : 0,
                                RoomsAvailable = reader["ROOMS_AVAILABLE"] != DBNull.Value ? Convert.ToInt32(reader["ROOMS_AVAILABLE"]) : 0,

                                ImagePaths = reader["IMAGEPATHS"]?.ToString(),
                                Description = reader["DESCRIPTION"]?.ToString(),

                                OriginalPrice = reader["ORIGINAL_PRICE"] != DBNull.Value ? Convert.ToDecimal(reader["ORIGINAL_PRICE"]) : (decimal?)null,
                                DiscountExpiration = reader["DISCOUNT_EXPIRATION"] != DBNull.Value ? Convert.ToDateTime(reader["DISCOUNT_EXPIRATION"]) : (DateTime?)null,
                                AgeLimit = reader["AgeLimitation"] != DBNull.Value ? Convert.ToInt32(reader["AgeLimitation"]) : 0,
                                StartDate = reader["START_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["START_DATE"]) : DateTime.Now,
                                EndDate = reader["END_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["END_DATE"]) : DateTime.Now
                            });
                        }
                    }
                }
            }
            return View(trips);
        }

        public IActionResult Create()
        {
            return View(new TripModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TripModel trip)
        {
            if (trip.ImageFiles != null && trip.ImageFiles.Count > 0)
            {
                List<string> filePaths = new List<string>();
                foreach (var file in trip.ImageFiles)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    filePaths.Add("/images/" + fileName);
                }
                trip.ImagePaths = string.Join(",", filePaths);
            }

            using (OracleConnection connection = new OracleConnection(_configuration))
            {
                await connection.OpenAsync();
                string sqlTrips = @"INSERT INTO TRAVEL_PROJECT.TRIPS 
            (ID, DESTINATION, COUNTRY, IMAGEPATHS) 
            VALUES (TRIPS_SEQ.NEXTVAL, :v1, :v2, :v3) 
            RETURNING ID INTO :newId";

                try
                {
                    decimal newTripId;
                    using (OracleCommand cmd1 = new OracleCommand(sqlTrips, connection))
                    {
                        cmd1.Parameters.Add(":v1", trip.Destination);
                        cmd1.Parameters.Add(":v2", trip.Country);
                        cmd1.Parameters.Add(":v3", (object)trip.ImagePaths ?? DBNull.Value);

                        OracleParameter idParam = new OracleParameter(":newId", OracleDbType.Decimal);
                        idParam.Direction = System.Data.ParameterDirection.Output;
                        cmd1.Parameters.Add(idParam);

                        await cmd1.ExecuteNonQueryAsync();
                        newTripId = ((Oracle.ManagedDataAccess.Types.OracleDecimal)idParam.Value).Value;
                    }
                    await connection.CloseAsync();

                    TempData["SuccessMessage"] = "היעד נוצר בהצלחה! כעת הוסף לו מועד.";
                    return RedirectToAction("AddDate", new { id = (int)newTripId });
                }
                catch (Exception ex)
                {
                    ViewBag.Error = "שגיאה ביצירת היעד: " + ex.Message;
                }
            }
            return View(trip);
        }


        public async Task<IActionResult> AddDate(int id)
        {
            decimal currentPrice = 0;
            int currentRooms = 0;
            string destinationName = "";

            using (OracleConnection connection = new OracleConnection(_configuration))
            {
                string sql = @"SELECT * FROM (
                        SELECT T.DESTINATION, I.PRICE, I.ROOMS_AVAILABLE 
                        FROM TRAVEL_PROJECT.TRIPS T
                        LEFT JOIN TRAVEL_PROJECT.TRIP_INSTANCES I ON T.ID = I.TRIP_MODEL_ID
                        WHERE T.ID = :id
                        ORDER BY I.ID ASC
                       ) WHERE ROWNUM = 1";

                using (OracleCommand cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add(":id", id);
                    await connection.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            destinationName = reader["DESTINATION"].ToString();
                            currentPrice = reader["PRICE"] != DBNull.Value ? Convert.ToDecimal(reader["PRICE"]) : 0;
                            currentRooms = reader["ROOMS_AVAILABLE"] != DBNull.Value ? Convert.ToInt32(reader["ROOMS_AVAILABLE"]) : 0;
                        }
                    }
                }
            }

            ViewBag.TripModelId = id;
            ViewBag.DestinationName = destinationName;
            ViewBag.DefaultPrice = currentPrice;
            ViewBag.DefaultRooms = currentRooms;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDate(TripModel model, IFormFile pdfFile)
        {
            int tripId = int.Parse(Request.Form["tripModelId"]);

            if (pdfFile != null && pdfFile.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(pdfFile.FileName);
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);
                using (var stream = new FileStream(path, FileMode.Create)) { await pdfFile.CopyToAsync(stream); }
                model.FilePath = "/uploads/" + fileName;
            }

            try
            {
                using (OracleConnection connection = new OracleConnection(_configuration))
                {

                    string sqlQuery = @"INSERT INTO TRAVEL_PROJECT.TRIP_INSTANCES 
    (ID, TRIP_MODEL_ID, START_DATE, END_DATE, PRICE, ROOMS_AVAILABLE, DESCRIPTION, TRIPTYPE, FILE_PATH, AgeLimitation) 
    VALUES (TRAVEL_PROJECT.TRIP_INSTANCES_SEQ.NEXTVAL, :v_modelId, :v_sd, :v_ed, :v_price, :v_ra, :v_desc, :v_tt, :v_fpath, :v_age)";
                    using (OracleCommand command = new OracleCommand(sqlQuery, connection))
                    {
                        command.BindByName = true;

                        command.Parameters.Add("v_modelId", tripId);
                        command.Parameters.Add("v_sd", model.StartDate);
                        command.Parameters.Add("v_ed", model.EndDate);
                        command.Parameters.Add("v_price", model.Price);
                        command.Parameters.Add("v_ra", model.RoomsAvailable);

                        command.Parameters.Add("v_desc", (object)model.Description ?? DBNull.Value);
                        command.Parameters.Add("v_tt", (object)model.TripType ?? DBNull.Value);
                        command.Parameters.Add("v_fpath", (object)model.FilePath ?? DBNull.Value);
                        command.Parameters.Add("v_age", model.AgeLimit ?? 0);
                        command.Parameters.Add("v_oprice", (object)model.OriginalPrice ?? DBNull.Value);
                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                    }
                    await connection.CloseAsync();

                }
                TempData["SuccessMessage"] = "המועד נוסף בהצלחה!";
                return RedirectToAction("Details", new { id = tripId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "שגיאה: " + ex.Message);
                return View(model);
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return RedirectToAction("Index");
            if (HttpContext.Session.GetInt32("IsAdmin") != 1) return RedirectToAction("Index", "Home");

            TripModel trip = null;
            string connectionString = _configuration;

            using (OracleConnection connection = new OracleConnection(connectionString))
            {
                string sqlQuery = @"
            SELECT t.ID, t.DESTINATION, t.COUNTRY, t.IMAGEPATHS, t.AGELIMIT,
                   i.ID as INSTANCE_ID, i.PRICE, i.START_DATE, i.END_DATE, 
                   i.ROOMS_AVAILABLE, i.ORIGINAL_PRICE, i.DISCOUNT_EXPIRATION,
                   i.FILE_PATH, i.DESCRIPTION, i.TRIPTYPE,i.AgeLimitation
            FROM TRAVEL_PROJECT.TRIPS t
            JOIN TRAVEL_PROJECT.TRIP_INSTANCES i ON t.ID = i.TRIP_MODEL_ID
            WHERE i.ID = :instanceId";

                using (OracleCommand command = new OracleCommand(sqlQuery, connection))
                {
                    command.Parameters.Add("instanceId", id);
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            trip = new TripModel
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                InstanceId = Convert.ToInt32(reader["INSTANCE_ID"]),
                                Destination = reader["DESTINATION"].ToString(),
                                Country = reader["COUNTRY"].ToString(),
                                Description = reader["DESCRIPTION"].ToString(),
                                TripType = reader["TRIPTYPE"].ToString(),
                                AgeLimit = reader["AgeLimitation"] != DBNull.Value ? Convert.ToInt32(reader["AgeLimitation"]) : 0,
                                ImagePaths = reader["IMAGEPATHS"] != DBNull.Value ? reader["IMAGEPATHS"].ToString() : "",
                                FilePath = reader["FILE_PATH"] != DBNull.Value ? reader["FILE_PATH"].ToString() : null,
                                Price = reader["PRICE"] != DBNull.Value ? Convert.ToDecimal(reader["PRICE"]) : 0,
                                RoomsAvailable = reader["ROOMS_AVAILABLE"] != DBNull.Value ? Convert.ToInt32(reader["ROOMS_AVAILABLE"]) : 0,
                                StartDate = reader["START_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["START_DATE"]) : DateTime.Now,
                                EndDate = reader["END_DATE"] != DBNull.Value ? Convert.ToDateTime(reader["END_DATE"]) : DateTime.Now,
                                OriginalPrice = reader["ORIGINAL_PRICE"] != DBNull.Value ? Convert.ToDecimal(reader["ORIGINAL_PRICE"]) : null,
                                DiscountExpiration = reader["DISCOUNT_EXPIRATION"] != DBNull.Value ? Convert.ToDateTime(reader["DISCOUNT_EXPIRATION"]) : null
                            };
                        }
                    }
                }
            }

            if (trip == null) return NotFound();
            return View(trip);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TripModel trip, IFormFile imageFile, IFormFile pdfFile)
        {
            if (HttpContext.Session.GetInt32("IsAdmin") != 1) return RedirectToAction("Index", "Home");
            if (id != trip.InstanceId) return NotFound();

            if (pdfFile != null && pdfFile.Length > 0)
            {
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(pdfFile.FileName);

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "itineraries");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }
                trip.FilePath = "/uploads/itineraries/" + uniqueFileName;
            }

            ModelState.Remove("imageFile");
            ModelState.Remove("pdfFile");
            ModelState.Remove("ImagePaths");

            decimal regularPrice = trip.Price;
            if (trip.DiscountPercentage.HasValue && trip.DiscountPercentage.Value > 0)
            {
                trip.OriginalPrice = regularPrice;
                decimal discountAmount = regularPrice * ((decimal)trip.DiscountPercentage.Value / 100);
                trip.Price = regularPrice - discountAmount;
            }

            string connectionString = _configuration;
            using (OracleConnection connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                       
                       
                        string sqlInstances = @"UPDATE TRAVEL_PROJECT.TRIP_INSTANCES 
                                        SET PRICE = :p, START_DATE = :sd, END_DATE = :ed, 
                                            ROOMS_AVAILABLE = :ra, ORIGINAL_PRICE = :op, 
                                            DISCOUNT_EXPIRATION = :de, FILE_PATH = :fpath,
                                            DESCRIPTION = :v_desc, TRIPTYPE = :v_tt,AgeLimitation = :age
                                        WHERE ID = :instanceId";

                        using (OracleCommand cmd2 = new OracleCommand(sqlInstances, connection))
                        {
                            cmd2.BindByName = true;
                            cmd2.Parameters.Add("p", trip.Price);
                            cmd2.Parameters.Add("sd", trip.StartDate);
                            cmd2.Parameters.Add("ed", trip.EndDate);
                            cmd2.Parameters.Add("ra", trip.RoomsAvailable);
                            cmd2.Parameters.Add("op", (object)trip.OriginalPrice ?? DBNull.Value);
                            cmd2.Parameters.Add("de", (object)trip.DiscountExpiration ?? DBNull.Value);
                            cmd2.Parameters.Add("fpath", (object)trip.FilePath ?? DBNull.Value);
                            cmd2.Parameters.Add("v_desc", trip.Description); 
                            cmd2.Parameters.Add("v_tt", trip.TripType);
                            cmd2.Parameters.Add("age", trip.AgeLimit ?? 0);
                            cmd2.Parameters.Add("instanceId", id);        
                            await cmd2.ExecuteNonQueryAsync();
                        }

                        await SharedLogic.ProcessWaitingList(id, connection, transaction);
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", " בעדכון: " + ex.Message);
                        return View(trip);
                    }
                }
            }

            TempData["SuccessMessage"] = "הטיול עודכן בהצלחה!";
            return RedirectToAction("Index", "Admin");
        }

        public async Task<IActionResult> Details(int? id, int? instanceId)
        {
            if (id == null) return RedirectToAction("Index");

            TripModel trip = null;

            using (OracleConnection connection = new OracleConnection(_configuration))
            {
                await connection.OpenAsync();

                string sqlTrip = "SELECT * FROM TRAVEL_PROJECT.TRIPS WHERE ID = :tripId";
                using (OracleCommand cmd1 = new OracleCommand(sqlTrip, connection))
                {
                    cmd1.Parameters.Add(":tripId", id);
                    using (var reader = await cmd1.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            trip = new TripModel
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                Destination = reader["DESTINATION"]?.ToString(),
                                Country = reader["COUNTRY"]?.ToString(),
                                Description = reader["DESCRIPTION"]?.ToString(),
                                TripType = reader["TRIPTYPE"]?.ToString(),
                                ImagePaths = reader["IMAGEPATHS"]?.ToString(),
                                AgeLimit = reader["AGELIMIT"] != DBNull.Value ? Convert.ToInt32(reader["AGELIMIT"]) : (int?)null,
                                Instances = new List<TripInstanceModel>()
                            };
                        }
                    }
                }

                if (trip == null) return NotFound();


                string sqlInstances = @"SELECT ID, START_DATE, END_DATE, PRICE, ROOMS_AVAILABLE, 
                         ORIGINAL_PRICE, DISCOUNT_EXPIRATION, DESCRIPTION, TRIPTYPE , AgeLimitation
                  FROM TRAVEL_PROJECT.TRIP_INSTANCES 
                  WHERE TRIP_MODEL_ID = :tripId ORDER BY START_DATE ASC";

                using (OracleCommand cmd2 = new OracleCommand(sqlInstances, connection))
                {
                    cmd2.Parameters.Add(":tripId", id);
                    using (var reader = await cmd2.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var instId = Convert.ToInt32(reader["ID"]);

                            int currentWaitCount = 0;
                            string sqlWait = @"SELECT COUNT(*) FROM TRAVEL_PROJECT.ORDERS 
                       WHERE INSTANCE_ID = :instId 
                       AND UPPER(TRIM(STATUS)) = 'WAITING'";

                            using (OracleCommand cmdWait = new OracleCommand(sqlWait, connection))
                            {
                                cmdWait.Parameters.Add(":instId", instId);
                                currentWaitCount = Convert.ToInt32(await cmdWait.ExecuteScalarAsync());
                            }

                            var currentPrice = Convert.ToDecimal(reader["PRICE"]);
                            var originalPrice = reader["ORIGINAL_PRICE"] != DBNull.Value ? Convert.ToDecimal(reader["ORIGINAL_PRICE"]) : (decimal?)null;
                            var expiration = reader["DISCOUNT_EXPIRATION"] != DBNull.Value ? Convert.ToDateTime(reader["DISCOUNT_EXPIRATION"]) : (DateTime?)null;

                            if (expiration.HasValue && expiration.Value < DateTime.Now && originalPrice.HasValue)
                            {
                                currentPrice = originalPrice.Value;
                                originalPrice = null;
                                expiration = null;

                            }

                            trip.Instances.Add(new TripInstanceModel
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                StartDate = Convert.ToDateTime(reader["START_DATE"]),
                                EndDate = Convert.ToDateTime(reader["END_DATE"]),
                                Price = currentPrice,
                                RoomsAvailable = Convert.ToInt32(reader["ROOMS_AVAILABLE"]),
                                OriginalPrice = originalPrice,
                                DiscountExpiration = expiration,
                                WaitingCount = currentWaitCount,
                                Description = reader["DESCRIPTION"]?.ToString(),
                                TripType = reader["TRIPTYPE"]?.ToString(),
                                AgeLimitation = Convert.ToInt32(reader["AgeLimitation"]),
                            });
                        }
                    }
                }
                if (trip.Instances != null && trip.Instances.Any())
                {
                    var selectedInstance = trip.Instances.FirstOrDefault(i => i.Id == instanceId)
                                          ?? trip.Instances.First();

                    string sqlWaitCount = @"SELECT COUNT(*) 
                            FROM TRAVEL_PROJECT.ORDERS 
                            WHERE INSTANCE_ID = :instId 
                            AND UPPER(TRIM(STATUS)) = 'WAITING'";

                    using (OracleCommand cmdWait = new OracleCommand(sqlWaitCount, connection))
                    {
                        cmdWait.Parameters.Add(":instId", selectedInstance.Id);
                        ViewBag.WaitingCount = Convert.ToInt32(await cmdWait.ExecuteScalarAsync());
                    }

                    trip.InstanceId = selectedInstance.Id;
                    trip.Price = selectedInstance.Price;
                    trip.RoomsAvailable = selectedInstance.RoomsAvailable;
                    trip.StartDate = selectedInstance.StartDate;
                    trip.EndDate = selectedInstance.EndDate;
                    trip.OriginalPrice = selectedInstance.OriginalPrice;
                    trip.DiscountExpiration = selectedInstance.DiscountExpiration;
                }

                var reviews = new List<TripReviewModel>();
                string sqlReviews = @"SELECT r.RATING, r.COMMENT_TEXT, r.REVIEW_DATE, u.USERNAME 
                      FROM TRAVEL_PROJECT.TRIP_REVIEWS r
                      JOIN TRAVEL_PROJECT.USERS u ON r.USER_ID = u.ID
                      WHERE r.TRIP_ID = :tripId
                      ORDER BY r.REVIEW_DATE DESC";

                using (OracleCommand cmd3 = new OracleCommand(sqlReviews, connection))
                {
                    cmd3.Parameters.Add(":tripId", id);
                    using (var reader = await cmd3.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            reviews.Add(new TripReviewModel
                            {
                                Username = reader["USERNAME"].ToString(),
                                Rating = Convert.ToInt32(reader["RATING"]),
                                CommentText = reader["COMMENT_TEXT"].ToString(),
                                ReviewDate = Convert.ToDateTime(reader["REVIEW_DATE"])
                            });
                        }
                    }
                }
                ViewBag.TripReviews = reviews;

                ViewBag.CanReview = HttpContext.Session.GetInt32("UserId") != null;
            }

            if (trip.Instances != null && trip.Instances.Any())
            {
                var selectedInstance = trip.Instances.FirstOrDefault(i => i.Id == instanceId)
                                        ?? trip.Instances.First();

                trip.InstanceId = selectedInstance.Id;
                trip.Price = selectedInstance.Price;
                trip.StartDate = selectedInstance.StartDate;
                trip.EndDate = selectedInstance.EndDate;
                trip.RoomsAvailable = selectedInstance.RoomsAvailable;
                trip.OriginalPrice = selectedInstance.OriginalPrice;
                trip.DiscountExpiration = selectedInstance.DiscountExpiration;
            }


            return View(trip);
        }



        [HttpPost]
        public async Task<IActionResult> AddTripReview(int instanceId, int tripId, int rating, string comment, string returnUrl = "Details")
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Users");

            using (OracleConnection conn = new OracleConnection(_configuration))
            {
                await conn.OpenAsync();
                string sql = @"INSERT INTO TRAVEL_PROJECT.TRIP_REVIEWS 
                       (USER_ID, TRIP_ID, RATING, COMMENT_TEXT, REVIEW_DATE) 
                       VALUES (:p_uid, :p_tid, :p_rate, :p_comm, CURRENT_TIMESTAMP)";

                using (OracleCommand cmd = new OracleCommand(sql, conn))
                {
                    cmd.Parameters.Add("p_uid", userId);
                    cmd.Parameters.Add("p_tid", instanceId);
                    cmd.Parameters.Add("p_rate", rating);
                    cmd.Parameters.Add("p_comm", comment);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            if (returnUrl == "PersonalArea")
            {
                return RedirectToAction("Index", "Customer"); 
            }
            return RedirectToAction("Details", new { id = tripId });
        }


        public async Task<IActionResult> Delete(int instanceId, int tripId)
        {
            if (HttpContext.Session.GetInt32("IsAdmin") != 1)
            {
                return RedirectToAction("Index", "Home");
            }
            using (OracleConnection connection = new OracleConnection(_configuration))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string sqlDeleteInstance = "DELETE FROM TRAVEL_PROJECT.TRIP_INSTANCES WHERE ID = :instanceId";
                        using (OracleCommand cmd = new OracleCommand(sqlDeleteInstance, connection))
                        {
                            cmd.Parameters.Add(":instanceId", instanceId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string sqlCheckRemaining = "SELECT COUNT(*) FROM TRAVEL_PROJECT.TRIP_INSTANCES WHERE TRIP_MODEL_ID = :tripId";
                        int remainingCount = 0;
                        using (OracleCommand cmd = new OracleCommand(sqlCheckRemaining, connection))
                        {
                            cmd.Parameters.Add(":tripId", tripId);
                            remainingCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        if (remainingCount == 0)
                        {
                            string sqlDeleteTrip = "DELETE FROM TRAVEL_PROJECT.TRIPS WHERE ID = :tripId";
                            using (OracleCommand cmd = new OracleCommand(sqlDeleteTrip, connection))
                            {
                                cmd.Parameters.Add(":tripId", tripId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            transaction.Commit();
                            TempData["SuccessMessage"] = "המועד והטיול הראשי נמחקו כיוון שלא נותרו מועדים נוספים.";
                            return RedirectToAction("Index", "Admin"); 
                        }

                        transaction.Commit();
                        TempData["SuccessMessage"] = "המועד נמחק בהצלחה.";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        TempData["ErrorMessage"] = "שגיאה במחיקה: " + ex.Message;
                    }
                }
            }
            return RedirectToAction("Details", new { id = tripId });
        }

    }   

    

}