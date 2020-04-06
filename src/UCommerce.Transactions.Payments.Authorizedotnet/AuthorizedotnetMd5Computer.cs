using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Ucommerce.Transactions.Payments.Authorizedotnet
{
    public class AuthorizedotnetMd5Computer : AbstractMd5Computer
    {
        /// <summary>
        /// See http://developer.authorize.net/guides/SIM/ (Subject: Custom Transaction Fingerprint Code)
        /// </summary>
        /// <param name="transactionKey">Transaction Key</param>
        /// <param name="apiLogin">Api login</param>
        /// <param name="sequence">Sequence nr - Payment id</param>
        /// <param name="timestamp">timestamp - create utc time in seconds sins 1/1 1970</param>
        /// <param name="amount">Amount</param>
        /// <returns>Md5 key</returns>
        public string GetPreMd5Key(string transactionKey, string apiLogin, string sequence, string timestamp, string amount)
        {
            // The first two lines take the input values and convert them from strings to Byte arrays
            byte[] HMACkey = (new ASCIIEncoding()).GetBytes(transactionKey);
            const string format = "{0}^{1}^{2}^{3}^";
            byte[] HMACdata = (new ASCIIEncoding()).GetBytes(string.Format(format, apiLogin, sequence, timestamp, amount));

            // create a HMACMD5 object with the key set
            HMACMD5 myhmacMD5 = new HMACMD5(HMACkey);

            //calculate the hash (returns a byte array)
            byte[] HMAChash = myhmacMD5.ComputeHash(HMACdata);

            //loop through the byte array and add append each piece to a string to obtain a hash string
            string fingerprint = "";
            for (int i = 0; i < HMAChash.Length; i++)
            {
                fingerprint += HMAChash[i].ToString("x").PadLeft(2, '0');
            }

            return fingerprint;
        }

        /// <summary>
        /// See http://developer.authorize.net/guides/SIM/ (Subject: Using the MD5 Hash Feature) and http://developer.authorize.net/downloads/samplecode/ (SIM code)
        /// </summary>
        /// <param name="md5Hash">Md5 Hash specified in account</param>
        /// <param name="apiLogin">Api login</param>
        /// <param name="transactionId">Transaction Id</param>
        /// <param name="amount">Amount</param>
        /// <param name="expected">Expected Md5Hash from respons param</param>
        /// <returns>Md5hash is match</returns>
        public bool IsMatch(string md5Hash, string apiLogin, string transactionId, string amount, string expected)
        {
            var unencrypted = string.Format("{0}{1}{2}{3}", md5Hash, apiLogin, transactionId, amount);

            var md5 = new MD5CryptoServiceProvider();
            var hashed = Regex.Replace(BitConverter.ToString(md5.ComputeHash(Encoding.Default.GetBytes(unencrypted))), "-", "");

            return hashed.Equals(expected.ToUpper());
        }
    }
}
