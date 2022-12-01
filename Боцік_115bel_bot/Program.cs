using IWalkBy.ConsoleUtilities;
using IWalkBy.Credentials;
using IWalkBy.Trello;
using Manatee.Trello;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

/*
nowy_czarnawik - Стварыць новы чарнавік заяўкі ў 115.бел.
pakazy_czarnawik - Паказаць што бот унёс у чарнавік заяўкі 115.бел. Мае сэнс праверць перад адпраўкай.
adprawic_czarnawik - Адправіць чарнавік на разгляд [ЧАЛАВЕКУ].
scierci_apisannie - Сцерці тэкст апісання праблемы з чарнавіка.
scierci_fajly - Сцерці фота з чарнавіка.
szto_z_czarnawikom - Высветлiць, што зараз з Вашым чарнавiком (патрэбна ведаць нумар чарнавіка).
*/

namespace IWalkBy.TelegramBots.Боцік_115bel_bot
{
	public static class Program
	{
		private const string newDraftCommand = "/nowy_czarnawik";
		private const string resetCurrentDescriptionCommand = "/scierci_apisannie";
		private const string resetCurrentFilesCommand = "/scierci_fajly";
		private const string showCurrentDraftCommand = "/pakazy_czarnawik";
		private const string sendDraftToMikalaiCommand = "/adprawic_czarnawik";
		private const string showInfoAboutRequestDraft = "/szto_z_czarnawikom";

		private const long maxAllowedAttachmentSize = 10485760 - 1;

		private const string descriptionOfProcessWithFilesUpload = "" +
			"Звяртаю Вашу ўвагу, што адпраўляць патрэбна менавіта файлы, каб не гублялась якасць фота. " +
			"Аднак памер файлаў павінен быць меньш за 10 Мб (меньш за 10 485 760 байт). " +
			"Гэта абмежаванне інструментаў 🛠, якія выкарыстоўваюцца мной.\n" +
			"\n" +
			"Колькасць файлаў - не меней аднаго (маё ўласнае патрабаванне) і не болей за тры (абмежаванне 115.бел). " +
			"Каб адправіць менавіта файл, а не пераціснутае Telegram-ам фота, Вам патрэбна будзе ў меню пасля 📎 выбраць " +
			"пункт \"Файл\" ў ніжнім радку. Вельмі часта людзі не звяртаюць увагу на гэты радок у меню адпраўкі.";

		private static ConcurrentDictionary<long, SessionState> usersSessions = new ConcurrentDictionary<long, SessionState>();

		private static TelegramBotClient bot;
		private static string telegramBotToken;

		private static ITrelloFactory trelloFactory;
		private static HttpClient httpClient = new HttpClient();

		private static Dictionary<string, string> incomingRequestDarftsBoardColumns = new Dictionary<string, string>();
		private static IList incominRequestDraftsList;

		private static string імяЎНазоўнымСклоне;
		private static string імяЎДавальнымСклоне;

		static async Task HandleError(ITelegramBotClient _, Exception exception, CancellationToken cancellationToken)
		{
			await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(true);
		}

