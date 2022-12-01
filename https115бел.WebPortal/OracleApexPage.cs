using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace IWalkBy.https115бел.WebPortal
{
	/// <summary>
	/// Documentation about URLs, used by Oracle APEX 18.1 is avaiable here
	/// https://docs.oracle.com/database/apex-18.1/HTMDB/understanding-url-syntax.htm 
	/// </summary>
	public class OracleApexPage
	{
		protected Uri portalUri { get; private set; }
		protected string oracleApexAppId { get; private set; }
		protected string oracleApexPageId { get; private set; }
		protected string oracleApexSessionId { get; private set; }

		protected string oracleApexRequest { get; set; }
		protected string oracleApexDebug { get; set; }
		protected string oracleApexClearCache { get; set; }
		protected string oracleApexItemNames { get; set; }
		protected string oracleApexItemValues { get; set; }
		protected string oracleApexPrinterFriendly { get; set; }

		protected string pPageItemsProtected { get; private set; }
		protected string pPageItemsRowVersion { get; private set; }
		protected string pPageSubmissionId { get; private set; }

		public OracleApexPage(Uri portalUri,
		                      string oracleApexAppId,
		                      string oracleApexPageId,
		                      string oracleApexSessionId)
		{
			this.portalUri = portalUri;
			this.oracleApexAppId = oracleApexAppId;
			this.oracleApexPageId = oracleApexPageId;
			this.oracleApexSessionId = oracleApexSessionId;

			this.oracleApexRequest = "";
			this.oracleApexDebug = "NO";
			this.oracleApexClearCache = "";
			this.oracleApexItemNames = "";
			this.oracleApexItemValues = "";
			this.oracleApexPrinterFriendly = "";
		}

		public string BuildJsonToPost(JToken pageItemsValue)
		{
			JObject rootObject = new JObject();
			rootObject["pageItems"] = pageItemsValue;
			rootObject["salt"] = pPageSubmissionId;

			string json = JsonConvert.SerializeObject(rootObject);
			//Console.WriteLine(json);
			return json;
		}

		public string BuildJsonToPost(params KeyValuePair<string, object>[] itemsToSubmit)
		{
			if (itemsToSubmit == null) throw new ArgumentNullException(nameof(itemsToSubmit));

			JObject pageItems = new JObject();

			JArray itemsToSubmitJArray = new JArray();
			pageItems["itemsToSubmit"] = itemsToSubmitJArray;
			pageItems["protected"] = pPageItemsProtected;
			pageItems["rowVersion"] = pPageItemsRowVersion;

			foreach(KeyValuePair<string, object> kvp in itemsToSubmit)
			{
				JObject itemToSubmit = new JObject();
				itemToSubmit["n"] = kvp.Key;
				object v = kvp.Value;
				if (v == null)
				{
					itemToSubmit["v"] = null;
				}
				else if (v is string)
				{
					itemToSubmit["v"] = (string)v;
				}
				else if (v is JArray)
				{
					itemToSubmit["v"] = (JArray)v;
				}
				else if (v is ValueWithChecksum)
				{
					ValueWithChecksum vwc = (ValueWithChecksum)v;
					itemToSubmit["v"] = vwc.Value;
					itemToSubmit["ck"] = vwc.Checksum;
				}
				else
				{
					throw new NotImplementedException("Падтрымка тыпу " + v.GetType().Name + " не зроблена.");
				}

				itemsToSubmitJArray.Add(itemToSubmit);
			}

			return BuildJsonToPost(pageItems);
		}

		public virtual Uri BuidPageUri()
		{
			Uri pageUri = new Uri(portalUri,
			                      "f?" +
			                      "p=" + oracleApexAppId + ":" + 
			                             oracleApexPageId + ":" +
			                             oracleApexSessionId + ":" +
			                             oracleApexRequest + ":" +
			                             oracleApexDebug + ":" +
			                             oracleApexClearCache + ":" +
			                             oracleApexItemNames + ":" +
			                             oracleApexItemValues +
			                             (String.IsNullOrEmpty(oracleApexPrinterFriendly) ? "" : ":"+oracleApexPrinterFriendly));
			return pageUri;
		}

		public Task<HttpResponseMessage> ExecuteNativeAction(HttpClient httpClient, string actionAjaxIdentifier, string json)
		{
			return ExecuteNativeAction(httpClient, actionAjaxIdentifier, new KeyValuePair<string, string>("p_json", json));
		}

		public Task<HttpResponseMessage> ExecuteNativeAction(
			HttpClient httpClient,
			string actionAjaxIdentifier,
			params KeyValuePair<string, string>[] additionalFields)
		{
			if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
			if (additionalFields == null) throw new ArgumentNullException(nameof(additionalFields));

			IList<KeyValuePair<string, string>> formFields = GetCommonFormFieldsValues();
			formFields.Add(new KeyValuePair<string, string>("p_request", "PLUGIN=" + actionAjaxIdentifier));
			foreach (KeyValuePair<string, string> additionalField in additionalFields)
			{
				formFields.Add(additionalField);
			}

			Uri wwv_flowAjaxUri = new Uri(portalUri, "wwv_flow.ajax");
			Task<HttpResponseMessage> postTask = PostUrlEncodedFormToUri(httpClient, wwv_flowAjaxUri, formFields);
			return postTask;
		}

		/// <summary>
		/// Вяртае значэнні для p_flow_id, p_flow_step_id, p_instance, p_debug
		/// </summary>
		public IList<KeyValuePair<string, string>> GetCommonFormFieldsValues()
		{
			List<KeyValuePair<string, string>> formFields = new List<KeyValuePair<string, string>>();
			formFields.Add(new KeyValuePair<string, string>("p_flow_id", oracleApexAppId));
			formFields.Add(new KeyValuePair<string, string>("p_flow_step_id", oracleApexPageId));
			formFields.Add(new KeyValuePair<string, string>("p_instance", oracleApexSessionId));
			formFields.Add(new KeyValuePair<string, string>("p_debug", (oracleApexDebug == "NO") ? "" : oracleApexDebug));

			return formFields;
		}

		public Task<string> Navigate(HttpClient httpClient)
		{
			if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));

			Uri pageUri = BuidPageUri();

			Task<string> res = httpClient.GetStringAsync(pageUri);
			return res;
		}

		protected virtual string NavigateAndParse(HttpClient httpClient)
		{
			string html = Navigate(httpClient).Result;

			Parse(html);

			return html;
		}

		protected virtual void Parse(string html)
		{
			if (html == null) throw new ArgumentNullException(nameof(html));

			const string pPageItemsProtectedPrefix = "<input type=\"hidden\" id=\"pPageItemsProtected\" value=\"";
			this.pPageItemsProtected = TextUtils.GetTextBetweenPrefixAndPostfix(html, pPageItemsProtectedPrefix, "\"");

			const string pPageItemsRowVersionPrefix = "<input type=\"hidden\" id=\"pPageItemsRowVersion\" value=\"";
			this.pPageItemsRowVersion = TextUtils.GetTextBetweenPrefixAndPostfix(html, pPageItemsRowVersionPrefix, "\"");

			const string pPageSubmissionIdPrefix = "<input type=\"hidden\" name=\"p_page_submission_id\" value=\"";
			this.pPageSubmissionId = TextUtils.GetTextBetweenPrefixAndPostfix(html, pPageSubmissionIdPrefix, "\"");
		}

		private static Task<HttpResponseMessage> PostUrlEncodedFormToUri(
			HttpClient httpClient,
			Uri targetUri,
			IList<KeyValuePair<string, string>> formFields)
		{

			#pragma warning disable CA2000
			FormUrlEncodedContent form = null;
			try
			{
				form = new FormUrlEncodedContent(formFields);
				form.Headers.Add("X-Requested-With", "XMLHttpRequest");

				Task<HttpResponseMessage> postTask = httpClient.PostAsync(targetUri, form);
				form = null;

				return postTask;
			}
			finally
			{
				#pragma warning disable CA1508 //нейкі баг аналізатара. form != null калі здарыўся Exception
				if (form != null)
				{
					form.Dispose();
				}
				#pragma warning restore CA1508
			}
			#pragma warning restore CA2000
		}

		public Task<HttpResponseMessage> Submit(HttpClient httpClient,
			#pragma warning disable CA1707
			string p_request, string p_reload_on_submit, string p_json
			#pragma warning restore CA1707
			)
		{
			if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
			if (p_request == null) throw new ArgumentNullException(nameof(p_request));
			if (p_reload_on_submit == null) throw new ArgumentNullException(nameof(p_reload_on_submit));
			if (p_json == null) throw new ArgumentNullException(nameof(p_json));

			//Артыкул, які коратка апісвае апрацоўку старонак у Oracle APEX 18.1
			//https://docs.oracle.com/database/apex-18.1/HTMDB/how-does-page-processing-and-page-rendering-work.htm#HTMDB03003
			IList<KeyValuePair<string, string>> formFields = GetCommonFormFieldsValues();
			formFields.Add(new KeyValuePair<string, string>("p_request", p_request));
			formFields.Add(new KeyValuePair<string, string>("p_reload_on_submit", p_reload_on_submit));
			formFields.Add(new KeyValuePair<string, string>("p_page_submission_id", pPageSubmissionId));
			formFields.Add(new KeyValuePair<string, string>("p_json", p_json));

			Uri wwv_flowAcceptUri = new Uri(portalUri, "wwv_flow.accept");
			Task<HttpResponseMessage> postTask = PostUrlEncodedFormToUri(httpClient, wwv_flowAcceptUri, formFields);
			return postTask;
		}
	}
}