using ServerViTrader;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml.Serialization;
using ViTrader.Database;
using ViTrader.Model;

namespace ViTraderServer
{
    class Server
    {
        HttpListener httpListener;
        TcpListener tcpListener;
        string httpEndPoint, tcpAddress;
        int tcpPort;
        string emailAddress, password;

        public Server()
        {
            using var reg = new ServerRegistry();

            try
            {
                LoadServerSetting(reg);
            }
            catch (Exception)
            {
                Console.WriteLine("Server settings missing.");

                EditServerSettings();
                LoadServerSetting(reg);
            }
        }

        private void LoadServerSetting(ServerRegistry reg)
        {
            httpEndPoint
                = "http://" + reg.GetHTTPServerAddress() + ":" + reg.GetHTTPServerPort() + "/";

            tcpAddress = reg.GetTCPServerAddress();
            tcpPort = reg.GetTCPServerPort();
            emailAddress = reg.GetServerEmail();
            password = reg.GetServerEmailPassword();
        }

        public void LaunchServer()
        {
            Thread httpThread = new Thread(LaunchHTTPServer);
            Thread tcpThread = new Thread(LaunchTCPServer);

            tcpThread.Start();
            httpThread.Start();

            Console.WriteLine("Server running. \nPress enter to exit.");
            Console.ReadLine();

            TerminateServer(httpThread, tcpThread);
        }

        private void TerminateServer(Thread httpThread, Thread tcpThread)
        {
            httpListener.Stop();
            tcpListener.Stop();

            httpThread.Join();
            tcpThread.Join();
        }

        public void EditServerSettings()
        {
            Console.Write("Enter TCP server address: ");
            string tcpServerAddress = Console.ReadLine();

            Console.Write("Enter TCP server port: ");
            string tcpServerPort = Console.ReadLine();

            Console.Write("Enter HTTP server address: ");
            string httpServerAddress = Console.ReadLine();

            Console.Write("Enter HTTP server port: ");
            string httpServerPort = Console.ReadLine();

            Console.Write("Enter server e-mail address: ");
            string serverAddress = Console.ReadLine();

            Console.Write("Enter server e-mail address password: ");
            string addressPassword = Console.ReadLine();

            using var reg = new ServerRegistry();

            reg.SetHTTPServerAddress(httpServerAddress);
            reg.SetTCPServerAddress(tcpServerAddress);
            reg.SetHTTPServerPort(int.Parse(httpServerPort));
            reg.SetTCPServerPort(int.Parse(tcpServerPort));
            reg.SetServerEmailAddressAndPassword(serverAddress, addressPassword);
        }

        private void LaunchTCPServer()
        {
            tcpListener = new TcpListener(IPAddress.Parse(tcpAddress), tcpPort);
            tcpListener.Start();

            try
            {
                while (true)
                {
                    TcpClient client = tcpListener.AcceptTcpClient();

                    Thread processTCPThread = new Thread(() => ProcessTCPRequest(client));
                    processTCPThread.Start();
                }
            }
            catch (SocketException)
            {
                Console.WriteLine("Closed TCP Server");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Terminated TCP Server.\n" + ex.Message);
            }
        }

        private void LaunchHTTPServer()
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add(httpEndPoint);
            httpListener.Start();

            try
            {
                while (true)
                {
                    HttpListenerContext context = httpListener.GetContext();

                    Thread processHttpThread = new Thread(() => ProcessHTTPRequest(context));
                    processHttpThread.Start();
                }
            }
            catch (HttpListenerException)
            {
                Console.WriteLine("Closed HTTP Server");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Terminated HTTP Server.\n" + ex.Message);
            }
        }

        #region HTTP_Server

