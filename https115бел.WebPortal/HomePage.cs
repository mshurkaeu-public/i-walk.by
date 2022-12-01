using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace IWalkBy.https115бел.WebPortal
{
	public class HomePage: OracleApexPage
	{
		private string createRequestDialogChecksum;

		public HomePage(Uri portalUri, string oracleApexAppId, string oracleApexSessionId):
			//"1" == "HOME"
			base(portalUri, oracleApexAppId, "1", oracleApexSessionId)
		{
		}

		public void CreateRequest(HttpClient httpClient, Request request)
		{
			if (createRequestDialogChecksum == null)
			{
				NavigateAndParse(httpClient);

				HttpResponseMessage httpResponseMessage = Submit(httpClient, "CreateReq", "S",
					"{" +
						"\"pageItems\":{" +
							"\"itemsToSubmit\":[" +
								"{\"n\":\"P1_REGION\",\"v\":\"21\"}," + //Менск
								"{\"n\":\"P1_ADDRESS\",\"v\":\"\"}" +
							"]," +
							"\"protected\":\"" + pPageItemsProtected + "\"," +
							"\"rowVersion\":\"" + pPageItemsRowVersion + "\"" +
						"}," +
						"\"salt\":\"" + pPageSubmissionId + "\"" +
					"}").Result;
				string jsonResponse = httpResponseMessage.Content.ReadAsStringAsync().Result;

				string p_dialog_csPrefix = "{\"redirectURL\":\"javascript:apex.navigation.dialog(\\u0027f?p=" + oracleApexAppId + ":9:" + oracleApexSessionId + "::NO:RP,9::\\u005Cu0026p_dialog_cs=";
				createRequestDialogChecksum = TextUtils.GetTextBetweenPrefixAndPostfix(jsonResponse, p_dialog_csPrefix, "\\u0027");
			}

			CreateRequestDialog createRequestDialog = new CreateRequestDialog(portalUri, oracleApexAppId, oracleApexSessionId, createRequestDialogChecksum);
			createRequestDialog.CreateRequest(httpClient, request);
		}
	}
}