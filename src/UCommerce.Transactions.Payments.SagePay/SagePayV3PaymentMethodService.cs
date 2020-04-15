using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Ucommerce.EntitiesV2;
using Ucommerce.Extensions;
using Ucommerce.Infrastructure;
using Ucommerce.Transactions.Payments.Common;
using Ucommerce.Web;

namespace Ucommerce.Transactions.Payments.SagePay
{
	public class SagePayV3PaymentMethodService : SagePayPaymentMethodService
	{
		public SagePayV3PaymentMethodService(SagePayMd5Computer md5Computer, INumberSeriesService numberSeriesService, IAbsoluteUrlService absoluteUrlService, ICallbackUrl callbackUrl) : base(md5Computer, numberSeriesService, absoluteUrlService, callbackUrl)
		{
		}

		protected override string PROTOCOL_VERSION
		{
			get { return "3.00"; }
		}

		/// <summary>
		/// Used in ProcessCallback for generating an MD5 check.
		/// </summary>
		/// <param name="request"></param>
		/// <param name="securityKey"></param>
		/// <param name="paymentMethod"></param>
		/// <returns></returns>
		/// <remarks>V.3 of the protocol contains more details when generating the hash. That is why we have an overriden method here.</remarks>
		protected override IList<string> GetSignatureParameterList(HttpRequest request, string securityKey, PaymentMethod paymentMethod)
		{
			IList<string> signatureParameterList = base.GetSignatureParameterList(request, securityKey, paymentMethod);

			signatureParameterList.Add(GetHttpRequestValueUrlDecoded(request, "DeclineCode"));
			signatureParameterList.Add(GetHttpRequestValueUrlDecoded(request, "ExpiryDate"));
			signatureParameterList.Add(GetHttpRequestValueUrlDecoded(request, "FraudResponse"));
			signatureParameterList.Add(GetHttpRequestValueUrlDecoded(request, "BankAuthCode"));

			return signatureParameterList;
		}

		/// <summary>t
		/// Cancels a payment that hasn't already been settled.
		/// </summary>
		/// <param name="payment"></param>
		/// <param name="status"></param>
		/// <returns></returns>
		/// <remarks>To Support DEFFERED payments in 3.0 we've derrived from the standard gateway, and will check if deffered payments are configured. If so, we need to call an abort operation.</remarks>
		protected override bool CancelPaymentInternal(Payment payment, out string status)
		{
			string txType = payment.PaymentMethod.DynamicProperty<string>().TxType;
			string vendor = payment.PaymentMethod.DynamicProperty<string>().Vendor;

			if (txType == SagePayTransactionType.DEFERRED.ToString())
				return CancelDeferredPayment(payment, out status, vendor);
			
			return base.CancelPaymentInternal(payment, out status);
		}

