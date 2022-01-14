using System;
using System.Text;
using MySql.Data.MySqlClient;
using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using ViTrader.Model;
using WebClient;
using System.Net;
using static ViTraderServer.Server;

namespace ViTrader.Database
{
    static class Database
    {
        static readonly string connectionString;

        static Database()
        {
            string fileName = "config.json";
            string jsonString = File.ReadAllText(fileName);

            using (JsonDocument doc = JsonDocument.Parse(jsonString))
            {
                JsonElement root = doc.RootElement;

                connectionString = root.GetProperty("ConnectionString").GetString();
            }
        }

        public static MySqlConnection Connect() 
        {
            var conn = new MySqlConnection(connectionString);
            conn.Open();
            return conn;
        }

        public static void Close(MySqlConnection conn)
        {
            conn.Close();
            conn.Dispose();
        } 

        public static bool Login(string userName, string password, out int id)
        {
            var conn = Connect();

            var query = "SELECT id FROM user WHERE userName = @name AND password = @password";

            int? result;
            using (var hasher = SHA256.Create())
            using (var cmd = new MySqlCommand(query, conn))
            {
                string hPassword = Encoding.UTF8.GetString
                    (hasher.ComputeHash(Encoding.UTF8.GetBytes(password)));

                cmd.Parameters.AddWithValue("@name", userName);
                cmd.Parameters.AddWithValue("@password", hPassword);
                cmd.Prepare();
                result = (int?)cmd.ExecuteScalar();
            }

            if (!result.HasValue)
            {
                Close(conn);
                id = (int)Errors.WRONG_INFO;
                return false;
            }

            query = "SELECT isValidated FROM user WHERE id = @userId";

            id = result.Value;
            sbyte validationResult;

            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@userId", id);
                cmd.Prepare();
                validationResult = (sbyte)cmd.ExecuteScalar();
            }

            Close(conn);
            if (validationResult == 0)
            {
                id = (int)Errors.INACTIVE_ACCOUNT;
                return false;
            }

            return true;
        }

        public static bool Register(string userName, string password, string emailAddress, out int id)
        {
            var conn = Connect();

            var query = "SELECT id FROM user WHERE userName = @name";

            int? result;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@name", userName);
                cmd.Prepare();
                result = (int?)cmd.ExecuteScalar();
            }

            if (result.HasValue)
            {
                Close(conn);
                id = (int)Errors.USERNAME_EXISTS;
                return false;
            }

