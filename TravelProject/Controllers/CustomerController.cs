using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using TravelProject.Data;
using TravelProject.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq; 
namespace TravelProject.Controllers
{    //Noam Maimon 212994297

    public class CustomerController : Controller
    {
        private readonly string _configuration;
        private readonly IConfiguration _config;
        public CustomerController(IConfiguration config)
        {
            _configuration = config.GetConnectionString("TravelAgencyDB");
            _config = config;
        }



        public async Task<IActionResult> Index(string searchString, string tripType, string sortOrder, DateTime? travelDate, decimal? minPrice, decimal? maxPrice)
        {
            List<TripModel> trips = new List<TripModel>();
            string connectionString = _configuration;

            using (OracleConnection connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                string sqlQuery = "SELECT * FROM TRAVEL_PROJECT.TRIPS WHERE 1=1";
                using (OracleCommand command = new OracleCommand(sqlQuery, connection))
                {
                    if (!string.IsNullOrEmpty(searchString))
                    {
                        command.CommandText += " AND (UPPER(DESTINATION) LIKE :search OR UPPER(COUNTRY) LIKE :search)";
                        command.Parameters.Add(":search", $"%{searchString.ToUpper()}%");
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            trips.Add(new TripModel
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                Destination = reader["DESTINATION"].ToString(),
                                Country = reader["COUNTRY"].ToString(),
                                TripType = reader["TRIPTYPE"].ToString(),
                                ImagePaths = reader["IMAGEPATHS"]?.ToString(),
                                Description = reader["DESCRIPTION"]?.ToString(),
                                Instances = new List<TripInstanceModel>() 
                            });
                        }
                    }
                }

