using IWalkBy.Credentials;
using IWalkBy.https115бел.WebPortal;
using Manatee.Trello;
using System;
using System.Net.Http;
using System.Globalization;
using System.Text.RegularExpressions;
using CardAttachment = Manatee.Trello.Attachment;
using RequestAttachment = IWalkBy.https115бел.WebPortal.Attachment;
using IWalkBy.Trello;

namespace IWalkBy.ConsoleUtilities
{
	public static class Program
	{
		//праверыць, что ў тэксце есць каардынаты кропкі на карце
		//прыклады:
		//1) "Апісанне праблемы {27.000000,53.000000}"
		//2) "Другая праблема {52.000000, 28.000000}"
		//шырата і даўгата запісаны ў любым парадку таму што для Беларусі можно здагдацца дзе што.
		//паміж шаратой і даўгатой стаіць коска. 
		private static Regex coordinatesArePresentRegex = new Regex(@"^.+\{\d\d\.\d+, ?\d\d\.\d+\}\s*$", //{50.127250, 19.492307}, {56.000000, 37.125812}
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

		//колькі заўгодна прабельных сымбалаў у пачатку радка
		//непустое апісанне з непрабельным сымбалам у канцы апісання
		//колькі заўгодна прабельных сымбалаў пасля апісання і перад каардынатамі
		//каардынаты з коскай паміж імі
		//колькі заўгодна прабельных сымбалаў у канцы радка
		private static Regex problemDescriptionAndCoordinatesExtractorRegex = new Regex(@"^\s*(?<pDescr>.+\S)\s*\{(?<c1>\d\d\.\d+), ?(?<c2>\d\d\.\d+)\}\s*$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		public static void Main(params string[] arguments)
		{
			ArgumentsParser argumentsParser = new ArgumentsParser(arguments);
			ICredentialsProvider credentialsProvider = argumentsParser.GetCredentialsProvider();

			TrelloAuthorization.Default.AppKey = credentialsProvider.GetTrelloAppKey();
			TrelloAuthorization.Default.UserToken = credentialsProvider.GetTrelloUserToken();
			ITrelloFactory trelloFactory = new TrelloFactory();

			IBoard incomingRequestDraftsBoard = BoardsFinder.GetTheBoardWithAGivenPurpose(
				KnownBoardPurposes.IncomingRequestDrafts,
				trelloFactory);

			IList listWithCardsToUpload = null;
			IList listWithProcessedCards = null;
			foreach (IList cardsList in incomingRequestDraftsBoard.Lists)
			{
				if (cardsList.Name == "Можна адпраўляць у 115.бел")
				{
					listWithCardsToUpload = cardsList;
				}
				else if (cardsList.Name == "Адпраўлена ў 115.бел")
				{
					listWithProcessedCards = cardsList;
				}
			}

			ICardCollection cardsToUpload = listWithCardsToUpload.Cards;
			cardsToUpload.Limit = 20;
			cardsToUpload.Refresh().Wait();

			if (!cardsToUpload.GetEnumerator().MoveNext())
			{
				#pragma warning disable CA1303
				Console.WriteLine("Спіс картак у Trello для загрузкі ў 115.бел пусты.");
				Console.WriteLine("Канец.");
				#pragma warning restore CA1303
				return;
			}

			using (Browser a115belWebBrowser = new Browser())
			{
				string a115белUsername = credentialsProvider.Get115белUsername();
				a115belWebBrowser.Login(
					a115белUsername,
					credentialsProvider.Get115белPassword());
				try
				{
					HttpClient trelloHttpClientToDownloadFiles = IWalkBy.Trello.HttpClientFactory.GetSingletone();//credTrello.UserName, credTrello.Password);

					foreach (Card card in cardsToUpload)
					{
						Console.WriteLine("Апрацоўваю картку \"" + card.Name + "\" (" + card.Id + ")...");

						string cardDescription = card.Description;
						if (String.IsNullOrWhiteSpace(cardDescription))
						{
							#pragma warning disable CA1303
							Console.WriteLine("Апісанне карткі пустое. Апісанне абавазана быць непустым. Прапускаю картку.");
							#pragma warning restore CA1303
							continue;
						}
						if (!coordinatesArePresentRegex.IsMatch(cardDescription))
						{
							#pragma warning disable CA1303
							Console.WriteLine("У аппісанні карткі адсутнічаюць каардынаты. Каардынаты абавязаны прысутнічаць. Прапускаю картку.");
							#pragma warning restore CA1303
							continue;
						}
						Match match = problemDescriptionAndCoordinatesExtractorRegex.Match(cardDescription);
						if (!match.Success)
						{
							#pragma warning disable CA1303
							Console.WriteLine("У аппісанні карткі адсутнічаюць апісанне самой праблемы. А яно абавязана прысутнічаць. Прапускаю картку.");
							#pragma warning restore CA1303
							continue;
						}

						string originalProblemDescription = match.Groups["pDescr"].Value;
						string c1 = match.Groups["c1"].Value;
						string c2 = match.Groups["c2"].Value;
						#pragma warning disable CA1303
						//Console.WriteLine("\"{0}\"\n{1}\n{2}", problemDescription, c1, c2);
						#pragma warning restore CA1303

						//Крайнія кропкі Беларусі. Узята з https://be.wikipedia.org/wiki/%D0%93%D0%B5%D0%B0%D0%B3%D1%80%D0%B0%D1%84%D1%96%D1%8F_%D0%91%D0%B5%D0%BB%D0%B0%D1%80%D1%83%D1%81%D1%96
						//Поўнач 	возера Асвейскае        	56˚10′ пн.ш.	56.166667
						//Поўдзень	гарадскі пасёлак Камарын	51˚16′ пн.ш.	51.266667
						//Захад 	горад Высокае           	23˚11′ у.д. 	23.183333
						//Усход 	гарадскі пасёлак Хоцімск	32˚47′ у.д. 	32.783333
						const Decimal northMost = 56.166667M;
						const Decimal southMost = 51.266667M;
						const Decimal westMost = 23.183333M;
						const Decimal eastMost = 32.783333M;

						Decimal d1 = Decimal.Parse(c1, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
						Decimal d2 = Decimal.Parse(c2, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

						string latitude;
						string longitude;
						if (
							(d1 <= northMost && d1 >= southMost) &&
							(d2 <= eastMost  && d2 >= westMost))
						{
							latitude = c1;
							longitude = c2;
						}
						else if (
							(d2 <= northMost && d2 >= southMost) &&
							(d1 <= eastMost  && d1 >= westMost))
						{
							latitude = c2;
							longitude = c1;
						}
						else
						{
							#pragma warning disable CA1303
							Console.WriteLine("З каардынатамі на картке нешта не тое {{{0}, {1}}}. Прапускаю картку.", c1, c2);
							#pragma warning restore CA1303
							continue;
						}

						#region апрацоўка файлаў, прымацаваных да карткі Trello
						//праверым майм-тып файлаў
						string unsupportedMimeType = null;
						int attachmentsNumber = 0;
						foreach(CardAttachment cardAttachment in card.Attachments)
						{
							attachmentsNumber++;
							string mimeType = cardAttachment.MimeType;
							if (mimeType == "image/jpg" || mimeType == "image/png" || mimeType == "image/jpeg")
							{
								continue;
							}
							else
							{
								unsupportedMimeType = mimeType;
								break;
							}
						}

						if (unsupportedMimeType != null)
						{
							#pragma warning disable CA1303
							Console.WriteLine("Да карткі прычэплены файл тыпу {0}. Гэты тып не падтрымливаеццца. Прапускаю картку.", unsupportedMimeType);
							#pragma warning restore CA1303
							continue;
						}
						if (attachmentsNumber == 0)
						{
							#pragma warning disable CA1303
							Console.WriteLine("Да карткі не прычэплена ніводнага файла. Гэта не падтрымливаеццца. Прапускаю картку.");
							#pragma warning restore CA1303
							continue;
						}
						if (attachmentsNumber > 3)
						{
							#pragma warning disable CA1303
							Console.WriteLine("Да карткі прычэплена больш за 3 файла ({0}). Гэта не падтрымливаеццца. Прапускаю картку.", attachmentsNumber);
							#pragma warning restore CA1303
							continue;
						}

						RequestAttachment[] requestAttachments = new RequestAttachment[attachmentsNumber];
						int i = 0;
						foreach(CardAttachment cardAttachment in card.Attachments)
						{
							Console.Write($"Файл {cardAttachment.Name}({cardAttachment.MimeType})...");
							HttpResponseMessage httpResponseMessage = trelloHttpClientToDownloadFiles.GetAsync(
								new Uri(cardAttachment.Url)).Result;
							byte[] attachmentBytes = httpResponseMessage.Content.ReadAsByteArrayAsync().Result;
							Console.WriteLine($" спампаваў {attachmentBytes.Length} байтаў");

							RequestAttachment requestAttachment = new RequestAttachment(
								cardAttachment.Name,
								cardAttachment.MimeType,
								attachmentBytes);
							requestAttachments[i] = requestAttachment;
							i++;
						}
						#endregion

						const string descriptionFormat = "{0}\r\n\r\n" +
							"Глядзі фота і кропку на карце. Калі ласка, пасля выканання заяўкі зрабіце фота з такіх самых ракурсаў. Дзякуй.\r\n\r\n" +
							"{{\r\n" +
								"\t\"c\": \"{1},{2}\",\r\n" +
								"\t\"Google карты\": \"https://www.google.com/maps/search/?api=1&query={1},{2}\",\r\n" +
								"\t\"Яндекс карты\": \"https://yandex.ru/maps/?pt={2},{1}&z=17\",\r\n" +
								"\t\"t\": \"{3}\",\r\n" +
								"\t\"1\": \"{4}\"\r\n" +
							"}}";
						string textFor115Request = String.Format(CultureInfo.InvariantCulture,
							descriptionFormat,
							originalProblemDescription,
							latitude, longitude,
							card.Id,
							"0123456789");
						if (textFor115Request.Length > 1000)
						{
							#pragma warning disable CA1303
							Console.WriteLine("Апісанне разам з дадатковай інфармацыей не ўмяшчаецца ў 1000 сымбалаў. Прапускаю картку.");
							#pragma warning restore CA1303
							continue;
						}

						//стварыць заяўку ў 115.бел
						Request request = new Request(textFor115Request, latitude, longitude, requestAttachments);
						a115belWebBrowser.CreateRequest(request);
						FoundRequestInfo[] latestOnReview = a115belWebBrowser.PaginateSearch(RequestStatusCriterion.OnReview, 1, 1, 1);
						if (latestOnReview.Length != 1)
						{
							throw new NotImplementedException("Нечаканая сітуацыя. Толькі што створаны запрос у 115.бел не знойдзены.");
						}
						if (!latestOnReview[0].Title.Contains(card.Id, StringComparison.InvariantCulture))
						{
							throw new NotImplementedException("Нечаканая сітуацыя. Знойдзены запрос у 115.бел не ўтрымлівае id Trello карткі.");
						}

						string justCreatedRequestId = latestOnReview[0].Id;
						card.Name = "1:" + justCreatedRequestId;

						textFor115Request = String.Format(CultureInfo.InvariantCulture,
							descriptionFormat,
							originalProblemDescription,
							latitude, longitude,
							card.Id,
							justCreatedRequestId);
						Console.WriteLine(textFor115Request);
						a115belWebBrowser.EditRequestDescription(justCreatedRequestId, textFor115Request);
						card.Description = textFor115Request;

						card.List = listWithProcessedCards;
					}
				}
				finally
				{
					a115belWebBrowser.Logout();
				}
			}
		}
	}
}