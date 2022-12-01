using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;

namespace IWalkBy.https115бел.WebPortal
{
	public class ViewRequestPage: OracleApexPage
	{
		private Request request;
		private MultiPageReportWidget requestHistoryWidget;

		public ViewRequestPage(Uri portalUri,
		                       string oracleApexAppId,
		                       string oracleApexSessionId,
							   string requestId):
			base(portalUri, oracleApexAppId, "10", oracleApexSessionId)
		{
			this.oracleApexClearCache = "10";
			this.oracleApexItemNames = "P10_ID_REQUEST";
			this.oracleApexItemValues = requestId;
		}

		private static IList<Uri> BuildListOfPhotoUris(string html, string photosReportId)
		{
			string photosReportPrefix = "<div id=\"report_" + photosReportId + "_catch\">";
			if (html.Contains(photosReportPrefix, StringComparison.Ordinal))
			{
				const string photosReportPostfix = "<table class=\"t-Report-pagination\" role=\"presentation\"></table>";
				string photosReportHtml = TextUtils.GetTextBetweenPrefixAndPostfix(html, photosReportPrefix, photosReportPostfix);
				HtmlDocument photosReportHtmlDocument = new HtmlDocument();
				photosReportHtmlDocument.LoadHtml(photosReportHtml);
				HtmlNodeCollection photoNodes = photosReportHtmlDocument.DocumentNode.SelectNodes(
					"ul[@id='R" + photosReportId + "_cards']" +
					"/li[@class='t-Cards-item #CARD_MODIFIERS#']");

				List<Uri> res = new List<Uri>();
				foreach (HtmlNode photoNode in photoNodes)
				{
					HtmlNode aNode = photoNode.SelectSingleNode(
						"div[@class='t-Cards']" +
						"/div[@class='t-Card-wrap']" +
						"/div[@class='t-Card-small bg-cover']" +
						"/a[@class='highslide']");
					string href = aNode.GetAttributeValue("href", null);
					res.Add(new Uri(href));
				}
				return res;
			}
			else
			{
				return null;
			}
		}

		private static string GetFieldValue(HtmlDocument htmlDocument, string fieldId)
		{
			HtmlNode htmlNode = htmlDocument.GetElementbyId(fieldId);
			string res = htmlNode.GetAttributeValue("value", null);
			res = HtmlEntity.DeEntitize(res);
			return res;
		}