        private void ProcessHTTPRequest(HttpListenerContext context)
        {
            try
            {
                if (context.Request.RawUrl == "/favicon.ico")
                    return;

                string[] request = context.Request.RawUrl.Split("/")
                    .Where(s => !String.IsNullOrWhiteSpace(s)).ToArray();

                if (request.Length == 0)
                {
                    WriteHTTPResponse(context, "Error: Bad request",
                            HttpStatusCode.BadRequest);

                    return;
                }

                switch (context.Request.HttpMethod)
                {
                    case "GET":
                        HandleGETRequest(context, request);
                        break;
                    case "PUT":
                        HandlePUTRequest(context, request);
                        break;
                    case "POST":
                        HandlePOSTRequest(context, request);
                        break;
                    case "DELETE":
                        HandleDELETERequest(context, request);
                        break;
                    default:
                        WriteHTTPResponse(context, "Unsupported HTTP method", HttpStatusCode.BadRequest);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception caught: {ex.Message} \n {ex.StackTrace}");

                WriteHTTPResponse(context, "Error: Bad request",
                            HttpStatusCode.BadRequest);
            }
        }

        void HandleGETRequest(HttpListenerContext context, string[] request)
        {
            string first = request[0];

            switch (first)
            {
                case "validate":
                    HandleValidation(context, request);
                    break;
                case "cryptos":
                    GetCryptos(context, request);
                    break;
                case "positions":
                    GetPositions(context, request);
                    break;
                case "trades":
                    GetTrades(context, request);
                    break;
                case "users":
                    GetUser(context, request);
                    break;
                default:
                    WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
                    return;
            }
        }

        void HandlePUTRequest(HttpListenerContext context, string[] request)
        {
            string first = request[0];

            switch (first)
            {
                case "cryptos":
                    CreateCrypto(context, request);
                    break;
                case "trades":
                    CreateTrade(context, request);
                    break;
                case "positions":
                    AddUSDT(context, request);
                    break;
                default:
                    WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
                    return;
            }
        }

        void HandlePOSTRequest(HttpListenerContext context, string[] request)
        {
            string first = request[0];

            switch (first)
            {
                case "users":
                    UpdateUser(context, request);
                    break;
                default:
                    WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
                    return;
            }
        }

        void HandleDELETERequest(HttpListenerContext context, string[] request)
        {
            string first = request[0];

            switch (first)
            {
                case "cryptos":
                    DeleteCrypto(context, request);
                    break;
                case "users":
                    DeleteUser(context, request);
                    break;
                default:
                    WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
                    return;
            }
        }

        void HandleValidation(HttpListenerContext context, string[] request)
        {
            string[] credentials = request[1].Split("-");

            int userId = Database.GetID(credentials[0], credentials[1]);

            if (userId != -1)
            {
                if (Database.ValidateUser(userId))
                {
                    WriteHTTPResponse(context, "Successfully validated",
                        HttpStatusCode.OK);
                }
                else
                {

                    WriteHTTPResponse(context, "Error: Internal server error",
                        HttpStatusCode.InternalServerError);
                }
            }
            else
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
        }

        void GetCryptos(HttpListenerContext context, string[] request)
        {
            if (request.Length != 1)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                List<Crypto> cryptos = Database.GetCryptos();

                XmlSerializer serializer = new XmlSerializer(typeof(List<Crypto>));

                using (StringWriter writer = new StringWriter())
                {
                    serializer.Serialize(writer, cryptos);
                    string serialized = writer.ToString();

                    WriteHTTPResponse(context, serialized, HttpStatusCode.OK);
                }
            }
        }

