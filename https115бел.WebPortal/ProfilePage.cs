using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace IWalkBy.https115бел.WebPortal
{
	public class ProfilePage: OracleApexPage
	{
		private string logoutAjaxIdentifier;
		
		public ProfilePage(Uri portalUri, string oracleApexAppId, string oracleApexSessionId):
			//"6" == "PROFILE"
			base(portalUri, oracleApexAppId, "6", oracleApexSessionId)
		{
			this.oracleApexClearCache = "6";
		}
		
		public void Logout(HttpClient httpClient)
		{
			NavigateAndParse(httpClient);

			string json = BuildJsonToPost((JToken)null);
			ExecuteNativeAction(
				httpClient,
				logoutAjaxIdentifier,
				json).Wait();
		}
		
		protected override void Parse(string html)
		{
			base.Parse(html);

			//{"triggeringElementType":"BUTTON","triggeringButtonId":"B45425002776522339","conditionElement":"P6_PROP_EMAIL","bindType":"bind","bindEventType":"click","anyActionsFireOnInit":false,actionList:[{"eventResult":true,"executeOnPageInit":false,"stopExecutionOnError":true,javascriptFunction:apex.da.submitPage,"attribute01":"EXIT_ACTION","attribute02":"Y","action":"NATIVE_SUBMIT_PAGE"},{"eventResult":true,"executeOnPageInit":false,"stopExecutionOnError":true,"waitForResult":true,javascriptFunction:apex.da.executePlSqlCode,"ajaxIdentifier":"
			const string logoutAjaxIdentifierPrefix = "{\"triggeringElementType\":\"BUTTON\",\"triggeringButtonId\":\"B45425002776522339\",\"conditionElement\":\"P6_PROP_EMAIL\",\"bindType\":\"bind\",\"bindEventType\":\"click\",\"anyActionsFireOnInit\":false,actionList:[{\"eventResult\":true,\"executeOnPageInit\":false,\"stopExecutionOnError\":true,javascriptFunction:apex.da.submitPage,\"attribute01\":\"EXIT_ACTION\",\"attribute02\":\"Y\",\"action\":\"NATIVE_SUBMIT_PAGE\"},{\"eventResult\":true,\"executeOnPageInit\":false,\"stopExecutionOnError\":true,\"waitForResult\":true,javascriptFunction:apex.da.executePlSqlCode,\"ajaxIdentifier\":\"";
			this.logoutAjaxIdentifier = TextUtils.GetTextBetweenPrefixAndPostfix(html, logoutAjaxIdentifierPrefix, "\"");
		}
	}
}