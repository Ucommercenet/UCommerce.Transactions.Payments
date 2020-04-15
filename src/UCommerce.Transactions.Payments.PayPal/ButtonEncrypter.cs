using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Web.Hosting;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;

namespace Ucommerce.Transactions.Payments.PayPal
{
	public class ButtonEncrypter
	{
		private Encoding _encoding = Encoding.Default;

		private readonly X509Certificate2 _signerCert;
		private readonly X509Certificate2 _recipientCert;

		public ButtonEncrypter(PaymentMethod paymentMethod)
		{
			var selfCertificatePath = HostingEnvironment.MapPath(paymentMethod.DynamicProperty<string>().PrivateCertificatePath);
			var publicPayPalCertificatePath = HostingEnvironment.MapPath(paymentMethod.DynamicProperty<string>().PublicPayPalCertificatePath);

			CheckFileExists(selfCertificatePath);
			CheckFileExists(publicPayPalCertificatePath);

			_signerCert = new X509Certificate2(selfCertificatePath, paymentMethod.DynamicProperty<string>().PrivateCertificatePassword, X509KeyStorageFlags.MachineKeySet);
			_recipientCert = new X509Certificate2(publicPayPalCertificatePath);
		}

		private void CheckFileExists(string path)
		{
			if (!File.Exists(path))
				throw new Exception(string.Format("Certificate file: {0} does not exist.", path));
		}

		/// <summary>
		/// Character encoding, e.g. UTF-8, Windows-1252
		/// </summary>
		public string Charset
		{
			get { return _encoding.WebName; }
			set
			{
				if (!string.IsNullOrEmpty(value))
				{
					_encoding = Encoding.GetEncoding(value);
				}
			}
		}

		/// <summary>
		/// Sign a message and encrypt it for the recipient.
		/// </summary>
		/// <param name="message">Name value pairs
		/// must be separated by \n (vbLf or chr&#40;10)),
		/// for example "cmd=_xclick\nbusiness=..."</param>
		/// <returns></returns>
		public string SignAndEncrypt(string message)
		{
			byte[] signedMessage = sign(_encoding.GetBytes(message));
			byte[] encryptedMessage = envelope(signedMessage);

			return formatEncoded(encryptedMessage);
		}

		public string SignAndEncrypt(IDictionary<string,string> formValues)
		{
			var sb = new StringBuilder();
			
			foreach (KeyValuePair<string, string> de in formValues)
			{
				sb.Append(de.Key).Append("=").Append(de.Value).Append("\n");
			}

			return SignAndEncrypt(sb.ToString());
		}

		private byte[] sign(byte[] message)
		{
			var content = new ContentInfo(message);
			var signedContent = new SignedCms(content);
			var signer = new CmsSigner(_signerCert);

			signedContent.ComputeSignature(signer);
			return signedContent.Encode();
		}

		private byte[] envelope(byte[] message)
		{
			var content = new ContentInfo(message);
			var envelopedContent = new EnvelopedCms(content);
			var recipient = new CmsRecipient(SubjectIdentifierType.IssuerAndSerialNumber, _recipientCert);

			envelopedContent.Encrypt(recipient);
			return envelopedContent.Encode();
		}

		private static string formatEncoded(byte[] message)
		{
			const string PKCS7_HEADER = "-----BEGIN PKCS7-----";
			const string PKCS7_FOOTER = "-----END PKCS7-----";

			string base64 = Convert.ToBase64String(message);
			var formatted = new StringBuilder();
			formatted.Append(PKCS7_HEADER);
			formatted.Append(base64.Replace("\r\n", ""));
			formatted.Append(PKCS7_FOOTER);

			return formatted.ToString();
		}
	}
}