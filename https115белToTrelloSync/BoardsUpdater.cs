using IWalkBy.EdsPortal.RestfulWebService;
using IWalkBy.https115бел.WebPortal;
using IWalkBy.TextUtils;
using IWalkBy.Trello;
using Manatee.Trello;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using EdsPortalNotification = IWalkBy.EdsPortal.RestfulWebService.Notification;

namespace IWalkBy.https115белToTrelloSync
{
	public static class BoardsUpdater
	{
		public static IList<BoardUpdater> AddMissingCardsToBoards(
			WebService webService,
			ITrelloFactory trelloFactory,
			IList<BoardUpdater> boardUpdaters)
		{
			if (webService == null) throw new ArgumentNullException(nameof(webService));
			if (trelloFactory == null) throw new ArgumentNullException(nameof(trelloFactory));
			if (boardUpdaters == null) throw new ArgumentNullException(nameof(boardUpdaters));

			RequestInfo[] allRequests = webService.GetAllRequests();
			Dictionary<int, RequestInfo> requestCandidates = new Dictionary<int, RequestInfo>();
			foreach (RequestInfo rInfo in allRequests)
			{
				if (!requestCandidates.TryAdd(rInfo.Id, rInfo))
				{
					Console.WriteLine($"! Памылка ў адказе RESTful сэрвіса - прадубляваны нумар заяўкі {rInfo.Id}:{rInfo.Number}. Проста ігнарую.");
				}
			}

			foreach (BoardUpdater boardUpdater in boardUpdaters)
			{
				IList<int> ids = boardUpdater.GetAllRequestsIds(trelloFactory);
				foreach(int id in ids)
				{
					requestCandidates.Remove(id);
				}

				Thread.Sleep(100);// каб панізіць колькасць запытаў да Trello ў секунду
			}

			List<BoardUpdater> res = new List<BoardUpdater>();
			int i = 0;
			foreach(RequestInfo rInfo in requestCandidates.Values)
			{
				i++;
				int id = rInfo.Id;
				string minimalCardName = $"1:{id}";
				Console.WriteLine($"{i}:{id}");

				string manualCreationInstructions = $"Табе патрэбна ўручную стварыць картку з назвай \"{minimalCardName}\" на падыходзячай дошцы ці дошках.";
				if (!rInfo.Latitude.HasValue || !rInfo.Longitude.HasValue)
				{
					Console.WriteLine($"Заяўка без каардынат {rInfo.Id}:{rInfo.Number}. {manualCreationInstructions}");
					continue;
				}

				List<BoardUpdater> boardUpdaterCandidates = new List<BoardUpdater>();
				foreach (BoardUpdater boardUpdater in boardUpdaters)
				{
					if (boardUpdater.PointBelongsToTheRegion(rInfo.Latitude.Value, rInfo.Longitude.Value))
					{
						boardUpdaterCandidates.Add(boardUpdater);
					}
				}

				if (boardUpdaterCandidates.Count > 1)
				{
					// калі нейкая заяўка адносіцца адразу да некалькіх тэрыторый, то выдаліць з
					// кандыдатаў тыя тэрыторыі, якія відавочна гэта запытваюць
					List<BoardUpdater> toRemoveFromCandidates = new List<BoardUpdater>();
					foreach (BoardUpdater candidate in boardUpdaterCandidates)
					{
						if (candidate.SkipRequestIfBelongsToOthers)
						{
							toRemoveFromCandidates.Add(candidate);
						}
					}
					foreach (BoardUpdater candidate in toRemoveFromCandidates)
					{
						boardUpdaterCandidates.Remove(candidate);
					}
				}

				if (boardUpdaterCandidates.Count == 0)
				{
					Console.WriteLine(
						$"Заяўка {rInfo.Id}:{rInfo.Number} не належыць ні адной вядомай тэрыторыі. " +
						$"Ці правілы аўтаматычнага размеркавання супярэчаць адно аднаму. {manualCreationInstructions}");
					continue;
				}

				foreach (BoardUpdater target in boardUpdaterCandidates)
				{
					target.OnReviewList.Cards.Add(minimalCardName).Wait();
					if (!res.Contains(target))
					{
						res.Add(target);
					}
					Thread.Sleep(100);// каб панізіць колькасць запытаў да Trello ў секунду
				}
			}

			return res;
		}

		public static IList<BoardUpdater> BuildListOfAvailableBoardUpdaters(ITrelloFactory trelloFactory)
		{
			if (trelloFactory == null) throw new ArgumentNullException(nameof(trelloFactory));

			List<BoardUpdater> res = new List<BoardUpdater>();

			IList<IBoard> boards = BoardsFinder.GetBoardsWithAGivenPurpose(
				KnownBoardPurposes.PublishMyRequestsForTheGivenRegion,
				trelloFactory);
			foreach(Board trelloBoard in boards)
			{
				string boardDescriptionFullText = trelloBoard.Description;

				JObject jObject;
				if (!JsonInDescription.MatchesTextualDescriptionFollowedByJson(boardDescriptionFullText, out jObject))
				{
					throw new NotImplementedException("Нешта незразумелае. Не ведаю, што рабіць.");
				}

				BoardUpdater b = new BoardUpdater(trelloBoard, jObject);
				res.Add(b);
			}

			return res;
		}

