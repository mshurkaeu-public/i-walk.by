using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace IWalkBy.https115бел.WebPortal
{
	public class LoginDialog: OracleApexDialog
	{
		public LoginDialog(Uri portalUri,
		                   string oracleApexAppId,
		                   string oracleApexSessionId,
		                   string oracleApexDialogChecksum)
			:base(portalUri, oracleApexAppId, "3", oracleApexSessionId, oracleApexDialogChecksum)
		{
			this.oracleApexClearCache = "3";
		}

		public void Login(HttpClient httpClient, string username, string password)
		{
			string html = NavigateAndParse(httpClient);
			
			const string loginButtonClickAjaxIdentifierPrefix = "{\"triggeringElementType\":\"BUTTON\",\"triggeringButtonId\":\"B31855928121779133\",\"conditionElement\":\"P3_LOGIN_RESULT\",\"bindType\":\"bind\",\"bindEventType\":\"click\",\"anyActionsFireOnInit\":false,actionList:[{\"eventResult\":true,\"executeOnPageInit\":false,\"stopExecutionOnError\":true,\"waitForResult\":true,\"affectedElementsType\":\"ITEM\",\"affectedElements\":\"P3_LOGIN_RESULT\",javascriptFunction:apex.da.setValue,\"ajaxIdentifier\":\"";
			string loginButtonClickAjaxIdentifier = TextUtils.GetTextBetweenPrefixAndPostfix(html, loginButtonClickAjaxIdentifierPrefix, "\"");

			//send POST request to https://115.xn--90ais/portal/wwv_flow.ajax
			//to execute some "NATIVE_SET_VALUE" action (called by click on "Войти" button)
			string json = BuildJsonToPost(
				new KeyValuePair<string, object>("P3_USERNAME", username),
				new KeyValuePair<string, object>("P3_PASSWORD", password)
			);
			HttpResponseMessage httpResponseMessage = ExecuteNativeAction(
				httpClient,
				loginButtonClickAjaxIdentifier,
				json).Result;
			
			string responseText = httpResponseMessage.Content.ReadAsStringAsync().Result;
			//If the password is wrong then the response is
			//{"value":"NO_VALID_USER"}
			if (responseText != "{\"value\":\"RESULT_OK\"}\n")
			{
				throw new NotImplementedException("Handling of login failure on step 1 is not implemented");
			}

			const string loginResultSuccessAjaxIdentifierPrefix = "{\"triggeringElementType\":\"ITEM\",\"triggeringElement\":\"P3_LOGIN_RESULT\",\"conditionElement\":\"P3_LOGIN_RESULT\",\"triggeringConditionType\":\"EQUALS\",\"triggeringExpression\":\"RESULT_OK\",\"bindType\":\"bind\",\"bindEventType\":\"change\",\"anyActionsFireOnInit\":false,actionList:[{\"eventResult\":true,\"executeOnPageInit\":false,\"stopExecutionOnError\":true,\"waitForResult\":true,javascriptFunction:apex.da.executePlSqlCode,\"ajaxIdentifier\":\"";
			string loginResultSuccessAjaxIdentifier = TextUtils.GetTextBetweenPrefixAndPostfix(html, loginResultSuccessAjaxIdentifierPrefix, "\"");

			//send POST request to https://115.xn--90ais/portal/wwv_flow.ajax
			//to execute some "NATIVE_EXECUTE_PLSQL_CODE" action (called after successful authentication call)
			//this call should set PORTAL_USER_TOKEN cookie
			json = BuildJsonToPost(
				new KeyValuePair<string, object>("P3_USERNAME", username),
				new KeyValuePair<string, object>("P3_PASSWORD", password),
				new KeyValuePair<string, object>("P3_REMEMBER", new JArray())
			);
			ExecuteNativeAction(
				httpClient,
				loginResultSuccessAjaxIdentifier,
				json).Wait();
		}
	}
}