		public Request GetRequest(HttpClient httpClient)
		{
			string html = NavigateAndParse(httpClient);

			const string dataForP10_ID_REQUESTPrefix = "<input type=\"hidden\" data-for=\"P10_ID_REQUEST\" value=\"";
			string dataForP10_ID_REQUEST = TextUtils.GetTextBetweenPrefixAndPostfix(html, dataForP10_ID_REQUESTPrefix, "\"");
			string json = BuildJsonToPost(
				new KeyValuePair<string, object>("P10_ID_REQUEST", new ValueWithChecksum(request.Id, dataForP10_ID_REQUEST))
			);

			const int pageSize = 100;
			bool doRequestNextPage = true;
			int startItemPosition = 1;
			while (doRequestNextPage)
			{
				doRequestNextPage = false;
				HtmlNodeCollection historyNodesCollection = requestHistoryWidget.Paginate(
					httpClient,
					startItemPosition, pageSize, pageSize,
					json,
					"ul[@id='R617836230648673704_report']" +
					"/li[@class='t-Comments-item #COMMENT_MODIFIERS#']");
				if (historyNodesCollection == null)
				{
					break;
				}

				if (request.History == null)
				{
					request.History = new List<HistoryEntry>();
				}

				foreach(HtmlNode historyNode in historyNodesCollection)
				{
					HtmlNode commentsBody = historyNode.SelectSingleNode("div[@class='t-Comments-body a-MediaBlock-content']");
					HtmlNode infoNode = commentsBody.SelectSingleNode("div[@class='t-Comments-info']");
					HtmlNode whoNode = infoNode.SelectSingleNode("text()");
					HtmlNode whenNode = infoNode.SelectSingleNode("span[@class='t-Comments-date']");
					//заўсёды пусты HtmlNode whatNode = infoNode.SelectSingleNode("span[@class='t-Comments-actions']");
					HtmlNode descriptionNode = commentsBody.SelectSingleNode("div[@class='t-Comments-comment']");

					string who = whoNode.InnerText;
					who = who.Substring(0, who.IndexOf("&middot;", StringComparison.Ordinal));
					who = HtmlEntity.DeEntitize(who);
					who = who.Trim();

					string whenStr = whenNode.InnerText.Trim();
					DateTime when = DateTime.ParseExact(whenStr, "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

					string description = descriptionNode.InnerText.Trim();
					description = HtmlEntity.DeEntitize(description);

					HistoryEntry historyEntry = new HistoryEntry(who, when, description);

					request.History.Add(historyEntry);
				}

				doRequestNextPage = (historyNodesCollection.Count == pageSize);
				startItemPosition += pageSize;
			}

			return request;
		}

		protected override void Parse(string html)
		{
			if (html == null) throw new ArgumentNullException(nameof(html));

			if (html.Contains("<h3>ORA-01403: данные не найдены</h3>", StringComparison.Ordinal))
			{
				throw new RequestNotFoundException();
			}

			base.Parse(html);

			#region прачытаць значэнні палёў заяўкі на форме 
			const string fieldsPrefix = "<div class=\"current_problem_moddate\">";
			const string fieldsPostfix = "<div class=\"t-Region-buttons t-Region-buttons--bottom\">";
			string fieldsHtml = TextUtils.GetTextBetweenPrefixAndPostfix(html, fieldsPrefix, fieldsPostfix);
			/*прыклад:
				<input type="hidden" id="P10_RATING_ST" name="P10_RATING_ST" value="1">
				<input type="hidden" data-for="P10_RATING_ST" value="RFIlQdUoDBmsAvZXhEqSurYuHKF4aEu0X8_9ecXUVzHtIEYEbmGCnk9pi9zwC6JUEETG25CWDRIs2YEzg8OmIg">
				<input type="hidden" id="P10_STATUS_CODE" name="P10_STATUS_CODE" value="50">
				<input type="hidden" data-for="P10_STATUS_CODE" value="nw64uanxt-CoSedtNHBlotXBqLSUrgCLG0q27xAFi7RSWlwKHAunJiW9mxXdtVkxd7K5KpOoUSSz0Y6zP-O2hg">
				<input type="hidden" id="P10_ID_REQUEST" name="P10_ID_REQUEST" value="16374907">
				<input type="hidden" data-for="P10_ID_REQUEST" value="uxay6lRgEx1I-Ep2wsPG7YWg6EC8hJawjiM6qKf5bqz2zc1nUj2b2mfEsv3iXjwFaI-hxkFQs5YBVQoirOgArw">
				<input type="hidden" id="P10_FAKE_ID" name="P10_FAKE_ID" value="Заявка №1248.6.131022">
				<input type="hidden" data-for="P10_FAKE_ID" value="BM-Q00m7fcl4iYQYQoO5OqDe57UixOdoGBI63RU-Uzdi32F1gr40Q5evSS-YtbHs3x4IANLIDJXYeZ02K-2-AQ">
				<input type="hidden" id="P10_LAT" name="P10_LAT" value="52,152039">
				<input type="hidden" data-for="P10_LAT" value="6Xl1E3W9iYc887t5yqE4qAcPlQTxIBQ_A-6XwF_6TJdLNyzEoZ_8a9gvgMgVRi3lUGMT9lNzUaLaDMl-WcCk9A">
				<input type="hidden" id="P10_CREATE_DATE" name="P10_CREATE_DATE" value="12.10.2022 18:24">
				<input type="hidden" data-for="P10_CREATE_DATE" value="sY_OX_F5imLp9q2M5Cb1ls0S0jyZOyZXrnGJrp85ruzFETipO3cBWeekV9UGRduldjDG-3s9-H4VsToO5euWWA">
				<input type="hidden" id="P10_SUBJECT" name="P10_SUBJECT" value="Ямы&#x2F;выступы на проезжей части&#x2F;тротуаре">
				<input type="hidden" data-for="P10_SUBJECT" value="GwdQryzX8va3Yo-d8BuYSIfzTCsBSRfFojQvrTQ7f01lJzvfilOmE_XNiZ_Ro-vivqlTCdaKSQi2SaUmWpdEHQ">
				<input type="hidden" id="P10_HOURS_LEFT" name="P10_HOURS_LEFT" value="Выполнено 14.10.2022">
				<input type="hidden" data-for="P10_HOURS_LEFT" value="FkniFRdIQr0AI6dvxGIImH_lsNBR74MYDAPUlt78YX8nLnBLQFSqk4KPxbjkcEbR-calZNoe-whww1wr5KAUFw">
				<input type="hidden" id="P10_STATUS" name="P10_STATUS" value="Заявка закрыта">
				<input type="hidden" data-for="P10_STATUS" value="IvTl1jRN4QQ6P1KVqbQIEPr8ywn1dfLCaa45z502hq3IzBZIo7bdA-8lgO4wled_Q3J6G0wbX_aphbutZH3qqA">
				<input type="hidden" id="P10_DESC" name="P10_DESC" value="Ямы на праезнай частцы.

				Глядзі фота і кропку на карце. Калі ласка, пасля выканання заяўкі зрабіце фота з такіх самых ракурсаў. Дзякуй.

				{
					&quot;c&quot;: &quot;52.152039,25.548084&quot;,
					&quot;Google карты&quot;: &quot;https:&#x2F;&#x2F;www.google.com&#x2F;maps&#x2F;search&#x2F;?api=1&amp;query=52.152039,25.548084&quot;,
					&quot;Яндекс карты&quot;: &quot;https:&#x2F;&#x2F;yandex.ru&#x2F;maps&#x2F;?pt=25.548084,52.152039&amp;z=17&quot;,
					&quot;t&quot;: &quot;6344667735464e2f23d8b790&quot;,
					&quot;1&quot;: &quot;16374907&quot;
				}">
				<input type="hidden" data-for="P10_DESC" value="nykKKsI289oZ_vUHOaq97JxU7oMiadNvC9FIkRGNmAuXQupTNyKUC5WixoJCAmEE60PIqLAy9-RMb93ypbtTCQ">
				<input type="hidden" id="P10_MODIFY_DATE" name="P10_MODIFY_DATE" value="14.10.2022 14:25">
				<input type="hidden" data-for="P10_MODIFY_DATE" value="Cl48ArWCdD7KXRFRGO4Wz1UJDh1M7JZU_Ijz0NOR_qDUeInKxGbCv8fxJqWI39k2uRgq3sx0MFAPWZUjDzgtnQ">
				<input type="hidden" id="P10_LNG" name="P10_LNG" value="25,548084">
				<input type="hidden" data-for="P10_LNG" value="eqSA-pue7pvCepXNSkE8q3L1gbJ5Ca8trt-T-dOQbT_dxVrZTEtbhCdZj_wZ5-iZWlZQntxqqJbJAISMblQd4A">
				<input type="hidden" id="P10_ADDRESS" name="P10_ADDRESS" value="Ивановский район, Иваново, Советская улица, 16">
				<input type="hidden" data-for="P10_ADDRESS" value="0I_1-p_QJ2Jk4AP_HyL4uLkdQVePPRUqSaIGAVaGCvvc5sth5F-eeHbl7VUodb8_VaWyCyKJ6SyjNs39nSFtcw">
				<input type="hidden" id="P10_ZOOM" name="P10_ZOOM" value="16">
				<input type="hidden" data-for="P10_ZOOM" value="py66jErFjaUkT4Kz_ydT--8KaMgFpvRVVRdL94gH5ildqB_Myf9QYzEzx2cQtfslPSHcoSzhT4bZHLuA0IJVVQ">
				<input type="hidden" id="P10_ORG_COMMENT" name="P10_ORG_COMMENT" value="Выполнен ремонт дорожного покрытия.">
				<input type="hidden" data-for="P10_ORG_COMMENT" value="xWSK-idvXNJCUkE-ml5OufaYFib6nCsHxLGeKjYZuEbIlqqYleRc0C4s2mewEF7ipbxa59vEldefqyLxkW0BCQ">
			*/
			HtmlDocument fieldsHtmlDocument = new HtmlDocument();
			fieldsHtmlDocument.LoadHtml(fieldsHtml);

			request = new Request();
			request.StatusCode  = GetFieldValue(fieldsHtmlDocument, "P10_STATUS_CODE");
			request.Id          = GetFieldValue(fieldsHtmlDocument, "P10_ID_REQUEST");
			request.Latitude    = GetFieldValue(fieldsHtmlDocument, "P10_LAT");
			request.CreatedOn   = GetFieldValue(fieldsHtmlDocument, "P10_CREATE_DATE");
			request.Status      = GetFieldValue(fieldsHtmlDocument, "P10_STATUS");
			request.Description = GetFieldValue(fieldsHtmlDocument, "P10_DESC");
			request.ModifiedOn  = GetFieldValue(fieldsHtmlDocument, "P10_MODIFY_DATE");
			request.Longitude   = GetFieldValue(fieldsHtmlDocument, "P10_LNG");
			request.OrganizationComment = GetFieldValue(fieldsHtmlDocument, "P10_ORG_COMMENT");

			const string normalNumberPrefix = "Заявка №";
			string P10_FAKE_ID  = GetFieldValue(fieldsHtmlDocument, "P10_FAKE_ID");
			if (P10_FAKE_ID.StartsWith(normalNumberPrefix, StringComparison.Ordinal)) //калі заяўка адхілена мадэратарам, то гэта "Отклоненный запрос"
			{
				request.Number = P10_FAKE_ID.Substring(normalNumberPrefix.Length);
			}
			#endregion

			requestHistoryWidget = new MultiPageReportWidget("617836230648673704", this, html);

			request.ListOfUserPhotos = BuildListOfPhotoUris(html, "100528386613270216");
			request.ListOfOrganizationPhotos = BuildListOfPhotoUris(html, "117314668153759643");
		}
	}
}