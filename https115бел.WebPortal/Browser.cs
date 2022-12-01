using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;

namespace IWalkBy.https115бел.WebPortal
{
	/// <summary>
	/// A web-browser, optimized to perform repeated tasks on web-site https://115.бел
	/// The web-site is implemeted with Oracle APEX version 18.1.0.00.45.
	/// </summary>
	public class Browser: IDisposable
	{
		private bool isDisposed;

		private bool isInitialized;

		private readonly Uri topLevelUri;
		private readonly Uri portalUri;

		/// <summary>
		/// Id of the Oracle APEX application 
		/// </summary>
		private string oracleApexAppId;

		/// <summary>
		/// Checksum, which is passed as a parameter to Oracle APEX dialog. Is new on each new visit to a page.
		/// </summary>
		private string oracleApexDialogChecksum;

		/// <summary>
		/// Id of a session with Oracle APEX application
		/// </summary>
		private string oracleApexSessionId;

		private string currentUsername;

		private HttpClient httpClient;

		HomePage homePage;
		RequestsPage requestsPage;

		/// <summary>
		/// A web-browser for 115.бел web-site. Allows to automate some interactions with the web-site.
		/// 
		/// Some of the operations may require Login.
		/// </summary>
		public Browser()
		{
			topLevelUri = new Uri("https://115.xn--90ais/");//https://115.бел
			portalUri = new Uri(topLevelUri, "portal/");
		}

		#region IDisposable implementation
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (isDisposed) return;

			if (disposing)
			{
				httpClient.Dispose();
			}

			isDisposed = true;
		}
		#endregion

		private void AssertLoginHasHappend()
		{
			if (currentUsername == null)
			{
				throw new NotImplementedException("Не ведаю як працягваць працу таму што ўваход у 115.бел не быў выкананы.");
			}
		}

		public void CreateRequest(Request request)
		{
			AssertLoginHasHappend();

			if (homePage == null)
			{
				homePage = new HomePage(portalUri, oracleApexAppId, oracleApexSessionId);
			}
			homePage.CreateRequest(httpClient, request);
		}

		public void EditRequestDescription(string requestId, string newDescription)
		{
			AssertLoginHasHappend();

			if (requestsPage == null)
			{
				requestsPage = new RequestsPage(portalUri, oracleApexAppId, oracleApexSessionId);
			}
			requestsPage.EditRequestDescription(httpClient, requestId, newDescription);
		}

		public byte[] GetPhotoBytes(Uri photoUri)
		{
			byte[] res = httpClient.GetByteArrayAsync(photoUri).Result;
			return res;
		}

		public string GetPhotoName(Uri photoUri)
		{
			HttpResponseMessage httpResponseMessage = httpClient.GetAsync(
				photoUri,
				HttpCompletionOption.ResponseHeadersRead).Result;

			HttpContentHeaders contentHeaders = httpResponseMessage.Content.Headers;
			if (contentHeaders.ContentDisposition != null)
			{
				throw new NotImplementedException("З'явілася падтрымка загалоўка Content-Disposition. Падтрэбна змяніць логіку праграмы.");
			}

			int counter = 0;
			foreach(string val in contentHeaders.GetValues("Content-Disposition"))
			{
				counter++;

				//сервер вяртае загаловак віда
				//filename="15833323_150922_123505.jpg"; filename*=UTF-8''15833323_150922_123505.jpg
				//ContentDispositionHeaderValue.Parse не можа разабраць яго
				if (!val.StartsWith("filename=", StringComparison.Ordinal))
				{
					throw new NotImplementedException("Фармат адказу з сервера змяніўся. Патрэбна змяніць код.");
				}

				if (counter > 1)
				{
					throw new NotImplementedException("Фармат адказу з сервера змяніўся. Патрэбна змяніць код.");
				}

				//паспрабуем прымяніць правілы, апісаные https://datatracker.ietf.org/doc/rfc6266/
				//If the disposition type matches "attachment" (case-insensitively),
				//this indicates that the recipient should prompt the user to save the
				//response locally, rather than process it normally (as per its media
				//type).
				//
				//On the other hand, if it matches "inline" (case-insensitively), this
				//implies default processing.  Therefore, the disposition type "inline"
				//is only useful when it is augmented with additional parameters, such
				//as the filename (see below).

				ContentDispositionHeaderValue contentDisposition = ContentDispositionHeaderValue.Parse("inline; " + val);
				return contentDisposition.FileNameStar!;
			}
			throw new NotImplementedException("Не ведаю, што рабіць, калі загаловак Content-Disposition адсутнічае.");
		}

		public Request GetRequest(string requestId)
		{
			if (requestId == null) throw new ArgumentNullException(nameof(requestId));

			AssertLoginHasHappend();

			ViewRequestPage viewRequestPage = new ViewRequestPage(portalUri, oracleApexAppId, oracleApexSessionId, requestId);
			return viewRequestPage.GetRequest(httpClient);
		}