        void GetPositions(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                string userName = request[1];

                if (!IsAuthorized(context, userName))
                {
                    WriteHTTPResponse(context, "Error: Unauthorized",
                        HttpStatusCode.Unauthorized);
                    return;
                }

                List<Position> positions = Database.GetPositions(userName);

                XmlSerializer serializer = new XmlSerializer(typeof(List<Position>));

                using (StringWriter writer = new StringWriter())
                {
                    serializer.Serialize(writer, positions);
                    string serialized = writer.ToString();

                    WriteHTTPResponse(context, serialized, HttpStatusCode.OK);
                }
            }
        }

        void GetTrades(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                string userName = request[1];

                if (!IsAuthorized(context, userName))
                {
                    WriteHTTPResponse(context, "Error: Unauthorized",
                        HttpStatusCode.Unauthorized);
                    return;
                }

                List<Trade> trades = Database.GetTrades(userName);

                XmlSerializer serializer = new XmlSerializer(typeof(List<Trade>));

                using (StringWriter writer = new StringWriter())
                {
                    serializer.Serialize(writer, trades);
                    string serialized = writer.ToString();

                    WriteHTTPResponse(context, serialized, HttpStatusCode.OK);
                }
            }
        }

        bool IsAuthorized(HttpListenerContext context, string requestUserName)
        {
            string headerPassword = context.Request.Headers.Get(requestUserName);

            if (headerPassword == null)
                return false;

            if (Database.IsValidUser(requestUserName, headerPassword))
                return true;

            return false;
        }

        void CreateCrypto(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                // cryptos/fgrgic?ticker=eth&name=ethereum

                request = request[1].Split("?")
                    .Where(s => !String.IsNullOrWhiteSpace(s)).ToArray();

                if (request.Length != 2)
                {
                    WriteHTTPResponse(context, "Error: Bad request",
                            HttpStatusCode.BadRequest);
                    return;
                }

                NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

                if (!IsAuthorized(context, request[0]) || !Database.IsAdmin(request[0]))
                {
                    WriteHTTPResponse(context, "Error: Unauthorized",
                        HttpStatusCode.Unauthorized);
                    return;
                }

                if (!Database.AddCrypto(collection.Get("ticker"), collection.Get("name")))
                {
                    WriteHTTPResponse(context, "Error: Internal server error",
                        HttpStatusCode.InternalServerError);
                }
                else
                {
                    WriteHTTPResponse(context, "Crypto added",
                        HttpStatusCode.Created);
                }
            }
        }

        void DeleteCrypto(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                // cryptos/fgrgic?ticker=eth&name=ethereum

                request = request[1].Split("?")
                    .Where(s => !String.IsNullOrWhiteSpace(s)).ToArray();

                if (request.Length != 2)
                {
                    WriteHTTPResponse(context, "Error: Bad request",
                            HttpStatusCode.BadRequest);
                    return;
                }

                NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

                if (!IsAuthorized(context, request[0]) || !Database.IsAdmin(request[0]))
                {
                    WriteHTTPResponse(context, "Error: Unauthorized",
                        HttpStatusCode.Unauthorized);
                    return;
                }

                if (!Database.DeleteCrypto(collection.Get("ticker"), collection.Get("name")))
                {
                    WriteHTTPResponse(context, "Error: Internal server error",
                        HttpStatusCode.InternalServerError);
                }
                else
                {
                    WriteHTTPResponse(context, "Crypto deleted",
                        HttpStatusCode.OK);
                }
            }
        }

        void CreateTrade(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                // trades/fgrgic?action=buy&name=ethereum&amount=10
                // trades/fgrgic?action=sell&name=ethereum&amount=10

                request = request[1].Split("?")
                    .Where(s => !String.IsNullOrWhiteSpace(s)).ToArray();

                if (request.Length != 2)
                {
                    WriteHTTPResponse(context, "Error: Bad request",
                            HttpStatusCode.BadRequest);
                    return;
                }

                NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

                if (!IsAuthorized(context, request[0]))
                {
                    WriteHTTPResponse(context, "Error: Unauthorized",
                        HttpStatusCode.Unauthorized);
                    return;
                }

                Database.CreateTrade(
                    request[0],
                    collection.Get("action"),
                    collection.Get("name"),
                    collection.Get("amount"),
                    out string message,
                    out HttpStatusCode code);

                WriteHTTPResponse(context, message, code);
            }
        }

        void AddUSDT(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                // positions/fgrgic?amount=10

                request = request[1].Split("?")
                    .Where(s => !String.IsNullOrWhiteSpace(s)).ToArray();

                if (request.Length != 2)
                {
                    WriteHTTPResponse(context, "Error: Bad request",
                            HttpStatusCode.BadRequest);
                    return;
                }

                NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

                if (!IsAuthorized(context, request[0]))
                {
                    WriteHTTPResponse(context, "Error: Unauthorized",
                        HttpStatusCode.Unauthorized);
                    return;
                }

                if (Database.AddUSDT(request[0], collection.Get("amount")))
                {
                    WriteHTTPResponse(context, "USDT added",
                        HttpStatusCode.OK);
                }
                else
                {
                    WriteHTTPResponse(context, "Bad request",
                        HttpStatusCode.BadRequest);
                }
            }
        }

        void GetUser(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                // users/fgrgic

                if (!IsAuthorized(context, request[1]))
                {
                    WriteHTTPResponse(context, "Error: Unauthorized",
                        HttpStatusCode.Unauthorized);
                    return;
                }

                User user = Database.GetUser(request[1]);

                XmlSerializer serializer = new XmlSerializer(typeof(User));

                using (StringWriter writer = new StringWriter())
                {
                    serializer.Serialize(writer, user);
                    string serialized = writer.ToString();

                    WriteHTTPResponse(context, serialized, HttpStatusCode.OK);
                }
            }
        }

        void UpdateUser(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                // users/fgrgic?name=filip_grgic

                request = request[1].Split("?")
                    .Where(s => !String.IsNullOrWhiteSpace(s)).ToArray();

                if (request.Length != 2)
                {
                    WriteHTTPResponse(context, "Error: Bad request",
                            HttpStatusCode.BadRequest);
                    return;
                }

                NameValueCollection collection = HttpUtility.ParseQueryString(request[1]);

                if (!IsAuthorized(context, request[0]))
                {
                    WriteHTTPResponse(context, "Error: Unauthorized",
                        HttpStatusCode.Unauthorized);
                    return;
                }

                if (Database.UpdateUser(request[0], collection.Get("name")))
                {
                    WriteHTTPResponse(context, "Updated",
                        HttpStatusCode.OK);
                }
                else
                {
                    WriteHTTPResponse(context, "Bad request",
                        HttpStatusCode.BadRequest);
                }
            }
        }

        void DeleteUser(HttpListenerContext context, string[] request)
        {
            if (request.Length != 2)
            {
                WriteHTTPResponse(context, "Error: Bad request",
                        HttpStatusCode.BadRequest);
            }
            else
            {
                // users/fgrgic

                if (!IsAuthorized(context, request[1]))
                {
                    WriteHTTPResponse(context, "Error: Unauthorized",
                        HttpStatusCode.Unauthorized);
                    return;
                }

                if (Database.DeleteUser(request[1]))
                {
                    WriteHTTPResponse(context, "Deleted",
                        HttpStatusCode.OK);
                }
                else
                {
                    WriteHTTPResponse(context, "Bad request",
                        HttpStatusCode.BadRequest);
                }
            }
        }

        void WriteHTTPResponse(HttpListenerContext context, string msg, HttpStatusCode status)
        {
            int byteLen = Encoding.UTF8.GetByteCount(msg);

            context.Response.StatusCode = (int)status;
            context.Response.ContentLength64 = byteLen;

            using (Stream stream = context.Response.OutputStream)
            {
                stream.Write(Encoding.UTF8.GetBytes(msg), 0, byteLen);
            }
        }

        #endregion

        #region TCP_Server

        public enum Type
        {
            LOGIN,
            REGISTER
        }

        public enum Errors
        {
            INACTIVE_ACCOUNT = -1,
            WRONG_INFO = -2,
            USERNAME_EXISTS = -3,
            EMAIL_EXISTS = -4,
            UNKNOWN = -5,
            MAIL_NOT_SENT = -6
        }

        private void ProcessTCPRequest(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    Type type = GetType(stream);

                    switch (type)
                    {
                        case Type.LOGIN:
                            Login(stream);
                            break;
                        case Type.REGISTER:
                            Register(stream);
                            break;
                        default:
                            Console.WriteLine("Handler not implemented for request.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception caught: {ex.Message} \n {ex.StackTrace}");
            }
        }

        private Type GetType(NetworkStream stream)
        {
            int bytesRead = 0;
            int chunkSize = 1;
            byte[] typeBytes = new byte[4];

            while (bytesRead < typeBytes.Length && chunkSize > 0)
            {
                bytesRead += chunkSize =
                    stream.Read(typeBytes, bytesRead, typeBytes.Length - bytesRead);
            }

            int typeInteger = BitConverter.ToInt32(typeBytes);

            if (Enum.IsDefined(typeof(Type), typeInteger))
                return (Type)typeInteger;

            throw new InvalidOperationException("Unknown request from client.");
        }

        private string GetNextString(NetworkStream stream)
        {
            int bytesRead = 0;
            int chunkSize = 1;
            byte[] lenBytes = new byte[4];

            while (bytesRead < lenBytes.Length && chunkSize > 0)
            {
                bytesRead += chunkSize =
                    stream.Read(lenBytes, bytesRead, lenBytes.Length - bytesRead);
            }

            int len = BitConverter.ToInt32(lenBytes);
            bytesRead = 0;
            chunkSize = 1;
            byte[] stringBytes = new byte[len];

            while (bytesRead < stringBytes.Length && chunkSize > 0)
            {
                bytesRead += chunkSize =
                    stream.Read(stringBytes, bytesRead, stringBytes.Length - bytesRead);
            }

            return Encoding.UTF8.GetString(stringBytes);
        }

        private void Login(NetworkStream stream)
        {
            string userName = GetNextString(stream);
            string password = GetNextString(stream);

            Console.WriteLine($"Received log in request from {userName}");

            if (Database.Login(userName, password, out int userId))
            {
                Console.WriteLine($"{userName} logged in");
            }
            else
            {
                Console.WriteLine($"{userName} failed to log in");
            }

            stream.Write(BitConverter.GetBytes(userId));
        }

        private void Register(NetworkStream stream)
        {
            string userName = GetNextString(stream);
            string password = GetNextString(stream);
            string emailAddress = GetNextString(stream);

            Console.WriteLine($"Received register request from {userName}" +
                $" with email address {emailAddress}");

            if (Database.Register(userName, password, emailAddress, out int result))
            {
                Console.WriteLine($"{userName} registered.");

                if (SendMailToUser(userName, password, emailAddress))
                {
                    Console.WriteLine($"Mail sent to {emailAddress}.");
                }
            }
            else
            {
                Console.WriteLine($"{userName} failed to register.");
            }

            stream.Write(BitConverter.GetBytes(result));
        }

        private bool SendMailToUser(string userName, string password, string emailAddress)
        {
            try
            {
                MailClient client = new MailClient("smtp.gmail.com", 587);

                string body = httpEndPoint + "validate/" + userName + "-" + password;

                client.SendEmail(this.emailAddress, emailAddress, this.password, body);

                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send mail: " + ex.Message);
                return false;
            }
        }

        #endregion
    }
}