		// Each time a user interacts with the bot, this method is called
		static async Task HandleUpdate(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
		{
			switch (update.Type)
			{
				// A message was received
				case UpdateType.Message:
					await HandleMessage(update.Message!).ConfigureAwait(true);
					break;
			}
		}

		private static bool IsLocationInBelarus(Location location)
		{
			//Гэта вельмі недакладны алгарытм, але самыя відавочныя выпадкі будуць адсечаны

			const Decimal northMost = 56.166667M;
			const Decimal southMost = 51.266667M;
			const Decimal westMost = 23.183333M;
			const Decimal eastMost = 32.783333M;

			Decimal latitude = (Decimal)location.Latitude;
			Decimal longitude = (Decimal)location.Longitude;
			if (latitude > northMost || latitude < southMost)
			{
				return false;
			}
			else if (longitude > eastMost || longitude < westMost)
			{
				return false;
			}

			return true;
		}

		private static bool IsRequestDraftContainsRequiredMinimum(RequestDraft requestDraft)
		{
			bool res = requestDraft.Description.Length > 0 && requestDraft.Location != null && requestDraft.ListOfPhotos.Count > 0;
			return res;
		}

		public static void Main(params string[] arguments)
		{
			ArgumentsParser argumentsParser = new ArgumentsParser(arguments);
			імяЎНазоўнымСклоне = argumentsParser.GetParameterValue("імя-ўладальніка-ў-назоўным-склоне");
			імяЎДавальнымСклоне = argumentsParser.GetParameterValue("імя-ўладальніка-ў-давальным-склоне");
			if (String.IsNullOrWhiteSpace(імяЎНазоўнымСклоне) || String.IsNullOrWhiteSpace(імяЎДавальнымСклоне))
			{
				throw new NotImplementedException("Не ведаю што рабіць, калі імя ўладальніка не ўказана.");
			}

			ICredentialsProvider credentialsProvider = argumentsParser.GetCredentialsProvider();

			TrelloAuthorization.Default.AppKey = credentialsProvider.GetTrelloAppKey();
			TrelloAuthorization.Default.UserToken = credentialsProvider.GetTrelloUserToken();
			trelloFactory = new TrelloFactory();

			IBoard incomingRequestDraftsBoard = BoardsFinder.GetTheBoardWithAGivenPurpose(
				KnownBoardPurposes.IncomingRequestDrafts,
				trelloFactory);
			incomingRequestDraftsBoard.Lists.Refresh().Wait();
			foreach (List list in incomingRequestDraftsBoard.Lists)
			{
				incomingRequestDarftsBoardColumns.Add(list.Id, list.Name);
				if (list.Name == "Сырое фота, сырая кропка на карце, сырое апісанне")
				{
					incominRequestDraftsList = list;
				}
			}

			telegramBotToken = credentialsProvider.GetTelegramBotTokenБоціка();
			bot = new TelegramBotClient(telegramBotToken);
			using (CancellationTokenSource cts = new CancellationTokenSource())
			{
				// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool, so we use cancellation token
				bot.StartReceiving(
					updateHandler: HandleUpdate,
					errorHandler: HandleError,
					cancellationToken: cts.Token
				);

				#pragma warning disable CA1303
				Console.WriteLine("Боцік чакае ўваходзячыя паведамлення ад карыстальнікаў. Каб спыніць трэба націснуць <ENTER>.");
				#pragma warning restore CA1303
				Console.ReadLine();

				// Send cancellation request to stop the bot
				cts.Cancel();
			}
		}

		private static bool RequestDraftIsInProgress(long userId, out RequestDraft requestDraft)
		{
			SessionState sessionState;
			if (usersSessions.TryGetValue(userId, out sessionState))
			{
				requestDraft = sessionState.RequestDraft;
				return (requestDraft != null);
			}
			else
			{
				requestDraft = null;
				return false;
			}
		}

		private static async Task SendToUserHowMuchRemainedToPopulate(
			long userId, int messageIdToReplyTo,
			RequestDraft requestDraft,
			bool showRecommendationToDoublecheck)
		{
			StringBuilder sb = new StringBuilder();

			if (requestDraft.Description.Length > 0)
			{
				sb.Append("✅ Нейкае апісанне ўжо ёсць. ");

				int numAllowedCharacters = RequestDraft.MaxDescriptionLength - requestDraft.Description.Length;
				if (numAllowedCharacters > 0)
				{
					sb.Append("Вы можаце дадаць яшчэ ");
					sb.Append(numAllowedCharacters);
					sb.AppendLine(" сымбалаў у апісанне, калі жадаеце.");
				}
				else
				{
					sb.AppendLine("І апісанне ўжо дасягнула максімальнай дліны.");
				}
			}
			else
			{
				sb.AppendLine("❗ Патрэбна дадаць апісанне.");
			}

			sb.AppendLine();
			if (requestDraft.Location == null)
			{
				sb.AppendLine("❗ Патрэбна даслаць месцазнаходжанне праблемы.");
			}
			else
			{
				sb.AppendLine("✅ Кропку на карце Вы ўжо даслалі.");
			}

			sb.AppendLine();
			int photosAllowedToSend = 3 - requestDraft.ListOfPhotos.Count;
			if (photosAllowedToSend == 3)
			{
				sb.Append("❗ Вам патрэбна даслаць хаця б адзін файл з фота.");
			}
			else if (photosAllowedToSend == 2)
			{
				sb.Append("✅ Адно фота праблемы ўжо ёсць. Вы можаце даслаць яшчэ два файла з фота.");
			}
			else if (photosAllowedToSend == 1)
			{
				sb.Append("✅ Два фота праблемы ўжо ёсць. Вы можаце даслаць яшчэ адзін файл з фота.");
			}
			else
			{
				sb.Append("✅ Фота праблемы ўжо ёсць. Вы ўжо даслалі максімальную колькасць файлаў з фота. Болей не патрэбна.");
			}

			if (IsRequestDraftContainsRequiredMinimum(requestDraft))
			{
				sb.AppendLine();
				sb.AppendLine();
				sb.Append("Ваш чарнавік змяшчае нейкае апісанне, ужо прымацавана месцазнаходжанне і ёсць фота.");
				if (showRecommendationToDoublecheck)
				{
					sb.Append(" Калі жадаеце прагледзець свой чарнавік заяўкі перед адпраўкай - выклікайце каманду " + showCurrentDraftCommand);
				}
				sb.Append(" Калі Вы ўжо гатовы дасылаць чарнавік - выклікайце каманду " + sendDraftToMikalaiCommand);
			}

			await bot.SendTextMessageAsync(userId, sb.ToString(), replyToMessageId: messageIdToReplyTo).ConfigureAwait(true);
		}

		static async Task HandleMessage(Message msg)
		{
			User user = msg.From!;
			if (user == null)
			{
				return;
			}
			if (user.IsBot)
			{
				return;
			}
			long userId = user.Id;

			// Print to console
			Console.WriteLine($"{userId} ({user.FirstName} {user.LastName}) sent message of type {msg.Type}");

			RequestDraft currentDraft;
			switch (msg.Type)
			{
				case MessageType.Document:
					if (RequestDraftIsInProgress(userId, out currentDraft))
					{
						if (currentDraft.ListOfPhotos.Count >= 3)
						{
							await bot.SendTextMessageAsync(
								userId,
								"Вы спрабуеце прымацаваць больш трох файлаў з фота да чарнавіка заяўкі ў 115.бел." +
								"Нажаль, гэта перавышае абмежаванне 115.бел.\n" +
								"\n" +
								"Каб даведацца як выглядае Ваш чарнавік на дадзены момант - скарастайцеся камандай " + showCurrentDraftCommand + ".\n" +
								"Каб ачысціць спіс файлаў у чарнавіку - скарастайцеся камандай " + resetCurrentFilesCommand + ".\n" +
								"Каб пачаць стварэнне чарнавіка заяўкі з самага пачатку - скарастайцеся камандай " + newDraftCommand + ".\n" +
								"\n" +
								"Дзякуй.",
								replyToMessageId: msg.MessageId).ConfigureAwait(true);
						}
						else
						{
							Document doc = msg.Document;
							string mimeType = doc.MimeType;

							Console.WriteLine("Document size=" + doc.FileSize + ", name=" + doc.FileName + ", type=" + doc.MimeType);
							if (mimeType == "image/jpeg" || mimeType == "image/png" || mimeType == "image/heic")
							{
								if (doc.FileSize <= maxAllowedAttachmentSize)
								{
									currentDraft.ListOfPhotos.Add(msg);
									string documentComment = msg.Caption;
									if (documentComment != null)
									{
										currentDraft.Description.AppendLine(documentComment.Trim());
									}

									await SendToUserHowMuchRemainedToPopulate(userId, msg.MessageId, currentDraft, true).ConfigureAwait(true);
								}
								else
								{
									await bot.SendTextMessageAsync(
										userId,
										"Нажаль, я не магу прыняць гэта фота праз яго памер.\n" +
										"Прабачце.\n" +
										"\n" +
										descriptionOfProcessWithFilesUpload,
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
								}
							}
							else
							{
								await bot.SendTextMessageAsync(
									userId,
									"Нажаль, я не ведаю што рабіць з файлам тыпу \"" + mimeType + "\".\n" +
									"Прабачце.\n" +
									"\n" +
									"Умею працаваць з image/jpeg, image/png, image/heic.",
									replyToMessageId: msg.MessageId).ConfigureAwait(true);
							}
						}
					}
					else
					{
						await bot.SendTextMessageAsync(
							userId,
							"Прабачце, я вельмі малады бот і, магчыма, проста неправільна разумею Вас.\n" +
							"\n" +
							"Калі Вы спрабуеце стварыць чарнавік заяўкі для 115.бел, то пачніце з каманды " + newDraftCommand + ".\n" +
							"\n" +
							"Дзякуй.",
							replyToMessageId: msg.MessageId).ConfigureAwait(true);
					}
					break;

				case MessageType.Location:
					if (RequestDraftIsInProgress(userId, out currentDraft))
					{
						if (IsLocationInBelarus(msg.Location))
						{
							currentDraft.Location = msg.Location;
							await SendToUserHowMuchRemainedToPopulate(userId, msg.MessageId, currentDraft, true).ConfigureAwait(true);
						}
						else
						{
							currentDraft.Location = null;
							await bot.SendTextMessageAsync(
								userId,
								"Гэта месцазнаходжанне па за межамі Беларусі. Немагчыма выправіць там праблему праз службу 115.бел.",
								replyToMessageId: msg.MessageId).ConfigureAwait(true);
						}
					}
					else
					{
						await bot.SendTextMessageAsync(
							userId,
							"Прабачце, я вельмі малады бот і, магчыма, проста неправільна разумею Вас.\n" +
							"\n" +
							"Калі Вы спрабуеце стварыць чарнавік заяўкі для 115.бел, то пачніце з каманды " + newDraftCommand + ".\n" +
							"\n" +
							"Дзякуй.",
							replyToMessageId: msg.MessageId).ConfigureAwait(true);
					}
					break;

				case MessageType.Photo:
					await bot.SendTextMessageAsync(
						userId,
						"Вы адправілі фота з рэжыму \"Галерэя\". Таму Telegram пераціснуў яго.\n" +
						"\n" +
						descriptionOfProcessWithFilesUpload + "\n" +
						"\n" +
						"Дзякуй.",
						replyToMessageId: msg.MessageId).ConfigureAwait(true);
					break;

				case MessageType.Text:
					string text = msg.Text!;
					Console.WriteLine($"{userId} ({user.FirstName} {user.LastName}) sent \"{text}\"");
					if (text.StartsWith("/", StringComparison.Ordinal))
					{
						switch (text)
						{
							case "/start":
								await bot.SendTextMessageAsync(
									userId,
									"Прывітанне!\n" +
									"\n" +
									"Я Боцік. 🥾\n" +
									"Я рады, што Вы завіталі да меня!\n" +
									"\n" +
									"Калі Вы гуляеце па вуліцы, то часам можаце заўважыць нейкую камунальную праблему. " +
									"Правал на пешаходнай дарожцы 🕳, іржавая сметніца 🗑 ці тэхнічная скрыня, не працуе ліхтар 🔦 і г.д...\n" +
									"\n" +
									"Я дапамагаю ствараць заяўкі ў службе 115.бел. Працэс выглядае наступным чынам:\n" +
									"1. Вы ствараеце чарнавік (апісанне, 1-3 фота, кропка на карце).\n" +
									$"2. {імяЎНазоўнымСклоне} праглядае Ваш чарнавік і на яго аснове стварае адну або некалькі заявак ў 115.бел.\n" +
									"3. Пасля стварэння заяўкі ў 115.бел картка з інфармацыяй публікуецца на адной з канбан-дошак Trello," +
									" куды Вы можаце зайсці (_без пароля, рэгістрацыі і СМС 😉_) і даведацца навіны пра заяўкі.\n" +
									"\n" +
									"Вось гэтыя канбан-дошкі. Проста адкрывайце спасылкі ў браўзеры. Спеціяльны мабільны дадатак зусім не абавазковы.\n" +
									"\n" +
									"Маладзечна - https://trello.com/b/RLIsXeMR/маладзечна115бел\n" +
									"\n" +
									"Менск. Вяснянка - https://trello.com/b/bTqqljVf/менсквяснянка115бел\n" +
									"\n" +
									"Менск. Лебядзіны - https://trello.com/b/o83CcKcP/менсклебядзіны115бел\n" +
									"\n" +
									"Недзе ў Беларусі - https://trello.com/b/z9dIOtS0/недзе-ў-беларусі115бел\n" +
									"\n" +
									"*Каб стварыць чарнавік заяўкі карастайцеся камандай " + newDraftCommand + "*.\n" +
									"",
									ParseMode.Markdown,
									replyToMessageId: msg.MessageId).ConfigureAwait(true);
								break;

							case newDraftCommand:
								usersSessions[userId] = new SessionState(new RequestDraft());
								await bot.SendTextMessageAsync(
									userId,
									"Новы чарнавік для заяўкі ў 115.бел створаны.\n" +
									"\n" +
									"Зараз у адвольным парадку ўвядзіце тэкставае апісанне, адпраўце мне каардынаты кропкі на карце і таксама файлы з фота.\n" +
									"\n" +
									"Тэкставае апісанне павінна быць не даўжэй за " + RequestDraft.MaxDescriptionLength + " сымбалаў. " +
									"Увесь тэкст, які Вы мне зараз надышлёце, будзе дададзены ў апісанне. І каментары файлаў будуць таксама дададзены ў апісанне.\n" +
									"\n" +
									descriptionOfProcessWithFilesUpload + "\n" +
									"\n" +
									"Каб адправіць каардынаты кропкі на карце, Вам патрэбна будзе ў меню пасля 📎 выбраць пункт \"Месцазнаходжанне\" ў ніжнім радку. " +
									"І з дапамогай карты, якую Вы пабачыце, выбраць кропку заяўкі.\n" +
									"",
									replyToMessageId: msg.MessageId).ConfigureAwait(true);
								break;

							case showCurrentDraftCommand:
								if (RequestDraftIsInProgress(userId, out currentDraft))
								{
									string pointOnTheMap;
									if (currentDraft.Location != null)
									{
										string latitude = currentDraft.Location.Latitude.ToString(CultureInfo.InvariantCulture);
										string longitude = currentDraft.Location.Longitude.ToString(CultureInfo.InvariantCulture);
										pointOnTheMap = "Кропка на карце: " + latitude + "," + longitude + "\n" +
										"https://www.google.com/maps/search/?api=1&query=" + latitude + "," + longitude + "\n" +
										"\n" +
										"https://yandex.ru/maps/?pt=" + longitude + "," + latitude + "&z=17\n";
									}
									else
									{
										pointOnTheMap = "Кропка на карце: невядома\n";
									}
									await bot.SendTextMessageAsync(
										userId,
										"На дадзены момент чарнавік такі:\n" +
										"Апісанне: " + currentDraft.Description.ToString() +
										"\n" +
										pointOnTheMap +
										"\n" +
										currentDraft.ListOfPhotos.Count + " фота глядзіце ніжэй",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
									foreach (Message photoMsg in currentDraft.ListOfPhotos)
									{
										await bot.CopyMessageAsync(
											userId, userId, photoMsg.MessageId,
											replyToMessageId: msg.MessageId).ConfigureAwait(true);
									}
									await bot.SendTextMessageAsync(
										userId,
										"-------------------",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);

									await SendToUserHowMuchRemainedToPopulate(userId, msg.MessageId, currentDraft, false).ConfigureAwait(true);
								}
								else
								{
									await bot.SendTextMessageAsync(
										userId,
										"На дадзены момент чарнавіка яшчэ няма.\n" +
										"\n" +
										"Каб пачаць стварэнне чарнавіка заяўкі - скарастайцеся камандай " + newDraftCommand + ".\n" +
										"\n" +
										"Дзякуй.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
								}
								break;

							case resetCurrentDescriptionCommand:
								if (RequestDraftIsInProgress(userId, out currentDraft))
								{
									currentDraft.Description.Clear();
									await bot.SendTextMessageAsync(
										userId,
										"Апісанне сцёрта.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);

									await SendToUserHowMuchRemainedToPopulate(userId, msg.MessageId, currentDraft, true).ConfigureAwait(true);
								}
								else
								{
									await bot.SendTextMessageAsync(
										userId,
										"На дадзены момент чарнавіка яшчэ няма.\n" +
										"\n" +
										"Каб пачаць стварэнне чарнавіка заяўкі - скарастайцеся камандай " + newDraftCommand + ".\n" +
										"\n" +
										"Дзякуй.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
								}
								break;

							case resetCurrentFilesCommand:
								if (RequestDraftIsInProgress(userId, out currentDraft))
								{
									currentDraft.ListOfPhotos.Clear();
									await bot.SendTextMessageAsync(
										userId,
										"Фота сцёрты з чарнавика.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);

									await SendToUserHowMuchRemainedToPopulate(userId, msg.MessageId, currentDraft, true).ConfigureAwait(true);
								}
								else
								{
									await bot.SendTextMessageAsync(
										userId,
										"На дадзены момент чарнавіка яшчэ няма.\n" +
										"\n" +
										"Каб пачаць стварэнне чарнавіка заяўкі - скарастайцеся камандай " + newDraftCommand + ".\n" +
										"\n" +
										"Дзякуй.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
								}
								break;

							case sendDraftToMikalaiCommand:
								if (RequestDraftIsInProgress(userId, out currentDraft))
								{
									if (IsRequestDraftContainsRequiredMinimum(currentDraft))
									{
										await bot.SendTextMessageAsync(
											userId,
											"Пачакайце, я зараз апрацоўваю Ваш чарнавік перад адпраўкай...",
											replyToMessageId: msg.MessageId).ConfigureAwait(true);

										ICard card = await incominRequestDraftsList.Cards.Add(
											"Telegram:" + userId + ":" + user.FirstName + " " + user.LastName,
											description: currentDraft.Description.ToString() +
												"\n" +
												"{" +
													currentDraft.Location.Latitude.ToString(CultureInfo.InvariantCulture) + "," +
													currentDraft.Location.Longitude.ToString(CultureInfo.InvariantCulture) +
												"}").ConfigureAwait(true);
										foreach (Message documentMessage in currentDraft.ListOfPhotos)
										{
											Document document = documentMessage.Document;
											File file = await bot.GetFileAsync(document.FileId).ConfigureAwait(true);
											Uri fileUri = new Uri($"https://api.telegram.org/file/bot{telegramBotToken}/{file.FilePath}");
											byte[] fileBytes = await httpClient.GetByteArrayAsync(fileUri).ConfigureAwait(true);
											await card.Attachments.Add(fileBytes, document.FileName).ConfigureAwait(true);
										}

										await bot.SendTextMessageAsync(
											userId,
											$"Ваш чарнавік адпраўлены {імяЎДавальнымСклоне}. Нумар чарнавіка: {card.Id}\n" +
											"(Захавайце гэты нумар у \"Захаванае\" Telegram, калі жадаеце ў будучыні даведвацца што сталася з гэтым чарнавіком.)\n" +
											"\n" +
											"Дзякуй Вам за неабыякавасць!\n" +
											"Жадаю Вам моцнага здароўя, прыемнага настрою і каб праблемы вырашаліся хутка!\n" +
											"❤",
											replyToMessageId: msg.MessageId).ConfigureAwait(true);

										SessionState tmp;
										usersSessions.TryRemove(userId, out tmp);
									}
									else
									{
										await SendToUserHowMuchRemainedToPopulate(userId, msg.MessageId, currentDraft, true).ConfigureAwait(true);
									}
								}
								else
								{
									await bot.SendTextMessageAsync(
										userId,
										"На дадзены момент чарнавіка яшчэ няма.\n" +
										"\n" +
										"Каб пачаць стварэнне чарнавіка заяўкі - скарастайцеся камандай " + newDraftCommand + ".\n" +
										"\n" +
										"Дзякуй.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
								}
								break;

							case showInfoAboutRequestDraft:
								SessionState currentSession;
								if (RequestDraftIsInProgress(userId, out currentDraft) && usersSessions.TryGetValue(userId, out currentSession))
								{
									currentSession.UserWantsHisDraftUpdates = true;
									await bot.SendTextMessageAsync(
										userId,
										"Вы зараз ствараеце новы чарнавік, але давайце зробім перапынак і паглядзім, што там з Вашым папярэднім чарнавіком. " +
										"Калі ласка, укажыце нумар, які Вы атрымалі пасля адпраўкі чарнавіка.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
								}
								else
								{
									usersSessions[userId] = new SessionState(true);
									await bot.SendTextMessageAsync(
										userId,
										"Калі ласка, укажыце нумар, які Вы атрымалі пасля адпраўкі чарнавіка.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
								}
								break;

							default:
								await bot.SendTextMessageAsync(
									userId,
									"Прабачце, я вельмі малады бот і, магчыма, просто неправільна разумею Вас.\n" +
									"\n" +
									"Такая каманда мне невядома.",
									replyToMessageId: msg.MessageId).ConfigureAwait(true);
								break;
						}
					}
					else //апрацоўка нейкага текста
					{
						SessionState currentSession;
						if (usersSessions.TryGetValue(userId, out currentSession))
						{
							if (currentSession.UserWantsHisDraftUpdates)
							{
								currentSession.UserWantsHisDraftUpdates = false;
								if (text.Length > 24 * 2)
								{
									await bot.SendTextMessageAsync(
										userId,
										"Мне непрыемна, што Вы дасылаеце мне нейкае тэкставае смецце замест нумара.\n" +
										"Калі ласка, не рабіце так.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
									break;
								}

								string draftId = text.Trim();
								if (draftId.Length == 24)
								{
									bool draftIsFound = false;
									ICard card = trelloFactory.Card(draftId);
									try
									{
										await card.Refresh().ConfigureAwait(true);
									}
									catch (TrelloInteractionException)
									{
									}
									if (card.List != null)
									{
										if (incomingRequestDarftsBoardColumns.ContainsKey(card.List.Id))
										{
											draftIsFound = true;
										}
									}

									if (draftIsFound)
									{
										StringBuilder sb = new StringBuilder();
										sb.Append("Чарнавік зараз у статусе \"");
										sb.Append(incomingRequestDarftsBoardColumns[card.List.Id]);
										sb.AppendLine("\"");
										await card.Comments.Refresh().ConfigureAwait(true);
										foreach (IAction action in card.Comments)
										{
											if (action.Type == ActionType.CommentCard)
											{
												sb.Append("каментар:");
												sb.AppendLine(action.Data.Text);
											}
										}
										await bot.SendTextMessageAsync(
											userId,
											sb.ToString(),
											replyToMessageId: msg.MessageId).ConfigureAwait(true);
									}
									else
									{
										await bot.SendTextMessageAsync(
											userId,
											"Чарнавік з такім нумарам не знойдзены. Мне шкада.",
											replyToMessageId: msg.MessageId).ConfigureAwait(true);
									}
								}
								else
								{
									await bot.SendTextMessageAsync(
										userId,
										"Гэта не нумар чарнавіка.",
										replyToMessageId: msg.MessageId).ConfigureAwait(true);
									break;
								}

								if (RequestDraftIsInProgress(userId, out currentDraft))
								{
									await SendToUserHowMuchRemainedToPopulate(userId, msg.MessageId, currentDraft, true).ConfigureAwait(true);
								}
								break;
							}
						}

						if (RequestDraftIsInProgress(userId, out currentDraft))
						{
							int currentDescriptionLength = currentDraft.Description.Length;
							int maxCharactersToUse = Math.Min(text.Length, RequestDraft.MaxDescriptionLength - currentDescriptionLength);
							if (maxCharactersToUse > 0)
							{
								currentDraft.Description.AppendLine(text.Substring(0, maxCharactersToUse).Trim());

								await SendToUserHowMuchRemainedToPopulate(userId, msg.MessageId, currentDraft, true).ConfigureAwait(true);
							}
							else
							{
								await bot.SendTextMessageAsync(
									userId,
									"Прабачце, я вельмі малады бот і, магчыма, просто неправільна разумею Вас.\n" +
									"\n" +
									"Мне здаецца, што Вы спрабуеце стварыць апісанне заяўкі для 115.бел даўжынёй болей за " + RequestDraft.MaxDescriptionLength + " сымбалаў." +
									"Нажаль, 115.бел не падтрымлівае такія доўгія апісанні.\n" +
									"\n" +
									"Каб даведацца што я запісаў як Ваша апісанне праблемы на дадзены момант - скарастайцеся камандай " + showCurrentDraftCommand + ".\n" +
									"Каб стварыць новае апісанне - скарастайцеся камандай " + resetCurrentDescriptionCommand + ".\n" +
									"Каб пачаць стварэнне чарнавіка заяўкі з самага пачатку - скарастайцеся камандай " + newDraftCommand + ".\n" +
									"\n" +
									"Дзякуй.",
									replyToMessageId: msg.MessageId).ConfigureAwait(true);
							}
						}
						else
						{
							await bot.SendTextMessageAsync(
								userId,
								"Прабачце, я вельмі малады бот і, магчыма, проста неправільна разумею Вас.\n" +
								"\n" +
								"Калі Вы спрабуеце стварыць чарнавік заяўкі для 115.бел, то пачніце з каманды " + newDraftCommand + ".\n" +
								"\n" +
								"Дзякуй.",
								replyToMessageId: msg.MessageId).ConfigureAwait(true);
						}
					}
					break;
			}
		}
	}
}