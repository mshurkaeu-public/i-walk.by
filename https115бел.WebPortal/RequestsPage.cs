using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace IWalkBy.https115бел.WebPortal
{
	public class RequestsPage: OracleApexPage
	{
		private string editRequestDialogChecksum;
		private MultiPageReportWidget searchResultsWidget;

		public RequestsPage(Uri portalUri, string oracleApexAppId, string oracleApexSessionId):
			//"8" == "REQUESTS"
			base(portalUri, oracleApexAppId, "8", oracleApexSessionId)
		{
			this.oracleApexClearCache = "8";
		}

		private static JArray BuildJArrayFromRequestStatusCriteria(RequestStatusCriterion criteria)
		{
			JArray res = new JArray();
			if (criteria == RequestStatusCriterion.None)
			{
				return res;
			}

			if ((criteria & RequestStatusCriterion.InWork) == RequestStatusCriterion.InWork)
			{
				res.Add("10:20:30:40");
			}
			else
			{
				if ((criteria & RequestStatusCriterion.NewRequest) == RequestStatusCriterion.NewRequest)
				{
					res.Add("10");
				}
				if ((criteria & RequestStatusCriterion.PerformerIsSet) == RequestStatusCriterion.PerformerIsSet)
				{
					res.Add("20");
				}
				if ((criteria & RequestStatusCriterion.SurveyConducted) == RequestStatusCriterion.SurveyConducted)
				{
					res.Add("30");
				}
				if ((criteria & RequestStatusCriterion.Unclear4) == RequestStatusCriterion.Unclear4)
				{
					res.Add("40");
				}
			}

			if ((criteria & RequestStatusCriterion.OnReview) == RequestStatusCriterion.OnReview)
			{
				res.Add("-20");
			}
			if ((criteria & RequestStatusCriterion.OnControl) == RequestStatusCriterion.OnControl)
			{
				res.Add("35");
			}
			if ((criteria & RequestStatusCriterion.Rejected) == RequestStatusCriterion.Rejected)
			{
				res.Add("-40");
			}
			if ((criteria & RequestStatusCriterion.Closed) == RequestStatusCriterion.Closed)
			{
				res.Add("50");
			}

			return res;
		}

		public void EditRequestDescription(HttpClient httpClient, string requestId, string newDescription)
		{
			if (editRequestDialogChecksum == null)
			{
				PaginateSearch(httpClient, RequestStatusCriterion.OnReview, 1, 1, 1);
			}

			EditRequestDialog editRequestDialog = new EditRequestDialog(
				portalUri,
				oracleApexAppId,
				oracleApexSessionId,
				editRequestDialogChecksum,
				requestId);
			editRequestDialog.EditRequestDescription(httpClient, newDescription);
		}

		public FoundRequestInfo[] PaginateSearch(HttpClient httpClient, RequestStatusCriterion criteria, int firstRowNumber, int maxRows, int rowsFetched)
		{
			return PaginateSearch(httpClient, criteria, "", "", firstRowNumber, maxRows, rowsFetched);
		}

		public FoundRequestInfo[] PaginateSearch(
			HttpClient httpClient,
			RequestStatusCriterion criteria,
			string dateFrom,
			string dateTo,
			int firstRowNumber, int maxRows, int rowsFetched)
		{
			if (searchResultsWidget == null)
			{
				NavigateAndParse(httpClient);
			}

			JArray statuses = BuildJArrayFromRequestStatusCriteria(criteria);
			string json = BuildJsonToPost(
				new KeyValuePair<string, object>("P8_STATUS", statuses),
				new KeyValuePair<string, object>("P8_DATE_FROM", dateFrom),
				new KeyValuePair<string, object>("P8_DATE_TO", dateTo)
			);

			HtmlNodeCollection nodeCollection = searchResultsWidget.Paginate(
				httpClient,
				firstRowNumber, maxRows, rowsFetched,
				json,
				"div[@class='t-SearchResults ']" +
				"/ul[@class='t-SearchResults-list']" +
				"/div[@class='problem_main_box']");
			if (nodeCollection == null)
			{
				return Array.Empty<FoundRequestInfo>();
			}

			int nodesCount = nodeCollection.Count;

			if (
				(criteria & RequestStatusCriterion.OnReview) == RequestStatusCriterion.OnReview &&
				(editRequestDialogChecksum == null) &&
				(nodesCount > 0))
			{
				string html = nodeCollection[0].OwnerDocument.DocumentNode.OuterHtml;
				TryToSetEditRequestDialogChecksum(html);
			}

			FoundRequestInfo[] res = new FoundRequestInfo[nodesCount];
			for (int i=0; i<nodesCount; i++)
			{
				res[i] = new FoundRequestInfo(nodeCollection[i]);
			}

			return res;
		}

		protected override void Parse(string html)
		{
			if (html == null) throw new ArgumentNullException(nameof(html));

			base.Parse(html);

			searchResultsWidget = new MultiPageReportWidget("46446660640596495", this, html);

			TryToSetEditRequestDialogChecksum(html);
		}

		public string ResetSearch(HttpClient httpClient, RequestStatusCriterion criteria)
		{
			return ResetSearch(httpClient, criteria, "", "");
		}

		public string ResetSearch(HttpClient httpClient, RequestStatusCriterion criteria, string dateFrom, string dateTo)
		{
			if (searchResultsWidget == null)
			{
				NavigateAndParse(httpClient);
			}

			JArray statuses = BuildJArrayFromRequestStatusCriteria(criteria);
			string json = BuildJsonToPost(
				new KeyValuePair<string, object>("P8_STATUS", statuses),
				new KeyValuePair<string, object>("P8_DATE_FROM", dateFrom),
				new KeyValuePair<string, object>("P8_DATE_TO", dateTo)
			);
			HttpResponseMessage httpResponseMessage = ExecuteNativeAction(httpClient, searchResultsWidget.AjaxIdentifier,
				new KeyValuePair<string, string>("p_widget_action", "reset"),
				new KeyValuePair<string, string>("x01", searchResultsWidget.ReportId),
				new KeyValuePair<string, string>("p_json", json)
			).Result;
			string res = httpResponseMessage.Content.ReadAsStringAsync().Result;
			return res;
		}

		private void TryToSetEditRequestDialogChecksum(string html)
		{
			string editRequestDialogChecksumPrefix = "<a href=\"javascript&#x3A;apex.navigation.dialog&#x28;&#x27;f&#x3F;p&#x3D;" + oracleApexAppId + "&#x3A;31&#x3A;" + oracleApexSessionId + "&#x3A;&#x3A;NO&#x3A;RP,31&#x3A;P31_ID_REQUEST&#x3A;";
			if (html.Contains(editRequestDialogChecksumPrefix, StringComparison.Ordinal))
			{
				const string editRequestDialogChecksumPostfix = ",&#x7B;title&#x3A;&#x27;&#x5C;";
				string tmp = TextUtils.GetTextBetweenPrefixAndPostfix(html, editRequestDialogChecksumPrefix, editRequestDialogChecksumPostfix);
				editRequestDialogChecksum = TextUtils.GetTextBetweenPrefixAndPostfix(tmp, "u0026p_dialog_cs&#x3D;", "&#x27;");
				Console.WriteLine("TryToSetEditRequestDialogChecksum:" + editRequestDialogChecksum);
			}
		}
	}
}