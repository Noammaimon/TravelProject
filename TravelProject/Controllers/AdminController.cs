using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using TravelProject.Data; 
using TravelProject.Models;
using Microsoft.EntityFrameworkCore;
namespace TravelProject.Controllers
{
    //Noam Maimon 212994297
    public class AdminController : Controller
    {
        private readonly string _configuration;
        public AdminController(IConfiguration config)
        {
            _configuration = config.GetConnectionString("TravelAgencyDB");
        }

        public async Task<IActionResult> Index(string searchString, string tripType)
        {
            List<TripModel> trips = new List<TripModel>();
            string connectionString = _configuration;

            using (OracleConnection connection = new OracleConnection(connectionString))
            {
                string sqlQuery = @"
            SELECT ID, DESTINATION, COUNTRY, TRIPTYPE, IMAGEPATHS, DESCRIPTION
            FROM TRAVEL_PROJECT.TRIPS 
            WHERE 1=1";

                using (OracleCommand command = new OracleCommand())
                {
                    if (!string.IsNullOrEmpty(searchString))
                    {
                        sqlQuery += " AND (UPPER(DESTINATION) LIKE :search OR UPPER(COUNTRY) LIKE :search)";
                        command.Parameters.Add(":search", $"%{searchString.ToUpper()}%");
                    }

                    command.CommandText = sqlQuery;
                    command.Connection = connection;
                    await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var trip = new TripModel
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                Destination = reader["DESTINATION"].ToString(),
                                Country = reader["COUNTRY"].ToString(),
                                TripType = reader["TRIPTYPE"].ToString(),
                                ImagePaths = reader["IMAGEPATHS"]?.ToString(),
                                Description = reader["DESCRIPTION"]?.ToString(),
                                Instances = new List<TripInstanceModel>() // אתחול הרשימה
                            };

                            
                            string instSql = "SELECT ID, START_DATE FROM TRAVEL_PROJECT.TRIP_INSTANCES WHERE TRIP_MODEL_ID = :tid";

                            using (OracleCommand instCmd = new OracleCommand(instSql, connection))
                            {
                                instCmd.Parameters.Add(":tid", trip.Id);
                                using (var instReader = await instCmd.ExecuteReaderAsync())
                                {
                                    while (await instReader.ReadAsync())
                                    {
                                        trip.Instances.Add(new TripInstanceModel
                                        {
                                            Id = Convert.ToInt32(instReader["ID"]),
                                            StartDate = Convert.ToDateTime(instReader["START_DATE"])
                                        });
                                    }
                                }
                            }

                            trips.Add(trip);
                        }
                    }
                }
            }
            return View(trips);
        }
        public async Task<IActionResult> ManageUsers()
        {
            List<UserModel> users = new List<UserModel>();
            using (OracleConnection conn = new OracleConnection(_configuration))
            {
                await conn.OpenAsync();
                string sql = "SELECT ID, USERNAME, EMAIL, FIRST_NAME, LAST_NAME, IS_ADMIN FROM TRAVEL_PROJECT.USERS ORDER BY ID DESC";

                using (OracleCommand cmd = new OracleCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add(new UserModel
                        {
                            Id = Convert.ToInt32(reader["ID"]),
                            Username = reader["USERNAME"]?.ToString() ?? "",
                            Email = reader["EMAIL"]?.ToString() ?? "",
                            FirstName = reader["FIRST_NAME"]?.ToString() ?? "",
                            LastName = reader["LAST_NAME"]?.ToString() ?? "",
                            IsAdmin = reader["IS_ADMIN"] != DBNull.Value && Convert.ToInt32(reader["IS_ADMIN"]) == 1
                        });
                    }
                }
            }
            return View(users);
        }

        public async Task<IActionResult> UserHistory(int id)
        {
            List<OrderModel> history = new List<OrderModel>();
            using (OracleConnection conn = new OracleConnection(_configuration))
            {
                await conn.OpenAsync();
                string sql = @"
                    SELECT o.ID, t.DESTINATION, i.START_DATE, o.STATUS, o.ORDER_DATE
                    FROM TRAVEL_PROJECT.ORDERS o
                    JOIN TRAVEL_PROJECT.TRIP_INSTANCES i ON o.INSTANCE_ID = i.ID
                    JOIN TRAVEL_PROJECT.TRIPS t ON i.TRIP_MODEL_ID = t.ID
                    WHERE o.USER_ID = :p_uid
                    ORDER BY o.ORDER_DATE DESC";

                using (OracleCommand cmd = new OracleCommand(sql, conn))
                {
                    cmd.Parameters.Add("p_uid", id);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            history.Add(new OrderModel
                            {
                                Id = Convert.ToInt32(reader["ID"]),
                                Destination = reader["DESTINATION"].ToString(),
                                StartDate = Convert.ToDateTime(reader["START_DATE"]),
                                Status = reader["STATUS"].ToString(),
                                OrderDate = Convert.ToDateTime(reader["ORDER_DATE"])
                            });
                        }
                    }
                }
            }
            return View(history);
        }

        
        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration))
                {
                    await conn.OpenAsync();

                    string sqlDeleteOrders = "DELETE FROM TRAVEL_PROJECT.ORDERS WHERE USER_ID = :p_uid";
                    using (OracleCommand cmdOrders = new OracleCommand(sqlDeleteOrders, conn))
                    {
                        cmdOrders.Parameters.Add("p_uid", id);
                        await cmdOrders.ExecuteNonQueryAsync();
                    }

                    string sqlDeleteUser = "DELETE FROM TRAVEL_PROJECT.USERS WHERE ID = :p_uid";
                    using (OracleCommand cmdUser = new OracleCommand(sqlDeleteUser, conn))
                    {
                        cmdUser.Parameters.Add("p_uid", id);
                        int rowsAffected = await cmdUser.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            TempData["Success"] = "המשתמש וכל היסטוריית ההזמנות שלו נמחקו לצמיתות.";
                        }
                        else
                        {
                            TempData["Error"] = "המשתמש לא נמצא.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "שגיאה בתהליך המחיקה: " + ex.Message;
            }

            return RedirectToAction("ManageUsers");
        }

        public IActionResult CreateUser()
        {
            return View(new UserModel());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(UserModel user, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("Password", "חובה להזין סיסמה");
                return View(user);
            }

            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration))
                {
                    await conn.OpenAsync();

                    
                    string checkSql = "SELECT USERNAME, PASSWORD FROM TRAVEL_PROJECT.USERS WHERE USERNAME = :uname OR PASSWORD = :pass";

                    using (OracleCommand checkCmd = new OracleCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.Add("uname", user.Username);
                        checkCmd.Parameters.Add("pass", password);

                        using (var reader = await checkCmd.ExecuteReaderAsync())
                        {
                            bool isDuplicate = false;
                            while (await reader.ReadAsync())
                            {
                                string dbUser = reader["USERNAME"].ToString();
                                string dbPass = reader["PASSWORD"].ToString();

                                if (dbPass == password)
                                {
                                    ModelState.AddModelError("Password", "סיסמה זו בשימוש, נא לבחור סיסמה אחרת");
                                    isDuplicate = true;
                                }
                                if (dbUser == user.Username)
                                {
                                    ModelState.AddModelError("Username", "שם משתמש זה בשימוש, נא לבחור שם משתמש אחר");
                                    isDuplicate = true;
                                }
                            }

                            if (isDuplicate)
                            {
                                return View(user); 
                            }
                        }
                    }
        
                    string sql = @"
                INSERT INTO TRAVEL_PROJECT.USERS 
                (ID, USERNAME, PASSWORD, EMAIL, FIRST_NAME, LAST_NAME, IS_ADMIN) 
                VALUES (TRAVEL_PROJECT.USERS_SEQ.NEXTVAL, :uname, :pass, :email, :fname, :lname, :isadmin)";

                    using (OracleCommand cmd = new OracleCommand(sql, conn))
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("uname", user.Username);
                        cmd.Parameters.Add("pass", password);
                        cmd.Parameters.Add("email", user.Email);
                        cmd.Parameters.Add("fname", user.FirstName ?? "");
                        cmd.Parameters.Add("lname", user.LastName ?? "");
                        cmd.Parameters.Add("isadmin", user.IsAdmin ? 1 : 0);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["Success"] = "המשתמש נוצר בהצלחה!";
                return RedirectToAction("ManageUsers");
            }
            catch (OracleException ex)
            {
                TempData["Error"] = "שגיאת מסד נתונים (" + ex.Number + "): " + ex.Message;
                return View(user);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "שגיאה כללית: " + ex.Message;
                return View(user);
            }
        }
    }

}
    