            query = "SELECT id FROM user WHERE emailAddress = @email";

            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@email", emailAddress);
                cmd.Prepare();
                result = (int?)cmd.ExecuteScalar();
            }

            if (result.HasValue)
            {
                Close(conn);
                id = (int)Errors.EMAIL_EXISTS;
                return false;
            }

            query =
                "INSERT INTO user(userName, password, emailAddress, isValidated, dateCreated, isAdmin)" +
                "VALUES(@name, @password, @email, @validation, @date, @admin)";

            using (var hasher = SHA256.Create())
            using (var cmd = new MySqlCommand(query, conn))
            {
                string hPassword = Encoding.UTF8.GetString
                    (hasher.ComputeHash(Encoding.UTF8.GetBytes(password)));

                cmd.Parameters.AddWithValue("@name", userName);
                cmd.Parameters.AddWithValue("@password", hPassword);
                cmd.Parameters.AddWithValue("@email", emailAddress);
                cmd.Parameters.AddWithValue("@validation", 0);
                cmd.Parameters.AddWithValue("@date",
                    DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@admin", 0);
                cmd.Prepare();
                result = cmd.ExecuteNonQuery();
            }

            Close(conn);
            if (result != 1)
            {
                id = (int)Errors.UNKNOWN;
                return false;
            }

            id = result.Value;
            return true;
        }

        public static bool ValidateUser(int userId)
        {
            var conn = Connect();

            var query =
                "UPDATE user SET isValidated = 1 WHERE id = @id";

            int rowsAffected;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", userId);
                cmd.Prepare();
                rowsAffected = cmd.ExecuteNonQuery();
            }

            Close(conn);
            if (rowsAffected == 1)
                return true;

            return false;
        }

        public static int GetID(string userName, string password)
        {
            var conn = Connect();

            var query =
                "SELECT id FROM user WHERE userName = @name AND password = @password";

            int? result;
            using (var hasher = SHA256.Create())
            using (var cmd = new MySqlCommand(query, conn))
            {
                string hPassword = Encoding.UTF8.GetString
                    (hasher.ComputeHash(Encoding.UTF8.GetBytes(password)));

                cmd.Parameters.AddWithValue("@name", userName);
                cmd.Parameters.AddWithValue("@password", hPassword);
                cmd.Prepare();
                result = (int?)cmd.ExecuteScalar();
            }

            Close(conn);
            if (result.HasValue)
                return result.Value;

            return -1;
        }

        public static bool IsValidUser(string userName, string password)
        {
            return GetID(userName, password) != -1;
        }

        public static List<Crypto> GetCryptos()
        {
            List<Crypto> cryptos = new List<Crypto>();

            var conn = Connect();

            var query =
                "SELECT ticker, name FROM crypto";

            MySqlDataReader reader;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Prepare();
                using (reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string ticker = (string)reader["ticker"];
                        string name = Encoding.UTF8.GetString((byte[])reader["name"]);

                        cryptos.Add(new Crypto(ticker, name));
                    }
                }
            }

            Close(conn);
            return cryptos;
        }

        public static List<Position> GetPositions(string userName)
        {
            List<Position> positions = new();

            var conn = Connect();

            var query =
                "SELECT c.name AS name, p.amount AS amount " +
                "FROM position p " +
                "INNER JOIN crypto c ON p.cryptoId = c.id " +
                "INNER JOIN user u ON p.userId = u.id " +
                "WHERE u.userName = @name";

            MySqlDataReader reader;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@name", userName);
                cmd.Prepare();
                using (reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = Encoding.UTF8.GetString((byte[])reader["name"]);
                        decimal amount = (decimal)reader["amount"];

                        positions.Add(new Position(name, amount));
                    }
                }
            }

            Close(conn);
            return positions;
        }

        public static List<Trade> GetTrades(string userName)
        {
            List<Trade> trades = new List<Trade>();

            var conn = Connect();

            var query =
                "SELECT cb.ticker AS tickerBought, cs.ticker AS tickerSold," +
                " tradeTime, amountBought, amountSold " +
                "FROM trade t " +
                "INNER JOIN crypto cb ON t.cryptoBoughtId = cb.id " +
                "INNER JOIN crypto cs ON t.cryptoSoldId = cs.id " +
                "INNER JOIN user u ON t.userId = u.id " +
                "WHERE u.userName = @name";

            MySqlDataReader reader;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@name", userName);
                cmd.Prepare();
                using (reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tickerBought = (string)reader["tickerBought"];
                        string tickerSold = (string)reader["tickerSold"];
                        DateTime dateTime = (DateTime)reader["tradeTime"];
                        decimal amountBought = (decimal)reader["amountBought"];
                        decimal amountSold = (decimal)reader["amountSold"];

                        trades.Add(new Trade(tickerBought, tickerSold,
                            amountBought, amountSold, dateTime));
                    }
                }
            }

            Close(conn);
            return trades;
        }

        public static bool IsAdmin(string userName)
        {
            var conn = Connect();

            var query =
                "SELECT isAdmin FROM user WHERE userName = @name";

            sbyte? result;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@name", userName);
                cmd.Prepare();
                result = (sbyte?)cmd.ExecuteScalar();
            }

            Close(conn);
            if (result.HasValue)
                return result.Value == 1;

            return false;
        }

        public static bool AddCrypto(string ticker, string name)
        {
            var conn = Connect();

            var query =
                "SELECT id " +
                "FROM crypto " +
                "WHERE UPPER(ticker) = UPPER(@ticker) OR name = BINARY(@name)";

            int? result;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@ticker", ticker);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Prepare();
                result = (int?)cmd.ExecuteScalar();
            }

            if (result.HasValue)
            {
                Close(conn);
                return false;
            }

            query =
                "INSERT INTO crypto(ticker, name) VALUES(@ticker, BINARY(@name))";

            int rowsAffected;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@ticker", ticker);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Prepare();
                rowsAffected = cmd.ExecuteNonQuery();
            }

            Close(conn);
            if (rowsAffected != 1)
                return false;

            return true;
        }

        public static bool DeleteCrypto(string ticker, string name)
        {
            var conn = Connect();

            var query =
                "SELECT id " +
                "FROM crypto " +
                "WHERE UPPER(ticker) = UPPER(@ticker) AND name = BINARY(@name)";

            int? result;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@ticker", ticker);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Prepare();
                result = (int?)cmd.ExecuteScalar();
            }

            if (!result.HasValue)
            {
                Close(conn);
                return false;
            }

            query =
                "DELETE FROM trade WHERE cryptoBoughtId = @id OR cryptoSoldId = @id";

            int rowsAffected;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", result.Value);
                cmd.Prepare();
                rowsAffected = cmd.ExecuteNonQuery();
            }

            query =
                "DELETE FROM position WHERE cryptoId = @id";

            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", result.Value);
                cmd.Prepare();
                rowsAffected = cmd.ExecuteNonQuery();
            }

            query =
                "DELETE FROM crypto WHERE id = @id";

            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", result.Value);
                cmd.Prepare();
                rowsAffected = cmd.ExecuteNonQuery();
            }

            Close(conn);
            if (rowsAffected != 1)
                return false;

            return true;
        }

        private static bool AddTrade(int userId, int cryptoBuyId, int cryptoSellId,
            decimal amountToBuy, decimal amountToSell, MySqlConnection conn)
        {
            var query =
                "INSERT INTO trade(userId, cryptoBoughtId, cryptoSoldId, tradeTime," +
                " amountBought, amountSold) " +
                "VALUES(@userId, @boughtId, @soldId, @tradeTime, @bought, @sold)";

            int rowsAffected;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@boughtId", cryptoBuyId);
                cmd.Parameters.AddWithValue("@soldId", cryptoSellId);
                cmd.Parameters.AddWithValue("@tradeTime",
                    DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@bought", amountToBuy);
                cmd.Parameters.AddWithValue("@sold", amountToSell);
                cmd.Prepare();
                rowsAffected = cmd.ExecuteNonQuery();
            }

            if (rowsAffected != 1)
                return false;

            return true;
        }

        public static int GetIDByUserName(string userName)
        {
            var conn = Connect();

            var query =
                "SELECT id FROM user WHERE userName = @name";

            int? result;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@name", userName);
                cmd.Prepare();
                result = (int?)cmd.ExecuteScalar();
            }

            Close(conn);
            if (result.HasValue)
                return result.Value;

            return -1;
        }

        public static int GetCryptoIDByName(string cryptoName)
        {
            var conn = Connect();

            var query =
                "SELECT id FROM crypto WHERE name = BINARY(@name)";

            int? result;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@name", cryptoName);
                cmd.Prepare();
                result = (int?)cmd.ExecuteScalar();
            }

            Close(conn);
            if (result.HasValue)
                return result.Value;

            return -1;
        }

        public static decimal GetUserCryptoAmount(int userId, string cryptoName)
        {
            var conn = Connect();

            var query =
                "SELECT amount " +
                "FROM position p " +
                "INNER JOIN crypto c ON p.cryptoId = c.id " +
                "INNER JOIN user u on p.userId = u.id " +
                "WHERE c.name = BINARY(@name) AND u.id = @uid";

            decimal? amount;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@name", cryptoName);
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.Prepare();
                amount = (decimal?)cmd.ExecuteScalar();
            }

            Close(conn);
            if (amount.HasValue)
                return amount.Value;

            return -1;
        }

        public static bool CreateTrade(string userName, string action, string cryptoName, string amount,
            out string message, out HttpStatusCode code)
        {
            decimal buyOrSellAmount = decimal.Parse(amount);

            if (!action.Equals("buy") && !action.Equals("sell") || buyOrSellAmount <= 0)
            {
                message = "Invalid action";
                code = HttpStatusCode.BadRequest;
                return false;
            }

            string uri = "https://api.coingecko.com/api/v3/simple/price?ids=" +
                    cryptoName + "&vs_currencies=USD";

            RestClient client = new RestClient(HTTP_VERB.GET, uri);

            string response = client.MakeRequest();
            decimal cryptoPrice;
            using (JsonDocument doc = JsonDocument.Parse(response))
            {
                JsonElement root = doc.RootElement;
                JsonElement coin = root.GetProperty(cryptoName);
                cryptoPrice = coin.GetProperty("usd").GetDecimal();
            }

            int userId = GetIDByUserName(userName);
            int cryptoId = GetCryptoIDByName(cryptoName);

            if (userId == -1 || cryptoId == -1)
            {
                message = "User or Crypto doesn't exist";
                code = HttpStatusCode.NotFound;
                return false;
            }

            int usdtID = GetCryptoIDByName("tether");
            decimal usdtToSpend = buyOrSellAmount * cryptoPrice;

            if (action.Equals("buy"))
            {
                decimal userUSDT = GetUserCryptoAmount(userId, "tether");

                if (usdtToSpend > userUSDT)
                {
                    message = "Not enough USDT to purchase";
                    code = HttpStatusCode.BadRequest;
                    return false;
                }

                var conn = Connect();
                bool tradeAdded = AddTrade(userId, cryptoId, usdtID, buyOrSellAmount, usdtToSpend, conn);

                if (!tradeAdded)
                {
                    Close(conn);
                    message = "Adding trade failed";
                    code = HttpStatusCode.InternalServerError;
                    return false;
                }

                bool positionAdded = CreateOrAddToPosition(userId, cryptoId, buyOrSellAmount, conn);

                if (!positionAdded)
                {
                    Close(conn);
                    message = "Adding to position failed";
                    code = HttpStatusCode.InternalServerError;
                    return false;
                }

                bool usdtRemoved = DeleteOrReducePosition(userId, usdtID, usdtToSpend, conn);

                Close(conn);

                if (!usdtRemoved)
                {
                    message = "Deleting from position failed";
                    code = HttpStatusCode.InternalServerError;
                }
                else
                {
                    message = "Trade added";
                    code = HttpStatusCode.Created;
                }

                return usdtRemoved;
            }
            else
            {
                decimal cryptoAmount = GetUserCryptoAmount(userId, cryptoName);

                if (buyOrSellAmount > cryptoAmount)
                {
                    message = "Not enough " + cryptoName + " to sell";
                    code = HttpStatusCode.BadRequest;
                    return false;
                }

                var conn = Connect();
                bool tradeAdded = AddTrade(userId, usdtID, cryptoId, usdtToSpend, buyOrSellAmount, conn);

                if (!tradeAdded)
                {
                    Close(conn);
                    message = "Adding trade failed";
                    code = HttpStatusCode.InternalServerError;
                    return false;
                }

                bool positionRemoved = DeleteOrReducePosition(userId, cryptoId, buyOrSellAmount, conn);

                if (!positionRemoved)
                {
                    Close(conn);
                    message = "Deleting from position failed";
                    code = HttpStatusCode.InternalServerError;
                    return false;
                }

                bool usdtAdded = CreateOrAddToPosition(userId, usdtID, usdtToSpend, conn);

                Close(conn);

                if (!usdtAdded)
                {
                    message = "Adding to position failed";
                    code = HttpStatusCode.InternalServerError;
                }
                else
                {
                    message = "Trade added";
                    code = HttpStatusCode.Created;
                }

                return usdtAdded;
            }
        }

        public static bool AddUSDT(string userName, string amount)
        {
            int userId = GetIDByUserName(userName);
            int usdtId = GetCryptoIDByName("tether");

            decimal amountDec = decimal.Parse(amount);

            if (amountDec > 10000 || amountDec < 100)
                return false;

            var conn = Connect();
            bool addedUSDT = CreateOrAddToPosition(userId, usdtId, amountDec, conn);
            Close(conn);

            return addedUSDT;
        }

        private static bool CreateOrAddToPosition(int userId, int cryptoId, decimal amount, MySqlConnection conn)
        {
            var query =
                    "SELECT id " +
                    "FROM position p " +
                    "WHERE userId = @userId AND cryptoId = @cryptoId";

            int? result;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@cryptoId", cryptoId);
                cmd.Prepare();
                result = (int?)cmd.ExecuteScalar();
            }

            int rowsAffected;
            if (result.HasValue)
            {
                query =
                    "UPDATE position SET amount = amount + @add WHERE id = @id";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@add", amount);
                    cmd.Parameters.AddWithValue("@id", result.Value);
                    cmd.Prepare();
                    rowsAffected = cmd.ExecuteNonQuery();
                }

                if (rowsAffected != 1)
                    return false;
            }
            else
            {
                query =
                    "INSERT INTO `position`(userId, cryptoId, amount) " +
                    "VALUES(@userId, @cryptoId, @amount)";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@cryptoId", cryptoId);
                    cmd.Parameters.AddWithValue("@amount", amount);
                    cmd.Prepare();
                    rowsAffected = cmd.ExecuteNonQuery();
                }

                if (rowsAffected != 1)
                    return false;
            }

            return true;
        }

        private static bool DeleteOrReducePosition(int userId, int cryptoId, decimal amountSold, MySqlConnection conn)
        {
            var query =
                    "SELECT id, amount " +
                    "FROM position p " +
                    "WHERE userId = @userId AND cryptoId = @cryptoId";

            MySqlDataReader reader;
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@cryptoId", cryptoId);
            cmd.Prepare();

            int positionId;
            decimal amount;
            using (reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    positionId = (int)reader["id"];
                    amount = (decimal)reader["amount"];
                }
                else
                    return false;
            }

            int rowsAffected;
            if (amount - amountSold <= 0)
            {
                query =
                    "DELETE FROM position WHERE id = @id";

                using (var cmd2 = new MySqlCommand(query, conn))
                {
                    cmd2.Parameters.AddWithValue("@id", positionId);
                    cmd2.Prepare();
                    rowsAffected = cmd2.ExecuteNonQuery();
                }

                if (rowsAffected != 1)
                    return false;
            }
            else
            {
                query =
                    "UPDATE position " +
                    "SET amount = amount - @amount " +
                    "WHERE id = @id";

                using (var cmd2 = new MySqlCommand(query, conn))
                {
                    cmd2.Parameters.AddWithValue("@amount", amountSold);
                    cmd2.Parameters.AddWithValue("@id", positionId);
                    cmd2.Prepare();
                    rowsAffected = cmd2.ExecuteNonQuery();
                }

                if (rowsAffected != 1)
                    return false;
            }

            return true;
        }

        public static User GetUser(string userName)
        {
            User user = new User();

            var conn = Connect();

            var query =
                "SELECT userName, emailAddress, dateCreated, isAdmin FROM user WHERE userName = @name";

            MySqlDataReader reader;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@name", userName);
                cmd.Prepare();
                using (reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        user.UserName = (string)reader["userName"];
                        user.EmailAddress = (string)reader["emailAddress"];
                        user.TimeCreated = (DateTime)reader["dateCreated"];
                        user.isAdmin = (sbyte)reader["isAdmin"];
                    }
                }
            }

            Close(conn);
            return user;
        }

        public static bool UpdateUser(string userName, string newName)
        {
            var conn = Connect();

            var query =
                "UPDATE user SET userName = @newName WHERE userName = @oldName";

            int rowsAffected;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@oldName", userName);
                cmd.Parameters.AddWithValue("@newName", newName);
                cmd.Prepare();
                rowsAffected = cmd.ExecuteNonQuery();
            }

            Close(conn);
            if (rowsAffected != 1)
                return false;

            return true;
        }

        public static bool DeleteUser(string userName)
        {
            int id = GetIDByUserName(userName);

            var conn = Connect();

            var query = "DELETE FROM position WHERE userId = @id";

            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Prepare();
                cmd.ExecuteNonQuery();
            }

            query = "DELETE FROM trade WHERE userId = @id";

            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Prepare();
                cmd.ExecuteNonQuery();
            }

            query = "DELETE FROM user WHERE id = @id";

            int rowsAffected;
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Prepare();
                rowsAffected = cmd.ExecuteNonQuery();
            }

            Close(conn);
            if (rowsAffected != 1)
                return false;

            return true;
        }
    }
}