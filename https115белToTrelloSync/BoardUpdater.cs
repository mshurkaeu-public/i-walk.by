using IWalkBy.https115бел.WebPortal;
using IWalkBy.Trello;
using Manatee.Trello;
using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;

namespace IWalkBy.https115белToTrelloSync
{
	public class BoardUpdater
	{
		#region канстанты
		private const string CardDescriptionDoesntMatchRequestDescriptionLabelName = "Апісанне карткі значна не супадае з апісаннем у заяўцы ў 115.бел.";
		private const string CommentAboutRequestEventDoesntMatchExpectedValueLabelName = "Каментар пра падзею з гісторыі заяўкі не супадае з тым, што запісана ў 115.бел.";
		private const string InformationalNoteLabelName = "інфармацыйная нататка";
		private const string InvalidCardNameLabelName = "Кепская назва карткі. Не знайсці id з 115.бел.";
		private const string InvalidRequestIdLabelName = "Кепскі 115.бел id. Няма заяўкі з такім id.";
		private const string UpdatedAutomaticallyLabelName = "Абнаўляецца аўтаматычна. Не рэдагаваць уручную!";

		private const string RejectedListName = "Адмова";
		private const string OnReviewListName = "На разглядзе ў мадэратара 115.бел";
		private const string InWorkListName = "Заяўка ў працы";
		private const string ToCheckListName = "Трэба праверыць выкананне";
		#endregion

		public Board Board { get; private set; }

		private Label CardDescriptionDoesntMatchRequestDescriptionLabel;

		private Label CommentAboutRequestEventDoesntMatchExpectedValueLabel;

		private Label InvalidCardNameLabel;

		private Label InvalidRequestIdLabel;

		private Label UpdatedAutomaticallyLabel;

		public IList RejectedList { get; private set; }
		public IList OnReviewList { get; private set; }
		public IList InWorkList { get; private set; }

		public bool SkipRequestIfBelongsToOthers { get; private set; }

		public IList ToCheckList { get; private set; }

		public string TerritoryName { get; private set; }
		private IndexedPointInAreaLocator regionLocator;

		public BoardUpdater(Board board, JObject jObject)
		{
			if (board == null) throw new ArgumentNullException(nameof(board));
			if (jObject == null) throw new ArgumentNullException(nameof(jObject));

			this.Board = board; 
			this.TerritoryName = (string)jObject["назва тэрыторыі"];
			this.SkipRequestIfBelongsToOthers = ("прапусціць" == (string)jObject["калі кропка належыць і іншай тэрыторыі"]);

			//напрыклад "https://www.openstreetmap.org/?mlat=53.94425524943648&mlon=27.461211271581885"
			const string latPrefix = "?mlat=";
			const string lonPrefix = "&mlon=";
			JArray coordsUrls = (JArray)jObject["шматкутнік"];
			bool needAdditionalVertice = ((string)coordsUrls[0] != (string)coordsUrls[coordsUrls.Count-1]);
			int verticesNumber = needAdditionalVertice ? coordsUrls.Count + 1 : coordsUrls.Count;
			Coordinate[] vertices = new Coordinate[verticesNumber];
			int i = 0;
			foreach(string coordsUrl in coordsUrls)
			{
				int latStart = coordsUrl.IndexOf(latPrefix, StringComparison.Ordinal) + latPrefix.Length;
				int latEnd = coordsUrl.IndexOf(lonPrefix, StringComparison.Ordinal);
				int lonStart = coordsUrl.IndexOf(lonPrefix, StringComparison.Ordinal) + lonPrefix.Length;
				int lonEnd = coordsUrl.Length;
				string latStr = coordsUrl.Substring(latStart, latEnd-latStart);
				string lonStr = coordsUrl.Substring(lonStart, lonEnd-lonStart);
				double lat = Double.Parse(latStr, CultureInfo.InvariantCulture);
				double lon = Double.Parse(lonStr, CultureInfo.InvariantCulture);
				vertices[i] = new Coordinate(lon, lat);
				i++;
			}
			if (needAdditionalVertice)
			{
				vertices[verticesNumber-1] = vertices[0];
			}
			GeometryFactory geometryFactory = new GeometryFactory();
			Polygon regionPolygon = geometryFactory.CreatePolygon(vertices);
			regionLocator = new IndexedPointInAreaLocator(regionPolygon);

			InitMyLabels();
			InitMyLists();
		}