		private bool CancelDeferredPayment(Payment payment, out string status, string vendor)
		{
			var systemUrl = GetSystemURL("abort", payment.PaymentMethod);

			IDictionary<string, string> dict = new Dictionary<string, string>();
			dict.Add("VPSProtocol", PROTOCOL_VERSION);
			dict.Add("TxType", "ABORT");
			dict.Add("Vendor", vendor);
			dict.Add("VendorTxCode", payment.ReferenceId);
			dict.Add("VPSTxId", payment.GetSagePaymentInfo(FieldCode.VPSTxId));
			dict.Add("SecurityKey", payment.GetSagePaymentInfo(FieldCode.SecurityKey));
			dict.Add("TxAuthNo", payment.GetSagePaymentInfo(FieldCode.TxAuthNo));

			var post = new HttpPost(systemUrl, dict);
			var response = post.GetString();

			status = GetField("StatusDetail", response);
			var stringStatus = GetField("status", response);
			var getStatus = GetStatus(stringStatus);
			switch (getStatus)
			{
				case SagePayStatusCode.Ok:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// acquires a payment that hasn't already been settled.
		/// </summary>
		/// <param name="payment"></param>
		/// <param name="status"></param>
		/// <returns></returns>
		/// <remarks>To Support DEFFERED payments in 3.0 we've derrived from the standard gateway, and will check if deffered payments are configured. If so, we need to call a release operation.</remarks>
		protected override bool AcquirePaymentInternal(Payment payment, out string status)
		{
			string txType = payment.PaymentMethod.DynamicProperty<string>().TxType;
			string vendor = payment.PaymentMethod.DynamicProperty<string>().Vendor;

			if (txType == SagePayTransactionType.DEFERRED.ToString())
				return AcquireDeferredPayment(payment, out status, vendor);
			
			return base.AcquirePaymentInternal(payment, out status);				
		}

		protected virtual bool AcquireDeferredPayment(Payment payment, out string status, string vendor)
		{
			var systemUrl = GetSystemURL("release", payment.PaymentMethod);

			IDictionary<string, string> dict = new Dictionary<string, string>();
			dict.Add("VPSProtocol", PROTOCOL_VERSION);
			dict.Add("TxType", "RELEASE");
			dict.Add("Vendor", vendor);
			dict.Add("VendorTxCode", payment.ReferenceId);
			dict.Add("VPSTxId", payment.GetSagePaymentInfo(FieldCode.VPSTxId));
			dict.Add("SecurityKey", payment.GetSagePaymentInfo(FieldCode.SecurityKey));
			dict.Add("TxAuthNo", payment.GetSagePaymentInfo(FieldCode.TxAuthNo));
			dict.Add("ReleaseAmount", payment.Amount.ToString("0.00", CultureInfo.InvariantCulture));

			var post = new HttpPost(systemUrl, dict);
			var response = post.GetString();

			status = GetField("StatusDetail", response);
			var stringStatus = GetField("status", response);
			var getStatus = GetStatus(stringStatus);
			switch (getStatus)
			{
				case SagePayStatusCode.Ok:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Get the URL for which an appropiate action needs to be sent.
		/// </summary>
		/// <param name="strType"></param>
		/// <param name="paymentMethod"></param>
		/// <returns></returns>
		/// <remarks>System urls are the same, but the way we figure out which one to use is different.</remarks>
		protected override string GetSystemURL(string strType, PaymentMethod paymentMethod)
		{
			bool testMode = paymentMethod.DynamicProperty<bool>().TestMode;

			var requestType = strType.ToLower();
			if (testMode)
			{
				switch (requestType)
				{
					case "abort"		:	return "https://test.sagepay.com/gateway/service/abort.vsp";
					case "authorise"	:	return "https://test.sagepay.com/gateway/service/authorise.vsp";
					case "cancel"		:	return "https://test.sagepay.com/gateway/service/cancel.vsp";
					case "purchase"		:	return "https://test.sagepay.com/gateway/service/vspserver-register.vsp";
					case "refund"		:	return "https://test.sagepay.com/gateway/service/refund.vsp";
					case "release"		:	return "https://test.sagepay.com/gateway/service/release.vsp";
					case "repeat"		:	return "https://test.sagepay.com/gateway/service/repeat.vsp";
					case "void"			:	return "https://test.sagepay.com/gateway/service/void.vsp";
					case "3dcallback"	:	return "https://test.sagepay.com/gateway/service/direct3dcallback.vsp";
					case "showpost"		:	return "https://test.sagepay.com/showpost/showpost.asp";
				}
			}			
			else
			{
				switch (requestType)
				{
					case "abort"		: return "https://live.sagepay.com/gateway/service/abort.vsp";
					case "authorise"	: return "https://live.sagepay.com/gateway/service/authorise.vsp";
					case "cancel"		: return "https://live.sagepay.com/gateway/service/cancel.vsp";
					case "purchase"		: return "https://live.sagepay.com/gateway/service/vspserver-register.vsp";
					case "refund"		: return "https://live.sagepay.com/gateway/service/refund.vsp";
					case "release"		: return "https://live.sagepay.com/gateway/service/release.vsp";
					case "repeat"		: return "https://live.sagepay.com/gateway/service/repeat.vsp";
					case "void"			: return "https://live.sagepay.com/gateway/service/void.vsp";
					case "3dcallback"	: return "https://live.sagepay.com/gateway/service/direct3dcallback.vsp";
					case "showpost"		: return "https://test.sagepay.com/showpost/showpost.asp";
				}
			}
			
			throw new InvalidOperationException(string.Format("Could not figure out request: {0}. Testmode: {1}", strType,testMode));
		}
	}
}
