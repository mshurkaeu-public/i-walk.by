using IWalkBy.Credentials;
using IWalkBy.EdsPortal.RestfulWebService;
using IWalkBy.https115бел.WebPortal;
using IWalkBy.https115белToTrelloSync;
using Manatee.Trello;
using System;
using System.Collections.Generic;

namespace IWalkBy.ConsoleUtilities
{
	public static class Program
	{
		public static void Main(params string[] arguments)
		{
			ArgumentsParser argumentsParser = new ArgumentsParser(arguments);
			ICredentialsProvider credentialsProvider = argumentsParser.GetCredentialsProvider();

			EnvironmentOnComputer environmentOnComputer = new EnvironmentOnComputer(credentialsProvider);
			/* дадай прабел там --> * /
			Console.WriteLine($"{DateTime.Now}: націсніце любую кнопку каб праграма пачала сваю працу.");
			Console.ReadKey(true);
			/* */

			TrelloAuthorization.Default.AppKey = credentialsProvider.GetTrelloAppKey();
			TrelloAuthorization.Default.UserToken = credentialsProvider.GetTrelloUserToken();
			TrelloFactory trelloFactory = new TrelloFactory();

			IList<BoardUpdater> boardUpdaters = BoardsUpdater.BuildListOfAvailableBoardUpdaters(trelloFactory);

			BoardsUpdater.CreateRequiredLabels(boardUpdaters);
			TrelloProcessor.Flush();

			if (BoardsUpdater.ThereAreCardsWithIssues(trelloFactory, boardUpdaters))
			{
				ShowMessageAboutCardsWithIssues();
				return;
			}

			using(Browser https115белWebBrowser = new Browser())
			{
				https115белWebBrowser.Login(
					credentialsProvider.Get115белUsername(),
					credentialsProvider.Get115белPassword());
				try
				{
					// Спачатку патрэбна ўпэўніцца, што карткі, якія ўжо ёсць на дошках "стандартна" выглядаюць.
					// Без гэтага шагу на дошках могуць быць карткі з нестандартнымі імёнамі і таму наступныя шагі
					// могуць стварыць непатрэбныя дублі.
					#pragma warning disable CA1303
					Console.WriteLine("Першы этап - прывесці карткі, дададзеныя ўручную на дошкі ў \"стандартны\" выгляд.");
					#pragma warning restore CA1303

					bool res = BoardsUpdater.MakeCardsLookStandard(
						https115белWebBrowser,
						trelloFactory,
						environmentOnComputer,
						boardUpdaters);
					TrelloProcessor.Flush();

					if (res)
					{
						#pragma warning disable CA1303
						Console.WriteLine("Другі этап - дадаць на дошкі карткі, якія адсутнічаюць.");
						#pragma warning restore CA1303

						WebService webService = new WebService(
							credentialsProvider.Get115белUsername(),
							credentialsProvider.Get115белPassword());

						IList<BoardUpdater> updatedBoards = BoardsUpdater.AddMissingCardsToBoards(
							webService,
							trelloFactory,
							boardUpdaters);

						if (updatedBoards.Count > 0)
						{
							TrelloProcessor.Flush();

							BoardsUpdater.MakeCardsLookStandard(
								https115белWebBrowser,
								trelloFactory,
								environmentOnComputer,
								updatedBoards);
							TrelloProcessor.Flush();
						}

						#pragma warning disable CA1303
						Console.WriteLine("Трэці этап - абнавіць тыя карткі, аб зменах у якіх паведамляе server.");
						#pragma warning restore CA1303

						BoardsUpdater.HandleServerNotifications(
							https115белWebBrowser,
							webService,
							trelloFactory,
							environmentOnComputer,
							boardUpdaters);
						TrelloProcessor.Flush();
					}
					else
					{
						ShowMessageAboutCardsWithIssues();
					}
				}
				finally
				{
					https115белWebBrowser.Logout();
				}
			}
		}

		private static void ShowMessageAboutCardsWithIssues()
		{
			Console.WriteLine(
				$"{DateTime.Now}: Былі знойдзены карткі, якія не могуць быць апрацаваны аўтаматычна. " +
				"Гэтыя карткі былі памечаны спецыяльнымі маркерамі. " +
				"Табе патрэбна самастойна вырашыць, што рабіць з гэтымі карткамі."
			);
		}
	}
}