		public static void CreateRequiredLabels(IList<BoardUpdater> boardUpdaters)
		{
			if (boardUpdaters == null) throw new ArgumentNullException(nameof(boardUpdaters));

			foreach (BoardUpdater boardUpdater in boardUpdaters)
			{
				boardUpdater.CreateRequiredLabels();
				Thread.Sleep(100);// каб панізіць колькасць запытаў да Trello ў секунду
			}
		}

		public static void HandleServerNotifications(
			Browser https115белWebBrowser,
			WebService webService,
			ITrelloFactory trelloFactory,
			IEnvironmentOnComputer environmentOnComputer,
			IList<BoardUpdater> boardUpdaters)
		{
			if (https115белWebBrowser == null) throw new ArgumentNullException(nameof(https115белWebBrowser));
			if (webService == null) throw new ArgumentNullException(nameof(webService));
			if (trelloFactory == null) throw new ArgumentNullException(nameof(trelloFactory));
			if (environmentOnComputer == null) throw new ArgumentNullException(nameof(environmentOnComputer));
			if (boardUpdaters == null) throw new ArgumentNullException(nameof(boardUpdaters));

			Dictionary<string, BoardUpdater> boardIdToItsUpdater = new Dictionary<string, BoardUpdater>();
			List<IQueryable> boardsToSearchOn = new List<IQueryable>();
			foreach (BoardUpdater boardUpdater in boardUpdaters)
			{
				boardIdToItsUpdater[boardUpdater.Board.Id] = boardUpdater;
				boardsToSearchOn.Add(boardUpdater.Board);
			}

			EdsPortalNotification[] notifications = webService.GetNotifications();
			//пачынаем з самых старых паведамленняў
			for (int i = notifications.Length - 1; i >= 0; i--)
			{
				EdsPortalNotification notification = notifications[i];
				int maxCardsToReturn = 1000;
				ISearch search = trelloFactory.Search(
					$"name:1:{notification.RequestId}",
					maxCardsToReturn,
					SearchModelType.Cards,
					boardsToSearchOn);
				search.Refresh().Wait();
				bool cardWasFound = false;
				foreach(Card card in search.Cards)
				{
					cardWasFound = true;
					BoardUpdater boardUpdater = boardIdToItsUpdater[card.Board.Id];
					Console.WriteLine($"  Абнаўляю картку \"{card.Name}\" на дошцы \"{boardUpdater.Board.Name}\"");
					CardFullUpdater cardUpdater = new CardFullUpdater(card, environmentOnComputer);
					CardUpdateResult cardUpdateResult = cardUpdater.TryFullUpdate(https115белWebBrowser, boardUpdater);
					if (cardUpdateResult != CardUpdateResult.Success)
					{
						throw new NotImplementedException();
					}

					Thread.Sleep(100);// каб панізіць колькасць запытаў да Trello ў секунду
				}

				if (!cardWasFound)
				{
					Console.WriteLine($"  Картка для заяўкі {notification.RequestId} не знойдзена!");
					throw new NotImplementedException();
				}

				webService.DeleteNotification(notification);
			}

		}

		public static bool MakeCardsLookStandard(
			Browser https115белWebBrowser,
			ITrelloFactory trelloFactory,
			IEnvironmentOnComputer environmentOnComputer,
			IList<BoardUpdater> boardUpdaters)
		{
			if (https115белWebBrowser == null) throw new ArgumentNullException(nameof(https115белWebBrowser));
			if (trelloFactory == null) throw new ArgumentNullException(nameof(trelloFactory));
			if (environmentOnComputer == null) throw new ArgumentNullException(nameof(environmentOnComputer));
			if (boardUpdaters == null) throw new ArgumentNullException(nameof(boardUpdaters));

			bool res = true;
			foreach (BoardUpdater boardUpdater in boardUpdaters)
			{
				Dictionary<Card, CardUpdateResult> issues = boardUpdater.UpdateManuallyAddedCards(
					trelloFactory,
					https115белWebBrowser,
					environmentOnComputer);
				if (issues.Count > 0)
				{
					res = false;
				}

				Thread.Sleep(100);// каб панізіць колькасць запытаў да Trello ў секунду
			}

			return res;
		}

		public static bool ThereAreCardsWithIssues(ITrelloFactory trelloFactory, IList<BoardUpdater> boardUpdaters)
		{
			if (boardUpdaters == null) throw new ArgumentNullException(nameof(boardUpdaters));

			foreach (BoardUpdater boardUpdater in boardUpdaters)
			{
				if (boardUpdater.ThereAreCardsWithIssues(trelloFactory))
				{
					return true;
				}
				Thread.Sleep(100);// каб панізіць колькасць запытаў да Trello ў секунду
			}

			return false;
		}
	}
}