using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace IWalkBy.https115бел.WebPortal
{
	public class EditRequestDialog: OracleApexDialog
	{
		private string requestId;
		private string dataForP31_ID_REQUEST;
		private string P31_LAT;
		private string P31_LNG;

		public EditRequestDialog(Uri portalUri,
		                   string oracleApexAppId,
		                   string oracleApexSessionId,
		                   string oracleApexDialogChecksum,
		                   string requestId)
			:base(portalUri, oracleApexAppId, "31", oracleApexSessionId, oracleApexDialogChecksum)
		{
			this.oracleApexClearCache = "RP,31";
			this.oracleApexItemNames = "P31_ID_REQUEST";
			this.oracleApexItemValues = requestId;
			this.requestId = requestId;
		}

		public void EditRequestDescription(HttpClient httpClient, string newDescription)
		{
			string html = NavigateAndParse(httpClient);

			if (String.IsNullOrWhiteSpace(P31_LAT) || String.IsNullOrWhiteSpace(P31_LNG))
			{
				throw new NotImplementedException("Не ведаю што рабіць калі шырыня або даўгата не ўстаноўлены");
			}

			string json = BuildJsonToPost(
				new KeyValuePair<string, object>("P31_ID_REQUEST", new ValueWithChecksum(requestId, dataForP31_ID_REQUEST)),
				new KeyValuePair<string, object>("P31_REGION", ""),
				new KeyValuePair<string, object>("P31_ADDRESS", ""),
				new KeyValuePair<string, object>("P31_LAT", P31_LAT),
				new KeyValuePair<string, object>("P31_LNG", P31_LNG),
				new KeyValuePair<string, object>("P31_USER_COMMENT", newDescription)
			);
			HttpResponseMessage httpResponseMessage = this.Submit(httpClient, "Edit", "S", json).Result;
			string responseText = httpResponseMessage.Content.ReadAsStringAsync().Result;
			Console.WriteLine(responseText);
		}

		protected override void Parse(string html)
		{
			base.Parse(html);

			const string dataForP31_ID_REQUESTPrefix = "<input type=\"hidden\" data-for=\"P31_ID_REQUEST\" value=\"";
			dataForP31_ID_REQUEST = TextUtils.GetTextBetweenPrefixAndPostfix(html, dataForP31_ID_REQUESTPrefix, "\"");

			const string P31_LATPrefix = "<input type=\"hidden\" id=\"P31_LAT\" name=\"P31_LAT\" value=\"";
			P31_LAT = TextUtils.GetTextBetweenPrefixAndPostfix(html, P31_LATPrefix, "\"");

			const string P31_LNGPrefix = "<input type=\"hidden\" id=\"P31_LNG\" name=\"P31_LNG\" value=\"";
			P31_LNG = TextUtils.GetTextBetweenPrefixAndPostfix(html, P31_LNGPrefix, "\"");
		}
	}
}