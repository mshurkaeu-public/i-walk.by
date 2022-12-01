using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace IWalkBy.EdsPortal.RestfulWebService
{
	public class WebService
	{
		private const string basePortalUrl = "https://disp.it-minsk.by/app/eds/portal/";

		private string tokenUsernamePasswordUriParameters;

		private readonly Lazy<HttpClient> lazyHttpClient = new Lazy<HttpClient>(
			() =>
			{
				HttpClient res = new HttpClient();
				HttpRequestHeaders defaultRequestHeaders = res.DefaultRequestHeaders;
				defaultRequestHeaders.Add("Accept", "application/json");
				defaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
				return res;
			});
		private HttpClient httpClient
		{
			get
			{
				return lazyHttpClient.Value;
			}
		}

		public WebService(string username, string password)
		{
			tokenUsernamePasswordUriParameters =
				"token=7D3991F2B11022AFE05347C81EACB6FE" +
				"&username=" + Uri.EscapeDataString(username) +
				"&pass=" + Uri.EscapeDataString(password);
		}

		public void DeleteNotification(Notification notification)
		{
			if (notification == null)
			{
				throw new ArgumentNullException(nameof(notification));
			}

			Uri uri = new Uri(
				basePortalUrl +
				"ntf/delete?" +
				tokenUsernamePasswordUriParameters +
				"&nid=" + Uri.EscapeDataString(notification.Id.ToString(CultureInfo.InvariantCulture)));
			HttpResponseMessage response = httpClient.PostAsync(uri, null).Result;
			if (response.StatusCode != HttpStatusCode.OK)
			{
				throw new NotImplementedException();
			}

			string json = response.Content.ReadAsStringAsync().Result;
			if (json != "{\"result_code\":\"RESULT_OK\"}")
			{
				throw new NotImplementedException();
			}
		}

		public RequestInfo[] GetAllRequests()
		{
			Uri uri = new Uri(
				basePortalUrl +
				"request/get_x_v2?" +
				tokenUsernamePasswordUriParameters);
			string json = httpClient.GetStringAsync(uri).Result;

			JObject jObject = JObject.Parse(json);
			JArray items = (JArray)jObject["items"];
			RequestInfo[] res = new RequestInfo[items.Count];
			int i = 0;
			foreach(JObject item in items)
			{
				res[i] = new RequestInfo(item);
				i++;
			}

			return res;
		}

		public Notification[] GetNotifications()
		{
			Uri uri = new Uri(
				basePortalUrl +
				"ntf/get?" +
				tokenUsernamePasswordUriParameters);
			string json = httpClient.GetStringAsync(uri).Result;

			JObject jObject = JObject.Parse(json);
			JArray items = (JArray)jObject["items"];
			Notification[] res = new Notification[items.Count];
			int i = 0;
			foreach(JObject item in items)
			{
				res[i] = new Notification(item);
				i++;
			}

			return res;
		}
	}
}