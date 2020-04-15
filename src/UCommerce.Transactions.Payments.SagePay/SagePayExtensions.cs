using System;
using System.Collections.Generic;
using System.Linq;
using Ucommerce.EntitiesV2;

namespace Ucommerce.Transactions.Payments.SagePay
{
    public enum FieldCode
    {
        VPSTxId,
        TxAuthNo,
        SecurityKey,
    }

    public static class SagePayExtensions
    {
        private const char SEP_CHAR = ',';
        private const string JOIN_STRING = "=";

        private static void SaveSagePaymentInfo(this Payment payment, IDictionary<FieldCode, string> item)
        {
            payment.TransactionId = string.Join(SEP_CHAR.ToString(), item.Select(a => a.Key.ToString() + JOIN_STRING + a.Value).ToArray());
        }

        public static void SetSagePaymentInfo(this Payment payment, FieldCode code, string value)
        {
            var loadSagePaymentInfo = payment.LoadSagePaymentInfo();
            loadSagePaymentInfo[code] = value;
            payment.SaveSagePaymentInfo(loadSagePaymentInfo);
        }

        public static string GetSagePaymentInfo(this Payment payment, FieldCode code)
        {
            var loadSagePaymentInfo = payment.LoadSagePaymentInfo();
            return loadSagePaymentInfo[code];
        }

        private static IDictionary<FieldCode, string> LoadSagePaymentInfo(this Payment payment)
        {
            var strings = (payment.TransactionId ?? "").Split(new char[] {SEP_CHAR}, StringSplitOptions.RemoveEmptyEntries);
            var dict = new Dictionary<FieldCode, string>();

            foreach (var item in strings)
            {
                var indexOf = item.IndexOf(JOIN_STRING);
                if (indexOf == 0)
                    continue;

                var name = item.Substring(0, indexOf);
                if(name.Length == 0)
                    continue;

                var value = item.Substring(indexOf + 1);
                if(value.Length == 0)
                    continue;

                var code = (FieldCode)Enum.Parse(typeof (FieldCode), name, true);

                dict.Add(code, value);
            }

            foreach (var value in Enum.GetValues(typeof(FieldCode)))
            {
                var code = (FieldCode) value;
                if(!dict.ContainsKey(code))
                    dict.Add(code, "");
            }

            return dict;
        }
    }
}