		private void Init()
		{
			if (isInitialized) return;

			#pragma warning disable CA2000
			//Або ведаю як паправіць, або баг валідатара.
			HttpClientHandler httpClientHandler = null;
			try
			{
				httpClientHandler = new HttpClientHandler();
				httpClientHandler.CookieContainer = new CookieContainer();
				httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
				httpClientHandler.CheckCertificateRevocationList = true;

				this.httpClient = new HttpClient(httpClientHandler);
				//httpClient.Timeout = new TimeSpan(0, 5, 0);

				httpClientHandler.CheckCertificateRevocationList = false;
				httpClientHandler = null;
			}
			finally
			{
				#pragma warning disable CA1508 //нейкі баг аналізатара. httpClientHandler != null калі здарыўся Exception
				if (httpClientHandler != null)
				{
					httpClientHandler.Dispose();
					httpClientHandler = null;
				}
				#pragma warning restore CA1508
			}
			#pragma warning restore CA2000

			//https://source.chromium.org/chromium/chromium/src/+/main:content/common/user_agent.cc
			HttpRequestHeaders defaultRequestHeaders = httpClient.DefaultRequestHeaders;
			defaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
			defaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
			defaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36");

			HttpResponseMessage responseMessage = httpClient.GetAsync(topLevelUri).Result;

			#region set OracleApexAppId
			string query = responseMessage.RequestMessage.RequestUri.Query;//expected to be ?p=10901:1
			if (query != "?p=10901:1")
			{
				throw new NotImplementedException("The logic for different types of URI is not implemented.");
			}
			oracleApexAppId = "10901";
			#endregion

			string html = responseMessage.Content.ReadAsStringAsync().Result;

			#region check APEX version
			//it's also possile to check values in
			//https://115.xn--90ais/i/apex_version.txt and/or
			//https://115.xn--90ais/i/apex_version.js
			if (!html.Contains("<link rel=\"stylesheet\" href=\"/i/app_ui/css/Core.min.css?v=18.1.0.00.45\" type=\"text/css\" />", StringComparison.Ordinal))
			{
				throw new NotImplementedException("Support of APEX version different from 18.1.0.00.45 is not implemented");
			}
			#endregion

			const string p_instancePrefix = "<input type=\"hidden\" name=\"p_instance\" value=\"";
			oracleApexSessionId = TextUtils.GetTextBetweenPrefixAndPostfix(html, p_instancePrefix, "\"");

			string p_dialog_csPrefix = "javascript:apex.navigation.dialog('f?p=" + oracleApexAppId + ":3:" + oracleApexSessionId + "::NO:3::\\u0026p_dialog_cs=";
			oracleApexDialogChecksum = TextUtils.GetTextBetweenPrefixAndPostfix(html, p_dialog_csPrefix, "'");

			isInitialized = true;
		}

		public void Login(string username, string password)
		{
			Init();

			if (currentUsername != null)
			{
				throw new NotImplementedException("Behavior for a case when login was called already is not defined yet.");
			}

			LoginDialog loginDialog = new LoginDialog(portalUri, oracleApexAppId, oracleApexSessionId, oracleApexDialogChecksum);
			loginDialog.Login(httpClient, username, password);

			currentUsername = username;
		}
		
		public void Logout()
		{
			AssertLoginHasHappend();
			
			ProfilePage userProfilePage = new ProfilePage(portalUri, oracleApexAppId, oracleApexSessionId);
			userProfilePage.Logout(httpClient);

			currentUsername = null;
		}

		public FoundRequestInfo[] PaginateSearch(RequestStatusCriterion criteria, int firstRowNumber, int maxRows, int rowsFetched)
		{
			return PaginateSearch(criteria, "", "", firstRowNumber, maxRows, rowsFetched);
		}

		public FoundRequestInfo[] PaginateSearch(
			RequestStatusCriterion criteria,
			string dateFrom,
			string dateTo,
			int firstRowNumber, int maxRows, int rowsFetched)
		{
			if (requestsPage == null)
			{
				requestsPage = new RequestsPage(portalUri, oracleApexAppId, oracleApexSessionId);
			}
			return requestsPage.PaginateSearch(httpClient, criteria, dateFrom, dateTo, firstRowNumber, maxRows, rowsFetched);
		}

		public string ResetSearch(RequestStatusCriterion criteria)
		{
			return ResetSearch(criteria, "", "");
		}

		public string ResetSearch(RequestStatusCriterion criteria, string dateFrom, string dateTo)
		{
			if (requestsPage == null)
			{
				requestsPage = new RequestsPage(portalUri, oracleApexAppId, oracleApexSessionId);
			}
			return requestsPage.ResetSearch(httpClient, criteria, dateFrom, dateTo);
		}

		public bool TryGetRequestByNumber(string requestNumber, out FoundRequestInfo request)
		{
			RequestNumber rNumber = new RequestNumber(requestNumber);
			return TryGetRequestByNumber(rNumber, out request);
		}

		public bool TryGetRequestByNumber(RequestNumber requestNumber, out FoundRequestInfo request)
		{
			if (requestNumber == null) throw new ArgumentNullException(nameof(requestNumber));

			AssertLoginHasHappend();

			DateOnly prevDay = requestNumber.Date.AddDays(-1);
			string dateFrom = prevDay.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
			string dateTo = requestNumber.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

			const int officialDailyRequestsLimit = 20;
			int pageSize = officialDailyRequestsLimit * 2;//theoretically, 2 is sufficient to get result with the 1st request 
			string numberToFind = requestNumber.AsString;

			int firstRowNumber = 1;
			bool doContinue = true;
			while (doContinue)
			{
				FoundRequestInfo[] foundRequests;
				try
				{
					foundRequests = PaginateSearch(RequestStatusCriterion.None, dateFrom, dateTo, firstRowNumber, pageSize, pageSize);
				}
				catch (LooksLikeOracleApexReportBugException)
				{
					if (pageSize > 1)
					{
						pageSize /= 2;
						continue;
					}
					else
					{
						throw;
					}
				}
				//Console.WriteLine("Found " + foundRequests.Length + " requests");
				int count = 0;
				foreach(FoundRequestInfo requestCandidate in foundRequests)
				{
					count++;
					if (requestCandidate.Number == numberToFind)
					{
						request = requestCandidate;
						return true;
					}
				}
				if (count == pageSize)
				{
					doContinue = true;
					firstRowNumber += pageSize;
				}
				else
				{
					doContinue = false;
				}
			}

			request = null;
			return false;
		}
	}
}