                foreach (var trip in trips)
                {

                    string instSql = @"
                SELECT ID, START_DATE, PRICE, ROOMS_AVAILABLE, ORIGINAL_PRICE,
                (SELECT COUNT(*) FROM TRAVEL_PROJECT.ORDERS o WHERE o.INSTANCE_ID = i.ID) as ORDERS_COUNT
                FROM TRAVEL_PROJECT.TRIP_INSTANCES i 
                WHERE TRIP_MODEL_ID = :tid AND START_DATE > SYSDATE"; using (OracleCommand instCmd = new OracleCommand(instSql, connection))
                    {
                        instCmd.Parameters.Add("tid", trip.Id);
                        using (var reader = await instCmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                trip.Instances.Add(new TripInstanceModel
                                {
                                    Id = Convert.ToInt32(reader["ID"]),
                                    StartDate = Convert.ToDateTime(reader["START_DATE"]),
                                    Price = Convert.ToDecimal(reader["PRICE"]),
                                    RoomsAvailable = Convert.ToInt32(reader["ROOMS_AVAILABLE"]),
                                    OriginalPrice = reader["ORIGINAL_PRICE"] != DBNull.Value ? (decimal?)Convert.ToDecimal(reader["ORIGINAL_PRICE"]) : null,
                                    OrdersCount = Convert.ToInt32(reader["ORDERS_COUNT"])
                                });
                            }
                        }
                    }
                }
            }

            var filteredTrips = FilterAndSortTrips(trips, sortOrder, travelDate, minPrice, maxPrice);

            ViewBag.TripTypes = new List<string> { "משפחות", "זוגות", "בטן גב", "טיולי טבע" }; 
            return View(filteredTrips);
        }


        private List<TripModel> FilterAndSortTrips(List<TripModel> trips, string sortOrder, DateTime? travelDate, decimal? minPrice, decimal? maxPrice)
        {
            var filtered = trips.Where(t => t.Instances.Any()).ToList();

            if (travelDate.HasValue)
            {
                foreach (var trip in filtered)
                {
                    trip.Instances = trip.Instances.Where(i => i.StartDate >= travelDate.Value).ToList();
                }
                filtered = filtered.Where(t => t.Instances.Any()).ToList();
            }

            if (minPrice.HasValue)
            {
                foreach (var trip in filtered)
                {
                    trip.Instances = trip.Instances.Where(i => i.Price >= minPrice.Value).ToList();
                }
                filtered = filtered.Where(t => t.Instances.Any()).ToList();
            }

            if (maxPrice.HasValue)
            {
                foreach (var trip in filtered)
                {
                    trip.Instances = trip.Instances.Where(i => i.Price <= maxPrice.Value).ToList();
                }
                filtered = filtered.Where(t => t.Instances.Any()).ToList();
            }

            switch (sortOrder)
            {
                case "price_asc": 
                    filtered = filtered.OrderBy(t => t.Instances.Min(i => i.Price)).ToList();
                    break;

                case "price_desc":
                    filtered = filtered.OrderByDescending(t => t.Instances.Min(i => i.Price)).ToList();
                    break;

                case "discount":
                    filtered = filtered
                               .Where(t => t.Instances.Any(i => i.OriginalPrice.HasValue && i.OriginalPrice > i.Price))
                               .ToList();

                    foreach (var trip in filtered)
                    {
                        trip.Instances = trip.Instances
                                         .Where(i => i.OriginalPrice.HasValue && i.OriginalPrice > i.Price)
                                         .ToList();
                    }
                    filtered = filtered.OrderBy(t => t.Instances.Min(i => i.Price)).ToList();
                    break;

                case "popular":
                    filtered = filtered
                               .OrderByDescending(t => t.Instances.Sum(i => i.OrdersCount))
                               .ToList();
                    break;

                default:
                    break;
            }

            return filtered;
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Users");

            using (OracleConnection conn = new OracleConnection(_configuration))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string checkSql = "SELECT INSTANCE_ID, STATUS FROM TRAVEL_PROJECT.ORDERS WHERE ID = :p_oid AND USER_ID = :p_uid";
                        int instanceId = 0;
                        string currentStatus = "";

                        using (var checkCmd = new OracleCommand(checkSql, conn))
                        {
                            checkCmd.Transaction = transaction;
                            checkCmd.BindByName = true;
                            checkCmd.Parameters.Add("p_oid", orderId);
                            checkCmd.Parameters.Add("p_uid", userId);
                            using (var reader = await checkCmd.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync()) return NotFound();
                                instanceId = Convert.ToInt32(reader["INSTANCE_ID"]);
                                currentStatus = reader["STATUS"].ToString();
                            }
                        }

                        string updateOrderSql = "UPDATE TRAVEL_PROJECT.ORDERS SET STATUS = 'Cancelled' WHERE ID = :p_oid";
                        using (var updateCmd = new OracleCommand(updateOrderSql, conn))
                        {
                            updateCmd.Transaction = transaction;
                            updateCmd.BindByName = true;
                            updateCmd.Parameters.Add("p_oid", orderId);
                            await updateCmd.ExecuteNonQueryAsync();
                        }

                        if (currentStatus.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
                        {
                            string updateStockSql = "UPDATE TRAVEL_PROJECT.TRIP_INSTANCES SET ROOMS_AVAILABLE = ROOMS_AVAILABLE + 1 WHERE ID = :p_instId";
                            using (var stockCmd = new OracleCommand(updateStockSql, conn))
                            {
                                stockCmd.Transaction = transaction;
                                stockCmd.BindByName = true;
                                stockCmd.Parameters.Add("p_instId", instanceId);
                                await stockCmd.ExecuteNonQueryAsync();
                            }

                            await SharedLogic.ProcessWaitingList(instanceId, conn, transaction);
                        }

                        transaction.Commit();
                        TempData["Success"] = "הביטול בוצע בהצלחה.";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine("Oracle Error: " + ex.ToString());
                        TempData["Error"] = "שגיאה בביטול: " + ex.Message;
                    }
                }
            }
            return RedirectToAction("PersonalArea");
        }


        [HttpPost]
        public async Task<IActionResult> BookTrip(int instanceId)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Users");

            using (OracleConnection conn = new OracleConnection(_configuration))
            {
                await conn.OpenAsync();

                int roomsAvailable = 0;

                string checkSql = "SELECT ROOMS_AVAILABLE FROM TRIP_INSTANCES WHERE ID = :p_inst";

                using (OracleCommand checkCmd = new OracleCommand(checkSql, conn))
                {
                    checkCmd.BindByName = true;
                    checkCmd.Parameters.Add("p_inst", instanceId);

                    object result = await checkCmd.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        roomsAvailable = Convert.ToInt32(result);
                    }
                }

                string orderStatus = (roomsAvailable > 0) ? "Pending" : "Waiting";

                
                string sql = @"INSERT INTO TRAVEL_PROJECT.ORDERS (USER_ID, INSTANCE_ID, STATUS, ORDER_DATE) 
                       VALUES (:p_uid, :p_inst, :p_status, CURRENT_TIMESTAMP)";

                using (OracleCommand cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("p_uid", userId);
                    cmd.Parameters.Add("p_inst", instanceId);
                    cmd.Parameters.Add("p_status", orderStatus);

                    await cmd.ExecuteNonQueryAsync();
                }

               
                if (orderStatus == "Waiting")
                {
                    TempData["Warning"] = "הטיול מלא כרגע. נוספת לרשימת המתנה - נעדכן אותך כשיתפנה מקום.";
                }
                else
                {
                    TempData["Success"] = "הטיול נוסף בהצלחה! השלם את התשלום כדי להבטיח מקום.";
                }
            }

            return RedirectToAction("Cart");
        }

        public IActionResult Checkout(int orderId, decimal price)
        {
            ViewBag.OrderId = orderId;
            ViewBag.Price = price;
            return View();
        }

        [RequireHttps]
        [HttpPost]
        public async Task<IActionResult> ProcessPayment(int orderId, int? instanceId, string cardNumber, string expiry, string cvv)
        {
            cardNumber = cardNumber?.Replace(" ", "").Trim();
            bool isCardValid = !string.IsNullOrEmpty(cardNumber) && cardNumber.Length == 16 && cardNumber.All(char.IsDigit);

            bool isCvvValid = !string.IsNullOrEmpty(cvv) && cvv.Length == 3 && cvv.All(char.IsDigit);

            bool isExpiryValid = false;
            var expiryRegex = new System.Text.RegularExpressions.Regex(@"^\d{1,2}\/\d{2,4}$");

            if (!string.IsNullOrEmpty(expiry) && expiryRegex.IsMatch(expiry))
            {
                try
                {
                    var parts = expiry.Split('/');
                    int inputMonth = int.Parse(parts[0]);
                    int inputYear = int.Parse(parts[1]);
                    if (inputYear < 100) inputYear += 2000;

                    var now = DateTime.Now;
                    if (inputMonth >= 1 && inputMonth <= 12)
                    {
                        if (inputYear > now.Year || (inputYear == now.Year && inputMonth >= now.Month))
                        {
                            isExpiryValid = true;
                        }
                    }
                }
                catch { isExpiryValid = false; }
            }


            if (!isCardValid || !isCvvValid || !isExpiryValid)
            {
                TempData["ErrorMessage"] = "פרטי אשראי לא תקינים. ודא שהמספר מכיל 16 ספרות, ה-CVV מכיל 3 והתוקף עתידי.";

                if (orderId > 0)
                {
                    TempData["OpenPaymentModal"] = orderId;
                    return RedirectToAction("PersonalArea");
                }
                else
                {
                    TempData["OpenPaymentModalInstanceId"] = instanceId;
                    return Redirect(Request.Headers["Referer"].ToString());
                }
            }

            using (OracleConnection conn = new OracleConnection(_configuration))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                        if (userId == 0) return RedirectToAction("Login", "Users");

                        string checkLimitSql = "SELECT COUNT(*) FROM TRAVEL_PROJECT.ORDERS WHERE USER_ID = :u AND STATUS = 'Confirmed'";
                        using (var limitCmd = new OracleCommand(checkLimitSql, conn))
                        {
                            limitCmd.Parameters.Add("u", userId);
                            int activeOrdersCount = Convert.ToInt32(await limitCmd.ExecuteScalarAsync());

                            if (activeOrdersCount >= 3)
                            {
                                TempData["ErrorMessage"] = "לא ניתן להשלים את התשלום. יש לך כבר 3 חבילות מאושרות במערכת.";
                                return orderId > 0 ? RedirectToAction("PersonalArea") : Redirect(Request.Headers["Referer"].ToString());
                            }
                        }

                        if (orderId <= 0 && instanceId.HasValue)
                        {
                            string insertSql = "INSERT INTO TRAVEL_PROJECT.ORDERS (USER_ID, INSTANCE_ID, STATUS, ORDER_DATE) VALUES (:u, :i, 'Pending', CURRENT_TIMESTAMP)";
                            using (var insCmd = new OracleCommand(insertSql, conn))
                            {
                                insCmd.BindByName = true;
                                insCmd.Parameters.Add("u", userId);
                                insCmd.Parameters.Add("i", instanceId.Value);
                                await insCmd.ExecuteNonQueryAsync();
                            }

                            string selectIdSql = "SELECT MAX(ID) FROM TRAVEL_PROJECT.ORDERS WHERE USER_ID = :u AND INSTANCE_ID = :i";
                            using (var getIdCmd = new OracleCommand(selectIdSql, conn))
                            {
                                getIdCmd.BindByName = true;
                                getIdCmd.Parameters.Add("u", userId);
                                getIdCmd.Parameters.Add("i", instanceId.Value);
                                orderId = Convert.ToInt32(await getIdCmd.ExecuteScalarAsync());
                            }
                        }

                       
                        string updateStockSql = @"
                UPDATE TRAVEL_PROJECT.TRIP_INSTANCES 
                SET ROOMS_AVAILABLE = ROOMS_AVAILABLE - 1 
                WHERE ID = (SELECT INSTANCE_ID FROM TRAVEL_PROJECT.ORDERS WHERE ID = :oid)
                AND ROOMS_AVAILABLE > 0";

                        using (var stockCmd = new OracleCommand(updateStockSql, conn))
                        {
                            stockCmd.Parameters.Add("oid", orderId);
                            int rowsAffected = await stockCmd.ExecuteNonQueryAsync();

                            if (rowsAffected == 0)
                            {
                                transaction.Rollback();
                                TempData["ErrorMessage"] = "מצטערים, אזלו המקומות בטיול זה.";
                                return RedirectToAction("Index", "Customer");
                            }
                        }

                        string detailsSql = @"
                SELECT u.EMAIL, t.DESTINATION 
                FROM TRAVEL_PROJECT.ORDERS o
                JOIN TRAVEL_PROJECT.USERS u ON o.USER_ID = u.ID
                JOIN TRAVEL_PROJECT.TRIP_INSTANCES i ON o.INSTANCE_ID = i.ID
                JOIN TRAVEL_PROJECT.TRIPS t ON i.TRIP_MODEL_ID = t.ID
                WHERE o.ID = :oid";

                        string userEmail = "";
                        string destination = "";
                        using (var detailCmd = new OracleCommand(detailsSql, conn))
                        {
                            detailCmd.Parameters.Add("oid", orderId);
                            using (var reader = await detailCmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    userEmail = reader["EMAIL"].ToString();
                                    destination = reader["DESTINATION"].ToString();
                                }
                            }
                        }

                        string updateStatusSql = "UPDATE TRAVEL_PROJECT.ORDERS SET STATUS = 'Confirmed' WHERE ID = :oid";
                        using (var cmd = new OracleCommand(updateStatusSql, conn))
                        {
                            cmd.Parameters.Add("oid", orderId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                        if (!string.IsNullOrEmpty(userEmail))
                        {
                            var configObject = (IConfiguration)HttpContext.RequestServices.GetService(typeof(IConfiguration));

                            await SharedLogic.SendStatusEmail(userEmail, destination, "Paid & Confirmed");
                        }
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        TempData["ErrorMessage"] = "אירעה שגיאה בעיבוד התשלום. נסה שוב מאוחר יותר.";
                        return RedirectToAction("Index", "Customer");
                    }
                }
            }

            TempData["Success"] = "התשלום בוצע בהצלחה! אישור נשלח לאימייל.";
            return RedirectToAction("Index", "Customer");
        }

            public async Task<IActionResult> PersonalArea()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Users");

            List<OrderModel> myOrders = new List<OrderModel>();

            using (OracleConnection conn = new OracleConnection(_configuration))
            {
                await conn.OpenAsync();

                string getWaitingInstancesSql = @"
    SELECT DISTINCT INSTANCE_ID 
    FROM TRAVEL_PROJECT.ORDERS 
    WHERE USER_ID = :p_uid 
    AND TRIM(UPPER(STATUS)) = 'WAITING'"; List<int> waitingInstanceIds = new List<int>();

                using (var waitListCmd = new OracleCommand(getWaitingInstancesSql, conn))
                {
                    waitListCmd.BindByName = true; 
                    waitListCmd.Parameters.Add("p_uid", userId);
                    using (var reader = await waitListCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            waitingInstanceIds.Add(Convert.ToInt32(reader["INSTANCE_ID"]));
                        }
                    }
                }

                foreach (var instId in waitingInstanceIds)
                {
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            await SharedLogic.ProcessWaitingList(instId, conn, transaction);
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Console.WriteLine($"--- ERROR in PersonalArea for Instance {instId}: {ex.Message} ---");
                        }
                    }
                }

                string sql = @"
    SELECT o.ID, o.STATUS, o.ORDER_DATE, i.FILE_PATH,
           i.START_DATE, t.DESTINATION, 
           t.ID AS TRIP_ID 
    FROM TRAVEL_PROJECT.ORDERS o
    JOIN TRAVEL_PROJECT.TRIP_INSTANCES i ON o.INSTANCE_ID = i.ID
    JOIN TRAVEL_PROJECT.TRIPS t ON i.TRIP_MODEL_ID = t.ID
    WHERE o.USER_ID = :p_uid 
    ORDER BY i.START_DATE ASC";

                using (OracleCommand cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("p_uid", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            myOrders.Add(new OrderModel
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                Status = reader["STATUS"].ToString(),
                                OrderDate = Convert.ToDateTime(reader["ORDER_DATE"]),
                                StartDate = Convert.ToDateTime(reader["START_DATE"]),
                                Destination = reader["DESTINATION"].ToString(),
                                FilePath = reader["FILE_PATH"] != DBNull.Value ? reader["FILE_PATH"].ToString() : null,
                                TripId = Convert.ToInt32(reader["TRIP_ID"])
                            });
                        }
                    }
                }
            }
            return View(myOrders);
        }


        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int tripId, int rating, string comment)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Users");

            using (OracleConnection conn = new OracleConnection(_configuration))
            {
                await conn.OpenAsync();

                string sql = @"INSERT INTO TRAVEL_PROJECT.TRIP_REVIEWS 
                       (TRIP_ID, USER_ID, RATING, COMMENT_TEXT, REVIEW_DATE) 
                       VALUES (:p_tripId, :p_userId, :p_rating, :p_comment, CURRENT_TIMESTAMP)";

                using (OracleCommand cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("p_tripId", tripId);
                    cmd.Parameters.Add("p_userId", userId);
                    cmd.Parameters.Add("p_rating", rating);
                    cmd.Parameters.Add("p_comment", comment);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return RedirectToAction("PersonalArea");
        }

        public async Task<IActionResult> Cart()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Users");

            List<OrderModel> orders = new List<OrderModel>();

            using (OracleConnection conn = new OracleConnection(_configuration))
            {
                await conn.OpenAsync();

                string sql = @"
            SELECT o.ID, o.STATUS, o.ORDER_DATE, 
                   i.START_DATE, i.FILE_PATH,
                   t.DESTINATION
            FROM TRAVEL_PROJECT.ORDERS o
            JOIN TRAVEL_PROJECT.TRIP_INSTANCES i ON o.INSTANCE_ID = i.ID
            JOIN TRAVEL_PROJECT.TRIPS t ON i.TRIP_MODEL_ID = t.ID
            WHERE o.USER_ID = :p_uid 
            AND (UPPER(TRIM(o.STATUS)) IN ('PENDING', 'WAITING', 'PENDINGPAYMENT'))
    ORDER BY i.START_DATE ASC";

                using (OracleCommand cmd = new OracleCommand(sql, conn))
                {
                    cmd.BindByName = true;
                    cmd.Parameters.Add("p_uid", userId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orders.Add(new OrderModel
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                Status = reader["STATUS"].ToString(),
                                OrderDate = Convert.ToDateTime(reader["ORDER_DATE"]),
                                StartDate = Convert.ToDateTime(reader["START_DATE"]),
                                Destination = reader["DESTINATION"].ToString(),
                                FilePath = reader["FILE_PATH"] != DBNull.Value ? reader["FILE_PATH"].ToString() : null
                            });
                        }
                    }
                }
            }
            return View("Cart", orders);
        }
    }
}