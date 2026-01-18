using System.Data;
using Oracle.ManagedDataAccess.Client;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration; 

public static class SharedLogic
{
    public static async Task ProcessWaitingList(int instanceId, OracleConnection conn, OracleTransaction transaction)
    {
        await ReleaseExpiredReservations(instanceId, conn, transaction);
        string stockSql = "SELECT ROOMS_AVAILABLE FROM TRAVEL_PROJECT.TRIP_INSTANCES WHERE ID = :p_instId";
        int available = 0;

        using (var stockCmd = new OracleCommand(stockSql, conn))
        {
            stockCmd.Transaction = transaction;
            stockCmd.BindByName = true;
            stockCmd.Parameters.Add("p_instId", instanceId);
            var res = await stockCmd.ExecuteScalarAsync();
            available = res != DBNull.Value ? Convert.ToInt32(res) : 0;
        }

        if (available <= 0) return;

        List<int> ordersToConfirm = new List<int>();
        string waitingSql = @"SELECT ID FROM (
                                SELECT ID FROM TRAVEL_PROJECT.ORDERS 
                                WHERE INSTANCE_ID = :p_instId 
                                AND UPPER(TRIM(STATUS)) = 'WAITING' 
                                ORDER BY ORDER_DATE ASC
                              ) WHERE ROWNUM <= :p_limit";