		private void ApplyAppropriateLabelToTheCard(Card card, CardUpdateResult cardUpdateResult)
		{
			ILabel labelToApply;
			switch (cardUpdateResult)
			{
				case CardUpdateResult.A115белIdNotFound:
					labelToApply = InvalidCardNameLabel;
					break;
				
				case CardUpdateResult.NameFormatNotRecognized:
					labelToApply = InvalidCardNameLabel;
					break;

				case CardUpdateResult.A115белRequestNotFound:
					labelToApply = InvalidRequestIdLabel;
					break;

				case CardUpdateResult.CardDescriptionDoesntMatchRequestDescription:
					labelToApply = CardDescriptionDoesntMatchRequestDescriptionLabel;
					break;

				case CardUpdateResult.CommentAboutRequestEventDoesntMatchExpectedValue:
					labelToApply = CommentAboutRequestEventDoesntMatchExpectedValueLabel;
					break;

				case CardUpdateResult.Success:
					labelToApply = UpdatedAutomaticallyLabel;
					break;

				default:
					throw new NotImplementedException();
			}

			card.Labels.Add(labelToApply).Wait();
		}

		public void CreateRequiredLabels()
		{
			Dictionary<string, LabelColor?> requiredLabels = new Dictionary<string, LabelColor?>();
			requiredLabels.Add(CardDescriptionDoesntMatchRequestDescriptionLabelName, LabelColor.Red);
			requiredLabels.Add(CommentAboutRequestEventDoesntMatchExpectedValueLabelName, LabelColor.Red);
			requiredLabels.Add(InformationalNoteLabelName, null);
			requiredLabels.Add(InvalidCardNameLabelName, LabelColor.Red);
			requiredLabels.Add(InvalidRequestIdLabelName, LabelColor.Red);
			requiredLabels.Add(UpdatedAutomaticallyLabelName, null);

			Board.Labels.Refresh().Wait();
			HashSet<string> existingLabels = new HashSet<string>();
			foreach(Label existingLabel in Board.Labels)
			{
				string existingLabelName = existingLabel.Name;
				if (!String.IsNullOrEmpty(existingLabelName))
				{
					existingLabels.Add(existingLabelName);
				}
			}

			bool newLabelsWereCreated = false;
			foreach(string requiredLabelName in requiredLabels.Keys)
			{
				if (!existingLabels.Contains(requiredLabelName))
				{
					LabelColor? requiredLabelColor = requiredLabels[requiredLabelName];
					Board.Labels.Add(requiredLabelName, requiredLabelColor).Wait();
					newLabelsWereCreated = true;
				}
			}

			if (newLabelsWereCreated)
			{
				Board.Labels.Refresh(true).Wait();
				InitMyLabels();
			}
		}

		public IList<int> GetAllRequestsIds(ITrelloFactory trelloFactory)
		{
			if (trelloFactory == null) throw new ArgumentNullException(nameof(trelloFactory));

			List<int> res = new List<int>();

			//https://developer.atlassian.com/cloud/trello/rest/api-group-search/#api-search-get
			const string query = $"-label:\"{InformationalNoteLabelName}\"";
			const int cards_limit = 1000;
			int cards_page = -1;

			HttpClient httpClient = HttpClientFactory.GetSingletone();
			bool doSearch = true;
			while (doSearch)
			{
				cards_page++;
				Uri searchUri = new Uri(
					"https://trello.com/1/search?" +
					$"query={Uri.EscapeDataString(query)}&" +
					$"idBoards={Uri.EscapeDataString(Board.Id)}&" +
					$"modelTypes=cards&card_fields=name&cards_limit={cards_limit}&" +
					$"cards_page={cards_page}");
				string json = httpClient.GetStringAsync(searchUri).Result;
				JObject jObject = JObject.Parse(json);
				JArray cards = (JArray)jObject["cards"];

				foreach (JObject card in cards)
				{
					string cardName = (string)card["name"];

					const string prefix = "1:";
					int idStart = cardName.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
					if (idStart < prefix.Length)
					{
						continue;
					}
					const string postfix = " ";
					int idEnd = cardName.IndexOf(postfix, idStart, StringComparison.Ordinal);

					string idStr;
					if (idEnd == -1)
					{
						idStr = cardName.Substring(idStart);
					}
					else
					{
						idStr = cardName.Substring(idStart, idEnd - idStart);
					}
					int id = Int32.Parse(idStr, CultureInfo.InvariantCulture);

					res.Add(id);
				}

				doSearch = (cards.Count == cards_limit);
			}

			return res;
		}

