using Microsoft.Win32;
using ServerViTrader;
using System;
using System.Text;

namespace ViTraderServer
{
    class ServerRegistry : IDisposable
    {
        readonly RegistryKey key;
        static string registryLocation = @"SOFTWARE\serversettings";
        static string tcpAddressKey = "TcpAddressKey";
        static string httpAddressKey = "HttpAddressKey";
        static string tcpPortKey = "TcpPortKey";
        static string httpPortKey = "HttpPortKey";
        static string emailKey = "EmailKey";
        static string passwordKey = "PasswordKey";
        static string cryptoKey = "CryptoKey";

        public ServerRegistry()
        {
            key = Registry.CurrentUser.CreateSubKey(registryLocation);
        }

        public string GetTCPServerAddress() => key.GetValue(tcpAddressKey).ToString();

        public string GetHTTPServerAddress() => key.GetValue(httpAddressKey).ToString();

        public int GetTCPServerPort() => int.Parse(key.GetValue(tcpPortKey).ToString());

        public int GetHTTPServerPort() => int.Parse(key.GetValue(httpPortKey).ToString());

        public string GetServerEmail()
        {
            byte[] emailBytes = (byte[])key.GetValue(emailKey);
            string cryptoKeyStr = key.GetValue(cryptoKey).ToString();

            Cryptography crypto = new(emailBytes, cryptoKeyStr);

            return crypto.DecryptRSA();
        }

        public string GetServerEmailPassword()
        {
            byte[] passwordBytes = (byte[])key.GetValue(passwordKey);
            string cryptoKeyStr = key.GetValue(cryptoKey).ToString();

            Cryptography crypto = new(passwordBytes, cryptoKeyStr);

            return crypto.DecryptRSA();
        }

        public void SetTCPServerAddress(string address) => key.SetValue(tcpAddressKey, address);

        public void SetHTTPServerAddress(string address) => key.SetValue(httpAddressKey, address);

        public void SetTCPServerPort(int port) => key.SetValue(tcpPortKey, port);

        public void SetHTTPServerPort(int port) => key.SetValue(httpPortKey, port);

        public void SetServerEmailAddressAndPassword(string address, string password)
        {
            Cryptography crypto = new(Encoding.UTF8.GetBytes(address));
            byte[] emailBytes = crypto.EncryptRSA();

            string publicKey = crypto.Key;
            crypto = new Cryptography(Encoding.UTF8.GetBytes(password), publicKey);
            byte[] passwordBytes = crypto.EncryptRSA();

            key.SetValue(emailKey, emailBytes);
            key.SetValue(passwordKey, passwordBytes);

            key.SetValue(cryptoKey, publicKey);
        }

        public void Dispose()
        {
            key.Close();
        }
    }
}
