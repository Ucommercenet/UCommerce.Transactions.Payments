using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ucommerce.Transactions.Payments.GlobalCollect.Api;
using Ucommerce.Transactions.Payments.GlobalCollect.Api.Parts;

namespace Ucommerce.Transactions.Payments.GlobalCollect
{
	public class GlobalCollectException : Exception
	{
		private readonly List<ErrorRow> _errors = new List<ErrorRow>();

		public IList<ErrorRow> Errors { get { return _errors; } }

		public GlobalCollectException(ErrorChecker checker) : base(checker.Errors.First().Message)
		{
			_errors.AddRange(checker.Errors);
		}
	}

	public class PaymentAmountOutOfRangeExeption : GlobalCollectException
	{
		public PaymentAmountOutOfRangeExeption(ErrorChecker checker): base(checker)
		{
			
		}
	}
}