        using (var waitCmd = new OracleCommand(waitingSql, conn))
        {
            waitCmd.Transaction = transaction;
            waitCmd.BindByName = true;
            waitCmd.Parameters.Add("p_instId", instanceId);
            waitCmd.Parameters.Add("p_limit", available);
            using (var reader = await waitCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    ordersToConfirm.Add(Convert.ToInt32(reader["ID"]));
                }
            }
        }

        foreach (var orderId in ordersToConfirm)
        {
            string userEmail = "";
            string destination = "";

            string userInfoSql = @"SELECT u.EMAIL, t.DESTINATION 
                                   FROM TRAVEL_PROJECT.ORDERS o
                                   JOIN TRAVEL_PROJECT.USERS u ON o.USER_ID = u.ID
                                   JOIN TRAVEL_PROJECT.TRIP_INSTANCES i ON o.INSTANCE_ID = i.ID
                                   JOIN TRAVEL_PROJECT.TRIPS t ON i.TRIP_MODEL_ID = t.ID
                                   WHERE o.ID = :p_oid";

            using (var infoCmd = new OracleCommand(userInfoSql, conn))
            {
                infoCmd.Transaction = transaction;
                infoCmd.Parameters.Add("p_oid", orderId);
                using (var reader = await infoCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        userEmail = reader["EMAIL"].ToString();
                        destination = reader["DESTINATION"].ToString();
                    }
                }
            }

            string confirmSql = @"UPDATE TRAVEL_PROJECT.ORDERS 
                                  SET STATUS = 'PendingPayment', 
                                      PAY_UNTIL = :expiry 
                                  WHERE ID = :p_oid";
            using (var confirmCmd = new OracleCommand(confirmSql, conn))
            {
                confirmCmd.Transaction = transaction;
                confirmCmd.BindByName = true;
                confirmCmd.Parameters.Add("expiry", DateTime.Now.AddHours(12));
                confirmCmd.Parameters.Add("p_oid", orderId);
                await confirmCmd.ExecuteNonQueryAsync();
            }

            string reduceStockSql = "UPDATE TRAVEL_PROJECT.TRIP_INSTANCES SET ROOMS_AVAILABLE = ROOMS_AVAILABLE - 1 WHERE ID = :p_instId";
            using (var reduceCmd = new OracleCommand(reduceStockSql, conn))
            {
                reduceCmd.Transaction = transaction;
                reduceCmd.Parameters.Add("p_instId", instanceId);
                await reduceCmd.ExecuteNonQueryAsync();
            }

            if (!string.IsNullOrEmpty(userEmail))
            {
                try
                {
                    await SendStatusEmail(userEmail, destination, "Confirmed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Mail failed but DB updated for {userEmail}: {ex.Message}");
                }
            }
        }
    }


    private static async Task ReleaseExpiredReservations(int instanceId, OracleConnection conn, OracleTransaction transaction)
    {
        string findExpiredSql = @"SELECT ID FROM TRAVEL_PROJECT.ORDERS 
                                  WHERE INSTANCE_ID = :p_instId 
                                  AND STATUS = 'PendingPayment' 
                                  AND PAY_UNTIL < CURRENT_TIMESTAMP";

        List<int> expiredOrders = new List<int>();

        using (var checkCmd = new OracleCommand(findExpiredSql, conn))
        {
            checkCmd.Transaction = transaction;
            checkCmd.Parameters.Add("p_instId", instanceId);
            using (var reader = await checkCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    expiredOrders.Add(Convert.ToInt32(reader["ID"]));
                }
            }
        }

        foreach (var orderId in expiredOrders)
        {
            string cancelSql = "UPDATE TRAVEL_PROJECT.ORDERS SET STATUS = 'Expired' WHERE ID = :p_oid";
            using (var cmd = new OracleCommand(cancelSql, conn))
            {
                cmd.Transaction = transaction;
                cmd.Parameters.Add("p_oid", orderId);
                await cmd.ExecuteNonQueryAsync();
            }

            string returnStockSql = "UPDATE TRAVEL_PROJECT.TRIP_INSTANCES SET ROOMS_AVAILABLE = ROOMS_AVAILABLE + 1 WHERE ID = :p_instId";
            using (var cmd = new OracleCommand(returnStockSql, conn))
            {
                cmd.Transaction = transaction;
                cmd.Parameters.Add("p_instId", instanceId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public static async Task CheckAndSend5DayReminders(string connectionString)
    {
        var remindersToSend = new List<(int OrderId, string Email, string Destination)>();

        using (var conn = new OracleConnection(connectionString))
        {
            await conn.OpenAsync();
            string sql = @"
            SELECT o.ID, u.EMAIL, t.DESTINATION
            FROM TRAVEL_PROJECT.ORDERS o
            JOIN TRAVEL_PROJECT.USERS u ON o.USER_ID = u.ID
            JOIN TRAVEL_PROJECT.TRIP_INSTANCES i ON o.INSTANCE_ID = i.ID
            JOIN TRAVEL_PROJECT.TRIPS t ON i.TRIP_MODEL_ID = t.ID
            WHERE TRUNC(i.START_DATE) = TRUNC(CURRENT_TIMESTAMP + 5)
            AND (UPPER(TRIM(o.STATUS)) = 'PAID' OR UPPER(TRIM(o.STATUS)) = 'CONFIRMED')
            AND (o.REMINDER_SENT IS NULL OR o.REMINDER_SENT = 0)";

            using (var cmd = new OracleCommand(sql, conn))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        remindersToSend.Add((
                            Convert.ToInt32(reader["ID"]),
                            reader["EMAIL"].ToString(),
                            reader["DESTINATION"].ToString()
                        ));
                    }
                } 
            }

            foreach (var item in remindersToSend)
            {
                try
                {
                    await SendStatusEmail(item.Email, item.Destination, "5DayReminder");

                    string updateSql = "UPDATE TRAVEL_PROJECT.ORDERS SET REMINDER_SENT = 1 WHERE ID = :p_oid";
                    using (var updateCmd = new OracleCommand(updateSql, conn))
                    {
                        updateCmd.Parameters.Add("p_oid", item.OrderId);
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    Console.WriteLine($"Reminder sent to {item.Email} for order {item.OrderId}");

                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending to {item.Email}: {ex.Message}");
                }
            }
        }
    }
    public static async Task SendStatusEmail(string userEmail, string destination, string newStatus)
    {
        string subject = "";
        string body = "";

        if (newStatus == "Confirmed")
        {
            subject = $"חדשות טובות! התפנה מקום ל{destination}";
            body = $"שלום,\n\nהתפנה מקום בטיול ל{destination} והוא נשמר עבורך.\nאנא היכנס לאתר להשלמת התשלום.";
        }
        else if (newStatus == "Paid & Confirmed")
        {
            subject = $"אישור תשלום עבור הטיול ל{destination}";
            body = $"התשלום התקבל וההזמנה מאושרת.";
        }
        else if (newStatus == "5DayReminder")
        {
            subject = $"מתכוננים? הטיול ל{destination} יוצא בעוד 5 ימים";
            body = $"שלום,\n\nרצינו להזכיר לך שהטיול שלך ל{destination} יוצא בעוד 5 ימים";
        }

        try
        {
            using (var smtpClient = new SmtpClient("sandbox.smtp.mailtrap.io"))
            {
                smtpClient.Port = 2525;
                smtpClient.Credentials = new NetworkCredential("53e987879e9be2", "7306560d61d053");
                smtpClient.EnableSsl = true;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("office@travel-agency.com", "Travel Agency"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                mailMessage.To.Add(userEmail);

               // await smtpClient.SendMailAsync(mailMessage);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Email Error: " + ex.Message);
        }
    }
}