		private void InitMyLabels()
		{
			foreach(Label existingLabel in Board.Labels)
			{
				string existingLabelName = existingLabel.Name;
				switch (existingLabelName)
				{
					case CardDescriptionDoesntMatchRequestDescriptionLabelName:
						this.CardDescriptionDoesntMatchRequestDescriptionLabel = existingLabel;
						break;

					case CommentAboutRequestEventDoesntMatchExpectedValueLabelName:
						this.CommentAboutRequestEventDoesntMatchExpectedValueLabel = existingLabel;
						break;

					case InvalidCardNameLabelName:
						this.InvalidCardNameLabel = existingLabel;
						break;

					case InvalidRequestIdLabelName:
						this.InvalidRequestIdLabel = existingLabel;
						break;

					case UpdatedAutomaticallyLabelName:
						this.UpdatedAutomaticallyLabel = existingLabel;
						break;
				}
			}
		}

		private void InitMyLists()
		{
			foreach(List existingList in Board.Lists)
			{
				string existingListName = existingList.Name;
				switch (existingListName)
				{
					case RejectedListName:
						this.RejectedList = existingList;
						break;

					case OnReviewListName:
						this.OnReviewList = existingList;
						break;

					case InWorkListName:
						this.InWorkList = existingList;
						break;

					case ToCheckListName:
						this.ToCheckList = existingList;
						break;
				}
			}
		}

		public bool PointBelongsToTheRegion(double lat, double lon)
		{
			Coordinate coordinate = new Coordinate(lon, lat);
			Location location = regionLocator.Locate(coordinate);
			bool res = !location.HasFlag(Location.Exterior);
			return res;
		}

		public bool ThereAreCardsWithIssues(ITrelloFactory trelloFactory)
		{
			if (trelloFactory == null) throw new ArgumentNullException(nameof(trelloFactory));

			string[] redLabels = new string[] {
				CardDescriptionDoesntMatchRequestDescriptionLabelName,
				CommentAboutRequestEventDoesntMatchExpectedValueLabelName,
				InvalidCardNameLabelName,
				InvalidRequestIdLabelName
			};
			int maxCardsToReturn = 1;
			foreach (string label in redLabels)
			{
				ISearch search = trelloFactory.Search(
					$"label:\"{label}\"",
					maxCardsToReturn,
					SearchModelType.Cards,
					new IQueryable[]{Board});
				search.Refresh().Wait();
				foreach(Card card in search.Cards)
				{
					return true;
				}
			}
			return false;
		}

		public Dictionary<Card, CardUpdateResult> UpdateManuallyAddedCards(
			ITrelloFactory trelloFactory,
			Browser https115белWebBrowser,
			IEnvironmentOnComputer environmentOnComputer)
		{
			if (trelloFactory == null) throw new ArgumentNullException(nameof(trelloFactory));
			if (https115белWebBrowser == null) throw new ArgumentNullException(nameof(https115белWebBrowser));

			Dictionary<Card, CardUpdateResult> issues = new Dictionary<Card, CardUpdateResult>();

			bool expectMoreCards = true;
			while (expectMoreCards)
			{
				//https://developer.atlassian.com/cloud/trello/rest/api-group-search/#api-search-get
				//cards_limit
				//integer
				//The maximum number of cards to return. Maximum: 1000
				int maxCardsToReturn = 1000;
				ISearch search = trelloFactory.Search(
					$"-label:\"{CardDescriptionDoesntMatchRequestDescriptionLabelName}\" " +
					$"-label:\"{CommentAboutRequestEventDoesntMatchExpectedValueLabelName}\" " +
					$"-label:\"{InformationalNoteLabelName}\" " +
					$"-label:\"{InvalidCardNameLabelName}\" " +
					$"-label:\"{InvalidRequestIdLabelName}\" " +
					$"-label:\"{UpdatedAutomaticallyLabelName}\"",
					maxCardsToReturn,
					SearchModelType.Cards,
					new IQueryable[]{Board});
				search.Refresh().Wait();
				int foundCardsNumber = 0;
				foreach(Card card in search.Cards)
				{
					foundCardsNumber++;
					Console.WriteLine($"    Знойдзена картка \"{card.Name}\". Паспрабуем прывесці яе ў \"стандартны\" выгляд...");
					CardFullUpdater cardUpdater = new CardFullUpdater(card, environmentOnComputer);
					CardUpdateResult cardUpdateResult = cardUpdater.TryFullUpdate(https115белWebBrowser, this);
					if (cardUpdateResult != CardUpdateResult.Success)
					{
						issues.Add(card, cardUpdateResult);
					}
					ApplyAppropriateLabelToTheCard(card, cardUpdateResult);
				}
				expectMoreCards = (foundCardsNumber == maxCardsToReturn);
			}

			return issues;
		}
	}
}