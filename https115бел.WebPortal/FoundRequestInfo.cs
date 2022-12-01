using System;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace IWalkBy.https115бел.WebPortal
{
	public class FoundRequestInfo
	{
		//f?p=10901:10:11790815261385::NO:10:P10_ID_REQUEST:15501020
		Regex correctLinkRegex = new Regex(
			@"^f\?p=\d+:10:\d+::NO:10:P10_ID_REQUEST:(?<id>\d+)$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		public string Id { get; private set; }
		public string Number { get; private set; }
		public string Title { get; private set; }

		public FoundRequestInfo(HtmlNode htmlNode)
		{
			if (htmlNode == null) throw new ArgumentNullException(nameof(htmlNode));

			HtmlNode problem_userNode = htmlNode.SelectSingleNode("section/div[@class='problem_box']/div[@class='problem_user']");
			if (problem_userNode == null) throw new LooksLikeOracleApexReportBugException();

			HtmlNode problem_numNode = problem_userNode.SelectSingleNode("p[@class='problem_p']/a[@class='problem_num']");
			if (problem_numNode == null) throw new LooksLikeOracleApexReportBugException();

			string href1 = problem_numNode.GetAttributeValue("href", null);
			if (href1 == null) throw new LooksLikeOracleApexReportBugException();

			//тэкст у тэгу <a> выглядае адным з наступных чынаў:
			//"Заявка № 2037.2.190822"
			//"На рассмотрении"
			//"Отклонено"
			string problem_numText = problem_numNode.InnerText.Trim();
			const string approvedRequestPrefix = "Заявка № ";
			string numberCandidate = problem_numText.Substring(approvedRequestPrefix.Length);
			if (
				problem_numText != "На рассмотрении" &&
				problem_numText != "Отклонено" &&
				!problem_numText.StartsWith(approvedRequestPrefix, StringComparison.Ordinal) &&
				!RequestNumber.MatchesFormat(numberCandidate))
			{
				throw new LooksLikeOracleApexReportBugException();
			}
			if (problem_numText.StartsWith(approvedRequestPrefix, StringComparison.Ordinal))
			{
				this.Number = numberCandidate;
			}

			HtmlNode aNode = problem_userNode.SelectSingleNode("h5[@class='problem_title']/a");
			if (aNode == null) throw new LooksLikeOracleApexReportBugException();

			string href2 = aNode.GetAttributeValue("href", null);
			if (href2 == null) throw new LooksLikeOracleApexReportBugException();

			string candidateHref;
			if (href1 == href2)
			{
				//амаль дакладна, што гэтыя дадзеныя надзейныя
				candidateHref = href1;
			}
			else
			{
				if (href1.Length > href2.Length)
				{
					//калі дліна першага болей, то можна спадзявацца, што другі сапсаваны (не хапае сымбалаў)
					candidateHref = href1;
				}
				else if (href1.Length < href2.Length)
				{
					candidateHref = href2;
				}
				else
				{
					throw new NotImplementedException("Не ведаю што рабіць");
				}

			}
			Match match = correctLinkRegex.Match(candidateHref);
			if (!match.Success) throw new LooksLikeOracleApexReportBugException();

			this.Id = match.Groups["id"].Value;

			HtmlNode problem_title_textNode = aNode.SelectSingleNode("span[@class='problem_title_text']");
			if (problem_title_textNode == null) throw new LooksLikeOracleApexReportBugException();

			this.Title = problem_title_textNode.InnerText.Trim();
		}
	}
}