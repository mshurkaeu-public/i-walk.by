using System;
using System.Text.RegularExpressions;

namespace IWalkBy.https115бел.WebPortal
{
	public class RequestNumber
	{
		//examples: 89.11.170622, 2009.4.210822, 2037.21.190622
		private static Regex standardRejectedRequestCardNameRegex = new Regex(
			@"^(?<organizationId>\d+)\.(?<requestOrder>\d+)\.(?<approvedOnDate>\d{6})$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		public string AsString { get; private set; }
		public DateOnly Date { get; private set; }

		public RequestNumber(string requestNumber)
		{
			if (requestNumber == null) throw new ArgumentNullException(nameof(requestNumber));

			Match match = standardRejectedRequestCardNameRegex.Match(requestNumber);
			if (!match.Success)
			{
				throw new ArgumentException("Фармат параметра не адпавядае стандарту", nameof(requestNumber));
			}

			this.AsString = requestNumber;
			string dateStr = match.Groups["approvedOnDate"].Value;
			this.Date = DateOnly.ParseExact(dateStr, "ddMMyy");
		}

		public static bool MatchesFormat(string requestNumberCandidate)
		{
			Match match = standardRejectedRequestCardNameRegex.Match(requestNumberCandidate);
			return match.Success;
		}
	}
}