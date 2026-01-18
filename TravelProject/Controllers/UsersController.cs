using Microsoft.AspNetCore.Mvc;
using System;
using TravelProject.Models;
using TravelProject.Data;
//using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using System.Transactions;

namespace TravelProject.Controllers
{
    public class UsersController : Controller
    {
        private readonly string _configuration;
        public UsersController(IConfiguration config)
        {
            _configuration = config.GetConnectionString("TravelAgencyDB");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(UserModel user)
        {
            if (ModelState.IsValid)
            {
                string connectionString = _configuration;

                using (OracleConnection connection = new OracleConnection(connectionString))
                {
                    connection.Open();

                    string sqlQuery = "INSERT INTO TRAVEL_PROJECT.USERS (ID, USERNAME, PASSWORD, EMAIL, FIRST_NAME, LAST_NAME, IS_ADMIN) " +
                  "VALUES (USERS_SEQ.NEXTVAL, :val1, :val2, :val3, :val4, :val5, 0)";

                    using (OracleCommand command = new OracleCommand(sqlQuery, connection))
                    {
                        command.Parameters.Add(":val1", user.Username);
                        command.Parameters.Add(":val2", user.Password);
                        command.Parameters.Add(":val3", user.Email);
                        command.Parameters.Add(":val4", user.FirstName);
                        command.Parameters.Add(":val5", user.LastName);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "ההרשמה בוצעה בהצלחה!";
                            return RedirectToAction("Login");
                        }
                        else
                        {
                            ViewBag.Error = "ההרשמה נכשלה. נסה שוב.";
                        }
                    }
                }
            }
            return View(user);
        }


        public IActionResult Login()
        {
            return View();
        }

        
        [HttpPost]
        
        public IActionResult Login(string username, string password)
        {

            string connectionString = _configuration;
            using (OracleConnection connection = new OracleConnection(connectionString))
            {
                connection.Open();
                string sqlQuery = "SELECT ID ,USERNAME, IS_ADMIN, FIRST_NAME FROM TRAVEL_PROJECT.USERS " + "WHERE USERNAME = :val1 AND PASSWORD = :val2";

                using (OracleCommand command = new OracleCommand(sqlQuery, connection))
                {
                    command.Parameters.Add(":val1", username);
                    command.Parameters.Add(":val2", password);

                    using (OracleDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read()) 
                        {
                            int userId = Convert.ToInt32(reader["ID"]); 
                            string Username = reader["USERNAME"].ToString();

                            int isAdminValue = Convert.ToInt32(reader["IS_ADMIN"]);
                            string firstName = reader["FIRST_NAME"].ToString();

                            HttpContext.Session.SetInt32("UserId", userId);
                            HttpContext.Session.SetString("Username", Username);
                            HttpContext.Session.SetInt32("IsAdmin", isAdminValue);


                            return isAdminValue == 1 ?
                                   RedirectToAction("Index", "Admin") :
                                   RedirectToAction("Index", "Customer");
                        }
                        else
                        {
                            ViewBag.Error = "שם משתמש או סיסמה שגויים";
                        }
                    }
                }
            } 

            return View();
        

        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}