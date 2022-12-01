using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace IWalkBy.https115бел.WebPortal
{
	public class MultiPageReportWidget
	{
		public string AjaxIdentifier { get; private set; }
		public OracleApexPage ParentPage { get; private set; }
		public string ReportId { get; private set; }

		public MultiPageReportWidget(string reportId, OracleApexPage parentPage, string parentPageHtml)
		{
			ReportId = reportId;
			ParentPage = parentPage;

			string widgetAjaxIdentifierPrefix = "(function(){apex.widget.report.init(\"R" + reportId +"\",\"";
			AjaxIdentifier = TextUtils.GetTextBetweenPrefixAndPostfix(parentPageHtml, widgetAjaxIdentifierPrefix, "\"");
		}

		public HtmlNodeCollection Paginate(HttpClient httpClient,
			#pragma warning disable CA1707
			int p_pg_min_row, int p_pg_max_rows, int p_pg_rows_fetched,
			#pragma warning restore CA1707
			string json,
			string xpath)
		{
			HttpResponseMessage httpResponseMessage = ParentPage.ExecuteNativeAction(
				httpClient,
				AjaxIdentifier,
				new KeyValuePair<string, string>("p_widget_action", "paginate"),
				new KeyValuePair<string, string>("p_pg_min_row", p_pg_min_row.ToString(CultureInfo.InvariantCulture)),
				new KeyValuePair<string, string>("p_pg_max_rows", p_pg_max_rows.ToString(CultureInfo.InvariantCulture)),
				new KeyValuePair<string, string>("p_pg_rows_fetched", p_pg_rows_fetched.ToString(CultureInfo.InvariantCulture)),
				new KeyValuePair<string, string>("x01", ReportId),
				new KeyValuePair<string, string>("p_json", json)
			).Result;
			string html = httpResponseMessage.Content.ReadAsStringAsync().Result;

			HtmlDocument htmlDocument = new HtmlDocument();
			htmlDocument.LoadHtml(html);
			HtmlNode rootReportNode = htmlDocument.DocumentNode.SelectSingleNode("/div[@id='report_" + ReportId + "_catch']");
			HtmlNode noDataFoundNode = rootReportNode.SelectSingleNode("span[@class='nodatafound']");
			if (noDataFoundNode != null)
			{
				return null;
			}

			HtmlNodeCollection nodeCollection = rootReportNode.SelectNodes(xpath);
			return nodeCollection;
		}
	}
}