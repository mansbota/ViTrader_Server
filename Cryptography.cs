using System.Security.Cryptography;
using System.Text;

namespace ServerViTrader
{
    class Cryptography
    {
        byte[] bytes;
        string key;

        public string Key { get; private set; }

        public Cryptography(byte[] bytes, string key = null)
        {
            this.bytes = bytes;
            this.key = key;
        }

        public byte[] EncryptRSA()
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                if (key != null)
                    rsa.FromXmlString(key);
                else
                    key = rsa.ToXmlString(true);

                byte[] encrypted = rsa.Encrypt(bytes, true);

                return encrypted;
            }
        }

        public string DecryptRSA()
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(key);
                byte[] decrypted = rsa.Decrypt(bytes, true);

                return Encoding.UTF8.GetString(decrypted);
            }
        }
    }
}