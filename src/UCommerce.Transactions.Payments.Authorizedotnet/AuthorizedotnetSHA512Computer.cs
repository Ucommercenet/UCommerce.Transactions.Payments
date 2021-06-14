using System;
using System.Linq;
using System.Security.Cryptography;

namespace Ucommerce.Transactions.Payments.Authorizedotnet
{
    public class AuthorizedotnetSHA512Computer : AbstractMd5Computer
    {
        public static string GetSHA512HashKey(string key, string textToHash)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException("HMACSHA512: key", "Parameter cannot be empty.");
            if (string.IsNullOrEmpty(textToHash))
                throw new ArgumentNullException("HMACSHA512: textToHash", "Parameter cannot be empty.");
            if (key.Length % 2 != 0 || key.Trim().Length < 2)
            {
                throw new ArgumentNullException("HMACSHA512: key", "Parameter cannot be odd or less than 2 characters.");
            }
            try
            {
                byte[] k = Enumerable.Range(0, key.Length)
                    .Where(x => x % 2 == 0)
                    .Select(x => Convert.ToByte(key.Substring(x, 2), 16))
                    .ToArray();
                HMACSHA512 hmac = new HMACSHA512(k);
                byte[] HashedValue = hmac.ComputeHash((new System.Text.ASCIIEncoding()).GetBytes(textToHash));
                return BitConverter.ToString(HashedValue).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                throw new Exception("HMACSHA512: " + ex.Message);
            }
        }
    }
}
