using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using IWalkBy.https115бел.WebPortal;
using IWalkBy.TextUtils;
using Manatee.Trello;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using TrelloAttachment = Manatee.Trello.Attachment;

namespace IWalkBy.https115белToTrelloSync
{
	public class CardFullUpdater
	{
		private Card card;
		private IEnvironmentOnComputer environmentOnComputer;

		private string _115белId;
		private Request request;

		private Dictionary<string, string> a115белPhotoNamesCache = new Dictionary<string, string>();
		private Dictionary<string, byte[]> photoBytesCache = new Dictionary<string, byte[]>();

		private const string _115белIdGroupName = "_115Id";
		private const string _115белNumberGroupName = "_115Number";

		private const double MAX_ALLOWED_DIFFERENCE_BETWEEN_IMAGES = 0.03;
		private const double WHEN_IMAGES_TREATED_FULLY_MATCHING = 0.005;

		#region Стандартныя фарматы імён картак
		//Калі заяўку адхіляюць на этапе папярэдняй мадэрацыі, то ў назве карткі павінен быць iдэнтыфикатар з 115.бел,
		//а перад ім "1:". Напрыклад: 1:16374907
		private const string _115IdFormat = "1:(?<" + _115белIdGroupName + @">\d+)";

		//Калі заяўка паспяхова прайшла папярэднюю мадэрацыю, то заяўка атрымлівае нумар выгляду 1248.1.250822,
		//дзе апошнія шэсць лічбаў гэта дзень, месяц і год, калі заяўка *прайшла мадэрацыю*. Здараецца, што заяўка
		//была створана карыстальнікам партала ў капярэдні дзень. Не ведаю, ці здараецца, яшчэ даўжэйшы "разрыў" паміж
		//датамі стварэння і мадэрацыі. Тэарэтычна гэта магчыма. 
		private const string _115NumberFormat = "(?<" + _115белNumberGroupName + @">\d+\.\d+\.\d{6})";

		//Калі заяўка была зачынена (значыць выканана або службы адмовіліся выконваць яе), то у назву карткі дадаецца дзень
		//і час, калі заяўка была зачынена. Дзень і час дадаецца ў фармаце, падыходзячым да сартыроўкі
		//yyyyMMddTHHmm. Таму што партал не паведамляе секунд закрыцця. Напрыклад 20221014T1425.
		private const string closedDateTimeFormat = @"(?<closedDateTime>\d{4}\d{2}\d{2}T\d{2}\d{2})";

		private static Regex standardRejectedRequestCardNameRegex = new Regex($"^{closedDateTimeFormat} {_115IdFormat}$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		private static Regex standardOnReviewRequestCardNameRegex = new Regex($"^{_115IdFormat}$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		private static Regex standardCreatedRequestCardNameRegex = new Regex($"^{_115IdFormat} {_115NumberFormat}$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		private static Regex standardClosedRequestCardNameRegex = new Regex($"^{closedDateTimeFormat} {_115IdFormat} {_115NumberFormat}$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		private static List<Regex> standardCardNameFormats = new List<Regex>(new Regex[] {
			standardRejectedRequestCardNameRegex,
			standardOnReviewRequestCardNameRegex,
			standardCreatedRequestCardNameRegex,
			standardClosedRequestCardNameRegex
		});
		#endregion

		#region Нестандартныя фарматы імен картак
		//напрыклад a:16323278
		private static Regex nonstandardIdCardNameRegex = new Regex($"^a:(?<" + _115белIdGroupName + @">\d+)$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		//напрыклад 2657.2.101022
		private static Regex nonstandardNumberOnlyCardNameRegex = new Regex($"^{_115NumberFormat}$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		//напрыклад 89.5.230822 - 2022-09-15
		private static Regex nonstandardNumberAndDateCardNameRegex = new Regex("^" + _115NumberFormat + @" - \d{4}-\d{2}-\d{2}$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		//напрыклад 1248.2.111022-1:16343333
		private static Regex nonstandardNumberAndIdCardNameRegex = new Regex($"^{_115NumberFormat}-{_115IdFormat}$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		private static List<Regex> nonStandardCardNameWithIdFormats = new List<Regex>(new Regex[] {
			nonstandardIdCardNameRegex,
			nonstandardNumberAndIdCardNameRegex
		});

		private static List<Regex> nonStandardCardNameWithNumberFormats = new List<Regex>(new Regex[] {
			nonstandardNumberOnlyCardNameRegex,
			nonstandardNumberAndDateCardNameRegex
		});
		#endregion

		public CardFullUpdater(Card card, IEnvironmentOnComputer environmentOnComputer)
		{
			this.card = card;
			this.environmentOnComputer = environmentOnComputer;
		}

		private string BuildJsonForCardDescription(JObject jObjectOnCard)
		{
			JObject newObject = new JObject();

			string latitude  = request.Latitude.Replace(",", ".", StringComparison.Ordinal);
			string longitude = request.Longitude.Replace(",", ".", StringComparison.Ordinal);
			newObject["c"] = $"{latitude},{longitude}";
			newObject["Google карты"] = $"https://www.google.com/maps/search/?api=1&query={latitude},{longitude}";
			newObject["Яндекс карты"] = $"https://yandex.ru/maps/?pt={longitude},{latitude}&z=17";
			if (jObjectOnCard != null && jObjectOnCard.ContainsKey("t"))
			{
				newObject["t"] = jObjectOnCard["t"];
			}
			newObject["1"] = _115белId;

			if (jObjectOnCard != null)
			{
				//скапіраваць астатнія Ўласцівасці з аб'екта на Trello картцы
				foreach (JProperty p in jObjectOnCard.Properties())
				{
					string propNameOnCard = p.Name;
					string newPropName;
					if (propNameOnCard == "каардынаты") newPropName = "c";
					else if (propNameOnCard == "a")     newPropName = "1";
					else                                newPropName = propNameOnCard;

					if (!newObject.ContainsKey(newPropName))
					{
						newObject[newPropName] = jObjectOnCard[propNameOnCard];
					}
				}
			}

			string json = SerializeJObjectToPretyString(newObject);
			return json;
		}

		private static string CalculateChecksumForAttachmentJObject(IAttachment attachment, JObject jObject)
		{
			JObject etalonObject = new JObject();
			etalonObject["1:i"] = jObject["1:i"];
			etalonObject["1:n"] = jObject["1:n"];
			etalonObject["1:u"] = jObject["1:u"];

			string etalonString = "v1" +
				"\n" +
				attachment.Id +
				"\n" +
				etalonObject.ToString(Formatting.Indented);

			HashTableHashing.IHashAlgorithm hashAlg = new HashTableHashing.MurmurHash2UInt32Hack();
			byte[] etalonBytes = Encoding.UTF8.GetBytes(etalonString);
			uint hash = hashAlg.Hash(etalonBytes);
			string res = hash.ToString("X", CultureInfo.InvariantCulture);
			return res;
		}

		private static void CalculateHistogramForPhoto(Mat photoMat, Mat result)
		{
				using (VectorOfMat vm = new VectorOfMat())
				{
					vm.Push(photoMat);
					CvInvoke.CalcHist(
						vm,
						new int[] {0},
						null,
						result,
						new int[] {50},
						new float[] {0, 256},
						false
					);
				}
				CvInvoke.Normalize(result, result, 0, 1, NormType.MinMax);
		}

		private IAttachment CreateNewAttachement(string filePath, string attachmentName)
		{
			byte[] fileContent = File.ReadAllBytes(filePath);
			IAttachment res = card.Attachments.Add(fileContent, attachmentName).Result;
			return res;
		}

		private static string ExtractPhotoIdFrom115белPhotoUrl(string a115белPhotoUriAsString)
		{
			//прыклад: https://disp.it-minsk.by/app/eds/portal/i/download?token=IMGWEB&pid=3922652
			Regex a115белPhotoUriRegex = new Regex(@"^https://disp\.it-minsk\.by/app/eds/portal/i/download\?token=IMGWEB&pid=(?<photoId>\d+)$",
				RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
			Match match = a115белPhotoUriRegex.Match(a115белPhotoUriAsString);
			if (!match.Success)
			{
				throw new NotImplementedException();
			}

			string res = match.Groups["photoId"].Value;
			return res;
		}

		private string FindOriginalPhotoOnAHardDrive(
			Uri a115белPhotoUri, Browser a115белBrowser,
			DateTime a115белRequestCreatedOn)
		{
			DateTime searchAmongPhotosToDate = a115белRequestCreatedOn.AddMinutes(1);// таму што час стварэння заяўкі с дакладнасцю да хвіліны
			DateTime searchAmongPhotosFromDate = searchAmongPhotosToDate.AddDays(-20);// мой вядомы максімум 2021-04-14 - 2021-03-27 = 19 дзён
			DirectoryInfo di = new DirectoryInfo(environmentOnComputer.PathToOriginalPhotosFolder);
			List<KeyValuePair<FileInfo, DateTime>> candidates = new List<KeyValuePair<FileInfo, DateTime>>();
			foreach (FileInfo candidateFile in di.GetFiles())
			{
				string candidatePhotoPath = candidateFile.FullName;
				IReadOnlyList<MetadataExtractor.Directory> metaDataDirectories = ImageMetadataReader.ReadMetadata(candidatePhotoPath);
				bool exifDatePresents = false;
				DateTime photoCreationDate = DateTime.MaxValue;
				foreach(MetadataExtractor.Directory d in metaDataDirectories)
				{
					if (d.GetType() == typeof(ExifSubIfdDirectory))
					{
						if (d.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out photoCreationDate))
						{
							exifDatePresents = true;
						}
						break;
					}
				}
				if (!exifDatePresents)
				{
					// калі ў файле адсутнічае EXIF інфармацыя пра дату, то прыйдзецца праверыць такі файл
					photoCreationDate = candidateFile.CreationTime;
				}
				if (photoCreationDate <= searchAmongPhotosToDate &&
					photoCreationDate >= searchAmongPhotosFromDate)
				{
					candidates.Add(new KeyValuePair<FileInfo, DateTime>(candidateFile, photoCreationDate));
				}
			}
			// адсартаваць па памяншэнні
			candidates.Sort((a, b) => b.Value.CompareTo(a.Value));

			string res = null;
			double differenceWithBestMatchingFile = Double.MaxValue;
			foreach(KeyValuePair<FileInfo, DateTime> candidate in candidates)
			{
				FileInfo candidateFile = candidate.Key;
				double difference = MeasureDifferenceBetween115белPhotoAndPhotoOnADrive(
					a115белPhotoUri, a115белBrowser,
					candidateFile.FullName, candidateFile.Name);
				if (difference <= MAX_ALLOWED_DIFFERENCE_BETWEEN_IMAGES &&
				    difference < differenceWithBestMatchingFile)
				{
					differenceWithBestMatchingFile = difference;
					res = candidateFile.FullName;
					if (difference <= WHEN_IMAGES_TREATED_FULLY_MATCHING)
					{
						break;
					}
				}
			}
			return res;
		}

		private string Get115белPhotoFileName(Uri a115белPhotoUri, Browser a115белBrowser)
		{
			string res;
			string a115белPhotoUriAsString = a115белPhotoUri.AbsoluteUri;
			if (!a115белPhotoNamesCache.TryGetValue(a115белPhotoUriAsString, out res))
			{
				res = a115белBrowser.GetPhotoName(a115белPhotoUri);
				a115белPhotoNamesCache[a115белPhotoUriAsString] = res;
			}

			return res;
		}

		private byte[] GetTrelloAttachmentFileBytes(TrelloAttachment attachment, HttpClient trelloHttpClientToDownloadFiles)
		{
			byte[] res;
			string trelloPhotoUrl = attachment.Url;
			if (!photoBytesCache.TryGetValue(trelloPhotoUrl, out res))
			{
				res = trelloHttpClientToDownloadFiles.GetByteArrayAsync(new Uri(trelloPhotoUrl)).Result;
				photoBytesCache[trelloPhotoUrl] = res;
			}

			return res;
		}

		private string GetClosedOnDateAsStringForName()
		{
			DateTime closedOn = DateTime.ParseExact(request.ModifiedOn, //напрыклад "14.10.2022 14:25"
				"dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
			string closedOnAsString = closedOn.ToString("yyyyMMddTHHmm", //стане 20221014T1425
				CultureInfo.InvariantCulture);
			return closedOnAsString;
		}

		private static string GetOriginallyUploadedFileName(TrelloAttachment attachment)
		{
			string originallyUploadedFileUrl = attachment.Url;
			string res = originallyUploadedFileUrl.Substring(
				originallyUploadedFileUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);
			return res;
		}

		private double MeasureDifferenceBetween115белPhotoAndPhotoOnADrive(
			Uri a115белPhotoUri, Browser a115белBrowser,
			string originalPhotoBytesPath, string originalPhotoName)
		{
			byte[] a115белPhoto;
			string a115белPhotoUriAsString = a115белPhotoUri.AbsoluteUri;
			if (!photoBytesCache.TryGetValue(a115белPhotoUriAsString, out a115белPhoto))
			{
				a115белPhoto = a115белBrowser.GetPhotoBytes(a115белPhotoUri);
				photoBytesCache[a115белPhotoUriAsString] = a115белPhoto;
			}

			string a115белPhotoName = Get115белPhotoFileName(a115белPhotoUri, a115белBrowser);
			if (a115белPhotoName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			{
				throw new NotImplementedException();
			}

			string a115белPhotoPath = Path.Combine(
				environmentOnComputer.PathToTempFolder,
				"115_" + a115белPhotoName);
			SaveBytesAsNewFile(a115белPhoto, a115белPhotoPath);

			FileInfo a115белPhotoFile = new FileInfo(a115белPhotoPath);
			FileInfo originalPhotoFile = new FileInfo(originalPhotoBytesPath);
			if (a115белPhotoFile.Length == originalPhotoFile.Length)
			{
				byte[] a115белPhotoBytes = File.ReadAllBytes(a115белPhotoPath);
				byte[] originalPhotoBytes = File.ReadAllBytes(originalPhotoBytesPath);
				bool areEqual = true;
				for (int i=0; i<a115белPhotoBytes.Length; i++)
				{
					if (a115белPhotoBytes[i] != originalPhotoBytes[i])
					{
						areEqual = false;
						break;
					}
				}
				if (areEqual)
				{
					return 0;
				}
			}
			// Ступень супадзення двух фота значна лепшая калі яны аднолькавых памераў (шырыня і вышыня)
			// Каб найлепшым чынам прывесці памеры да адной велічыні трэба як мага тачней паўтарыць
			// працэс змены памераў зыходнага фота.

			string fileWhichWasSentToServerPath;
			// Калі зыходнае фота было заліта на сервер 115.бел праз мабільны дадатак 115.бел, то ў такім
			// разе назва файла на серверы мае спецыфічны выгляд.
			// Напрыклад, 15527221_210822_211038.jpg
			Regex a115белPhotoNameRegex = new Regex(@"^\d{8}_\d{6}_\d{6}\.jpg$",
				RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
			Match match = a115белPhotoNameRegex.Match(a115белPhotoName);
			if (match.Success && a115белPhotoName != originalPhotoName)
			{
				fileWhichWasSentToServerPath = ResizeJpgPhotoAsAndroidApplicationDoes(originalPhotoBytesPath);
			}
			else if (a115белPhotoName == originalPhotoName)
			{
				// Калі імя файла ў Trello і файла на серверы 115.бел супадаюць, тады, напэўна, гэты файл
				// быў заліты на сервер праз веб-браўзэр.
				fileWhichWasSentToServerPath = originalPhotoBytesPath;
			}
			else
			{
				// пакуль што будзем лічыць, што калі імя на серверы не аўтаматычна створаннае
				// і калі яно адрозніваецца ад імя кандыдата, то гэта не тое, што мы шукаем.
				return Double.MaxValue;
			}

			// зараз пераціснем фота падобным чынам, як гэта робіць сервер
			string trelloPhotoResizedAsByServerPath = ResizeJpgPhotoAs115белPortalDoes(fileWhichWasSentToServerPath);

			using (Mat a115белMatGrayscale = CvInvoke.Imread(a115белPhotoPath, ImreadModes.Grayscale))
			{
				using (Mat
					trelloMatGrayscale = CvInvoke.Imread(trelloPhotoResizedAsByServerPath, ImreadModes.Grayscale),
					a115белHistogram = new Mat(),
					trelloHistogram = new Mat())
				{
					CalculateHistogramForPhoto(a115белMatGrayscale, a115белHistogram);
					CalculateHistogramForPhoto(trelloMatGrayscale, trelloHistogram);

					double res = CvInvoke.CompareHist(a115белHistogram, trelloHistogram, HistogramCompMethod.Bhattacharyya);
					return res;
				}
			}
		}

		private double MeasureDifferenceBetween115белPhotoAndTrelloAttachment(
			Uri a115белPhotoUri, Browser a115белBrowser,
			TrelloAttachment attachment, HttpClient trelloHttpClientToDownloadFiles)
		{
			string trelloPhotoName = GetOriginallyUploadedFileName(attachment);
			// напрыклад IMG_20220914_190731.jpg
			if (!trelloPhotoName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
			{
				throw new NotImplementedException();
			}

			byte[] trelloPhoto = GetTrelloAttachmentFileBytes(attachment, trelloHttpClientToDownloadFiles);
			string trelloOriginalPhotoPath = Path.Combine(
				environmentOnComputer.PathToTempFolder,
				trelloPhotoName);
			SaveBytesAsNewFile(trelloPhoto, trelloOriginalPhotoPath);

			double res = MeasureDifferenceBetween115белPhotoAndPhotoOnADrive(
				a115белPhotoUri, a115белBrowser,
				trelloOriginalPhotoPath, trelloPhotoName);
			return res;
		}

		private void MoveCardToColumn(IList targetColumn)
		{
			if (card.List.Id != targetColumn.Id)
			{
				if (card.Board != null && targetColumn.Board == null)
				{
					//Патрэбна, каб у спіскаў была ўласцівасць Board.
					//Таму што ўласцівасць Board ёсць у картак, знойдзенных праз метад TrelloFactory.Search.
					//Выклік <c>card.List = targetColumn</c> прывядзе да таго, што ўнутры Manatee.Trello.dll
					//адбудзецца спроба зрабіць нешта накшкалт <c>card.Board = targetColumn.Board</c>...
					//І вось тут здарыцца непрыемны сюрпрыз...
					//'Manatee.Trello.ValidationException`1' occurred in Manatee.Trello.dll: ''' is not a valid value.'
					//at Manatee.Trello.Internal.Field`1.Validate(T value)
					//at Manatee.Trello.Internal.Field`1.set_Value(T value)
					targetColumn.Refresh().Wait();
				}
				card.List = targetColumn;
			}
		}

		private void RemoveMetaData(string folderPath, string photoName)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo();
			startInfo.FileName = environmentOnComputer.PathToExiftool;
			startInfo.WorkingDirectory = folderPath;
			startInfo.UseShellExecute = false;
			startInfo.RedirectStandardOutput = true;
			// выдаліць усе мета-дадзенныя і скапіраваць orientation з зыходнага файла
			startInfo.Arguments = $"-all= -tagsfromfile @ -orientation \".\\{photoName}\"";

			using (Process exiftool = Process.Start(startInfo))
			{
				exiftool.WaitForExit();

				if (exiftool.ExitCode != 0)
				{
					throw new NotImplementedException();
				}
			}
		}

		private void RemoveMetaDataAndReuploadAttachment(
			TrelloAttachment attachment,
			HttpClient trelloHttpClientToDownloadFiles,
			out IAttachment newAttachement)
		{
			string trelloPhotoName = GetOriginallyUploadedFileName(attachment);
			if (trelloPhotoName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			{
				throw new NotImplementedException();
			}

			byte[] trelloPhoto = GetTrelloAttachmentFileBytes(attachment, trelloHttpClientToDownloadFiles);

			string pathToTrelloPhotoFile = Path.Combine(
				environmentOnComputer.PathToTempFolder,
				trelloPhotoName);
			string pathToTrelloPhotoFileBackup = Path.Combine(
				environmentOnComputer.PathToTempFolder,
				trelloPhotoName + "_original");

			if (File.Exists(pathToTrelloPhotoFile)) File.Delete(pathToTrelloPhotoFile);
			if (File.Exists(pathToTrelloPhotoFileBackup)) File.Delete(pathToTrelloPhotoFileBackup);

			SaveBytesAsNewFile(trelloPhoto, pathToTrelloPhotoFile);

			RemoveMetaData(environmentOnComputer.PathToTempFolder, trelloPhotoName);

			FileInfo modifiedFile = new FileInfo(pathToTrelloPhotoFile);
			if (modifiedFile.Length == trelloPhoto.Length)
			{
				// файл не змяніўся
				newAttachement = attachment;
				return;
			}
			else if (modifiedFile.Length > trelloPhoto.Length)
			{
				throw new NotImplementedException();
			}

			newAttachement = CreateNewAttachement(pathToTrelloPhotoFile, attachment.Name);

			attachment.Delete().Wait();
		}

		private string ResizeJpgPhotoAs115белPortalDoes(string originalPhotoPath)
		{
			/*
			List<Inter> modes = new List<Inter>();
			//modes.Add(Inter.Area);//<------- лепшае супадзенне для некаторых
			//modes.Add(Inter.Cubic);
			//modes.Add(Inter.Lanczos4);
			//modes.Add(Inter.Linear);
			modes.Add(Inter.LinearExact);//<------- лепшае супадзенне для некаторых
			//modes.Add(Inter.Nearest);
			//modes.Add(Inter.NearestExact);

			List<int> qualities = new List<int>();
			//qualities.Add(100);
			//qualities.Add(95);
			//qualities.Add(90);
			//qualities.Add(85);
			//qualities.Add(80);
			//qualities.Add(76);
			//qualities.Add(74);
			qualities.Add(75);// <----- поўнае супадзенне для _4-800x600-galaxy-space-stars-5k.jpg
			//qualities.Add(70);
			//qualities.Add(65);
			//qualities.Add(60);
			//qualities.Add(55);
			//qualities.Add(50);

			foreach(Inter mode in modes) foreach(int quality in qualities)
			{

				using (Mat
					matAsWasSentToServer = CvInvoke.Imread(originalPhotoPath),
					matResizedAsServerDoes = new Mat())
				{
					Size newSize = Size.Empty;
					newSize.Height = a115белMatGrayscale.Rows;
					newSize.Width = a115белMatGrayscale.Cols;
					CvInvoke.Resize(matAsWasSentToServer, matResizedAsServerDoes, newSize, 0, 0, mode);
					CvInvoke.Imwrite(resizedJpgPath, matResizedAsServerDoes,
						new KeyValuePair<ImwriteFlags, int>(ImwriteFlags.JpegQuality, quality));

				}
			}//foreach foreach
			*/

			string originalFileName = Path.GetFileNameWithoutExtension(originalPhotoPath);
			string resizedJpgPath = Path.Combine(
				environmentOnComputer.PathToTempFolder,
				originalFileName + "-resized-as-115-portal-does.jpg");
			if (File.Exists(resizedJpgPath))
			{
				#pragma warning disable CA1303
				Console.Write("-");
				#pragma warning restore CA1303
				return resizedJpgPath;
			}

			FileInfo originalFileInfo = new FileInfo(originalPhotoPath);
			if (originalFileInfo.Length <= 50000)
			{
				// калі файлы памерам 50000 або менш, то сервер нічога не робіць з імі
				originalFileInfo.CopyTo(resizedJpgPath, true);
				return resizedJpgPath;
			}

			string inOutFilePathPrefix = Path.Combine(
				environmentOnComputer.PathToTempFolder,
				"portal-files-in-out");
			if (System.IO.Directory.Exists(environmentOnComputer.PathToTempFolder) &&
			    !System.IO.Directory.Exists(inOutFilePathPrefix))
			{
				System.IO.Directory.CreateDirectory(inOutFilePathPrefix);
			}
			string inFilePath = Path.Combine(inOutFilePathPrefix, "original.jpg");
			string outFilePath = Path.Combine(inOutFilePathPrefix, "compressed.jpg");
			originalFileInfo.CopyTo(inFilePath, true);
			using (OracleConnection oracleConnection = new OracleConnection())
			{
				OracleConnectionStringBuilder connectionStringBuilder = new OracleConnectionStringBuilder();
				connectionStringBuilder.UserID = environmentOnComputer.OracleUserId;
				connectionStringBuilder.Password = environmentOnComputer.OraclePassword;
				connectionStringBuilder.DataSource = environmentOnComputer.OracleDataSource;

				oracleConnection.ConnectionString = connectionStringBuilder.ToString();

				oracleConnection.Open();

				string sql = @"
					DECLARE
						IMG ORDSYS.ORDIMAGE;
						ctx RAW(64) :=NULL;
					BEGIN
						IMG := ORDImage.init();
						DBMS_LOB.CreateTemporary(IMG.source.localdata, TRUE);
						IMG.importFrom(ctx,  'FILE', 'PORTAL_FILES_IN_OUT_DIR', 'original.jpg');

						IMG.process('maxScale=800 800');
						IMG.export(ctx, 'FILE', 'PORTAL_FILES_IN_OUT_DIR', 'compressed.jpg');
					END;";
				using (OracleCommand cmd = new OracleCommand(sql, oracleConnection))
				{
					cmd.ExecuteNonQuery();
				}
				oracleConnection.Close();
			}

			FileInfo compressedFileInfo = new FileInfo(outFilePath);
			compressedFileInfo.CopyTo(resizedJpgPath, true);
			return resizedJpgPath;
		}

		//d:\AndroidSDK\emulator\emulator.exe -avd Nexus_6_API_31
		private static bool BridgeAndAndroidAppAreInitialized;
		private string ResizeJpgPhotoAsAndroidApplicationDoes(string originalPhotoPath)
		{
			string originalFileName = Path.GetFileNameWithoutExtension(originalPhotoPath);
			// TODO: калі памеры <= 800px патрэбна вярнуць заходны файл як ёсць
			// if (Height <= 800 && Width <= 800) return originalPhotoPath;
			string resizedJpgPath = Path.Combine(
				environmentOnComputer.PathToTempFolder,
				originalFileName + "-resized-as-Android-app-does.jpg");
			if (File.Exists(resizedJpgPath))
			{
				#pragma warning disable CA1303
				Console.Write("+");
				#pragma warning restore CA1303
				return resizedJpgPath;
			}

			const string pathToWorkingDirectoryOnEmulator = "/data/user/0/by.IWalk.AndroidPhotoCompressor/app_ping-pong";
			const string pathToOriginalPhotoOnEmulator = pathToWorkingDirectoryOnEmulator + "/original.jpg";
			const string pathToCompressedPhotoOnEmulator = pathToWorkingDirectoryOnEmulator + "/compressed.jpg";
			const string pathToPingFileOnEmulator = pathToWorkingDirectoryOnEmulator + "/ping.txt";
			const string pathToPongFileOnEmulator = pathToWorkingDirectoryOnEmulator + "/pong.txt";

			string deviceReference = $"-s {environmentOnComputer.AndroidDeviceSerial}";

			List<string> commandsToAdbToExecute = new List<string>();
			if (!BridgeAndAndroidAppAreInitialized)
			{
				commandsToAdbToExecute.Add("root");
				commandsToAdbToExecute.Add($"{deviceReference} shell " +
					"currentProcess=$(pidof -s by.IWalk.AndroidPhotoCompressor); " +
					"if [[ -z \"$currentProcess\" ]]; then " + // калі наш дадатак не запушчаны, то
						$"touch {pathToPingFileOnEmulator}; " + // ствараем пусты ping файл
						"am start -n by.IWalk.AndroidPhotoCompressor/by.IWalk.AndroidPhotoCompressor.MainActivity; " + // запушчаем наш дадатак. Ён выдаліць ping файл
						$"while [[ -f {pathToPingFileOnEmulator} ]]; do " +
							"sleep 0.05s; " + // чакаем пакуль ping файл існуе
						"done; " +
					"fi;"
				);
			}
			// -f флаг каб ігнараваць выпадкі, калі файла няма
			commandsToAdbToExecute.Add($"{deviceReference} shell rm -f {pathToPongFileOnEmulator}");
			commandsToAdbToExecute.Add($"{deviceReference} push \"{originalPhotoPath}\" {pathToOriginalPhotoOnEmulator}");
			commandsToAdbToExecute.Add($"{deviceReference} shell " +
				$"touch {pathToPingFileOnEmulator}; " +
				$"until [[ -f {pathToPongFileOnEmulator} ]]; do " +
					"sleep 0.05s; " +
				"done;"
			);
			commandsToAdbToExecute.Add($"{deviceReference} pull {pathToCompressedPhotoOnEmulator} \"{resizedJpgPath}\"");
			commandsToAdbToExecute.Add($"{deviceReference} shell rm {pathToCompressedPhotoOnEmulator}");

			foreach(string command in commandsToAdbToExecute)
			{
				ProcessStartInfo startInfo = new ProcessStartInfo();
				startInfo.FileName = environmentOnComputer.PathToAndroidDebugBridge;
				startInfo.Arguments = command;
				startInfo.UseShellExecute = false;
				//каб артэфакты з кансолі новага працэса не пападалі ў кансоль гэтага працэса
				startInfo.RedirectStandardOutput = true;

				using (Process adbProcess = Process.Start(startInfo))
				{
					adbProcess.WaitForExit();
					if (adbProcess.ExitCode != 0)
					{
						throw new NotImplementedException();
					}
				}
			}
			BridgeAndAndroidAppAreInitialized = true;
			return resizedJpgPath;
		}

		private static void SaveBytesAsNewFile(byte[] bytes, string pathToFile)
		{
			using(FileStream fs = new FileStream(pathToFile, FileMode.Create))
			{
				using (BinaryWriter bw = new BinaryWriter(fs))
				{
					bw.Write(bytes);
				}
			}
		}

		private static void SendPutRequestToTrello(Uri trelloUri, JObject jObject)
		{
			string json = jObject.ToString(Formatting.None);

			HttpClient trelloHttpClient = IWalkBy.Trello.HttpClientFactory.GetSingletone();
			using (StringContent jsonContent = new StringContent(json, Encoding.UTF8, "application/json"))
			{
				trelloHttpClient.PutAsync(
					trelloUri,
					jsonContent).Wait();
			}
		}

		private static string SerializeJObjectToPretyString(JObject jObject)
		{
			using (StringWriter stringWriter = new StringWriter())
            {
				stringWriter.NewLine = "\n";
                using (JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter))
				{
					jsonWriter.CloseOutput = false;
					jsonWriter.Formatting = Formatting.Indented;
					jsonWriter.IndentChar = '\t';
					jsonWriter.Indentation = 1;

					JsonSerializer serializer = new JsonSerializer();
					serializer.Serialize(jsonWriter, jObject);
					string res = stringWriter.ToString();
					return res;
				}
            }
		}

		public CardUpdateResult TryFullUpdate(Browser a115белBrowser, BoardUpdater boardUpdater)
		{
			if (a115белBrowser == null) throw new ArgumentNullException(nameof(a115белBrowser));
			if (boardUpdater == null) throw new ArgumentNullException(nameof(boardUpdater));

			CardUpdateResult res = TryGet115белId(a115белBrowser);
			if (res != CardUpdateResult.Success) return res;

			res = TryGet115белRequest(a115белBrowser);
			if (res != CardUpdateResult.Success) return res;

			UpdateColumn(boardUpdater.RejectedList, boardUpdater.OnReviewList, boardUpdater.InWorkList, boardUpdater.ToCheckList);

			UpateName();

			res = TryUpdateDescription();
			if (res != CardUpdateResult.Success) return res;

			res = TryUpdateUserPhotos(a115белBrowser);
			if (res != CardUpdateResult.Success) return res;

			UpdateCardCover();

			UpdateOrganizationPhotos(a115белBrowser);

			res = TryUpdateCardComments();
			if (res != CardUpdateResult.Success) return res;

			return CardUpdateResult.Success;
		}

		private CardUpdateResult TryGet115белId(Browser _115белBrowser)
		{
			if (this._115белId != null) return CardUpdateResult.Success;

			string currentCardName = card.Name;
			foreach (Regex standardFormat in standardCardNameFormats)
			{
				Match match = standardFormat.Match(currentCardName);
				if (match.Success)
				{
					this._115белId = match.Groups[_115белIdGroupName].Value;
					return CardUpdateResult.Success;
				}
			}
			foreach (Regex nonstandardFormat in nonStandardCardNameWithIdFormats)
			{
				Match match = nonstandardFormat.Match(currentCardName);
				if (match.Success)
				{
					this._115белId = match.Groups[_115белIdGroupName].Value;
					return CardUpdateResult.Success;
				}
			}
			foreach(Regex nonstandardFormat in nonStandardCardNameWithNumberFormats)
			{
				Match match = nonstandardFormat.Match(currentCardName);
				if (match.Success)
				{
					string _115Number = match.Groups[_115белNumberGroupName].Value;
					FoundRequestInfo request;
					if (_115белBrowser.TryGetRequestByNumber(_115Number, out request))
					{
						this._115белId = request.Id;
						return CardUpdateResult.Success;
					}
					else
					{
						return CardUpdateResult.A115белIdNotFound;
					}
				}
			}

			return CardUpdateResult.NameFormatNotRecognized;
		}

		private CardUpdateResult TryGet115белRequest(Browser a115белBrowser)
		{
			if (this.request != null) return CardUpdateResult.Success;

			try
			{
				request = a115белBrowser.GetRequest(_115белId);
			}
			catch (RequestNotFoundException)
			{
				return CardUpdateResult.A115белRequestNotFound;
			}

			return CardUpdateResult.Success;
		}

		private CardUpdateResult TryUpdateCardComments()
		{
			CardUpdateResult res = CardUpdateResult.Success;
			if (request.History == null || request.History.Count == 0)
			{
				return res;
			}

			// спіс падзей, адбыўшыхся з заяўкай у 115.бел. Гэты спіс пастроены у парадку ад самай першай
			// падзеі, да самай апошняй (ад найдаўнейшай, да найсвяжэйшай)
			IList<HistoryEntry> a115белRequestHistory = request.History;

			List<IAction> cardCommentsWith115белRequestEvent = new List<IAction>();
			card.Comments.Refresh().Wait();
			// каментары ў картцы Trello ўпарадкаваны ад найсвяжэйшага, да найдаўнейшага.
			// Таму, каб параўноўваць з падзеямі заяўкі 115.бел, спатрэбіцца "перавярнуць" спіс.
			foreach(IAction cardHistoryAction in card.Comments)
			{
				if (cardHistoryAction.Type != ActionType.CommentCard)
				{
					continue;
				}

				// шукаю каментары выгляду
				// {
				// 	"хто": "Пользователь 115.бел",
				// 	"калі": "2022-09-15 12:27"
				// }
				// Поломана мусорка. Прошу починить и покрасить...
				string cardCommentText = cardHistoryAction.Data.Text;
				JObject jObject;
				if (JsonInDescription.MatchesJsonFollowedByTextualDescription(cardCommentText, out jObject))
				{
					if (jObject.ContainsKey("хто") && jObject.ContainsKey("калі"))
					{
						cardCommentsWith115белRequestEvent.Add(cardHistoryAction);
					}
				}
			}
			cardCommentsWith115белRequestEvent.Reverse();

			IEnumerator<IAction> historyEntryOnTrelloCardEnumerator = cardCommentsWith115белRequestEvent.GetEnumerator();
			foreach(HistoryEntry historyEntry in request.History)
			{
				const string whenFormat = "yyyy-MM-dd HH:mm";
				if (historyEntryOnTrelloCardEnumerator != null)
				{
					if (historyEntryOnTrelloCardEnumerator.MoveNext())
					{
						IAction entryOnCard = historyEntryOnTrelloCardEnumerator.Current;
						string entryOnCardText = entryOnCard.Data.Text;

						string textualDescription;
						JObject jObject;
						JsonInDescription.MatchesJsonFollowedByTextualDescription(
							entryOnCardText, out jObject, out textualDescription);

						bool entryOnCardMatchesRequestHistoryEntry =
							historyEntry.Who  == (string)jObject["хто"] &&
							historyEntry.When == DateTime.ParseExact((string)jObject["калі"], whenFormat, CultureInfo.InvariantCulture) &&
							historyEntry.Description == textualDescription;

						if (entryOnCardMatchesRequestHistoryEntry)
						{
							continue;
						}
						else
						{
							entryOnCard.Data.Text += "\n\n#**нешта не так з гэтым каментаром**#";
							return CardUpdateResult.CommentAboutRequestEventDoesntMatchExpectedValue;
						}
					}
					else
					{
						historyEntryOnTrelloCardEnumerator = null;
					}
				}

				JObject info = new JObject();
				info["хто"] = historyEntry.Who;
				info["калі"] = historyEntry.When.ToString(whenFormat, CultureInfo.InvariantCulture);
				string newCommentText = SerializeJObjectToPretyString(info) + "\n" +
					"\n" +
					historyEntry.Description;
				card.Comments.Add(newCommentText).Wait();
			}

			return res;
		}

		private CardUpdateResult TryUpdateUserPhotos(Browser a115белBrowser)
		{
			if (request.ListOfUserPhotos == null || request.ListOfUserPhotos.Count == 0)
			{
				return CardUpdateResult.Success;
			}

			// Зараз я ведаю, што ў заяўцы есць фота карыстальніка.
			// Патрэбна знайсці адпаведнасць паміж кожным фота з заяўкі і адным фота на картцы Trello.

			card.Attachments.Refresh().Wait();
			List<TrelloAttachment> candidatesFromCard = new List<TrelloAttachment>();
			foreach(TrelloAttachment attachment in card.Attachments)
			{
				if ((bool)attachment.IsUpload)
				{
					candidatesFromCard.Add(attachment);
				}
			}

			HttpClient trelloHttpClientToDownloadFiles = IWalkBy.Trello.HttpClientFactory.GetSingletone();

			int a115белPhotoOrder = 0;
			foreach(Uri a115белPhotoUri in request.ListOfUserPhotos)
			{
				a115белPhotoOrder++;
				string a115белPhotoFileName = a115белBrowser.GetPhotoName(a115белPhotoUri);
				TrelloAttachment bestMatchingAttachement = null;

				//Спачатку для кожнага з фота са 115 паспрабаваць знайсці фота на картцы ў адпаведнасці
				//з інфармацыяй у JSON у апісанні фота на картцы Trello.
				//if (bestMatchingAttachement == null)
				foreach(TrelloAttachment attachment in candidatesFromCard)
				{
					//шукаю карткі з JSON выгляду
					//{
					//	"1:i": "3922642",
					//	"1:n": "15527194_210822_210703.jpg",
					//	"1:u": "https://disp.it-minsk.by/app/eds/portal/i/download?token=IMGWEB&pid=3922642"
					//}
					JObject jObject;
					if (!JsonInDescription.MatchesTextualDescriptionFollowedByJson(attachment.Name, out jObject))
					{
						//на гэтым этапе прапускаю такія тыпы прычапленняў
						continue;
					}

					if (jObject.ContainsKey("1:n") && a115белPhotoFileName == (string)jObject["1:n"])
					{
						bestMatchingAttachement = attachment;
						break;
					}
				}

				//Калі не атрымалася знайсці супадзенні ў JSON, тады спрабуем знайсці спадзенні
				//ў імёнах файлаў на картцы Trello.
				if (bestMatchingAttachement == null)
				foreach(TrelloAttachment attachment in candidatesFromCard)
				{
					string candidateFileName = GetOriginallyUploadedFileName(attachment);
					if (candidateFileName == a115белPhotoFileName)
					{
						bestMatchingAttachement = attachment;
						break;
					}
				}

				//Калі не атрымалася знайсці супадзенні ў JSON і ў імёнах файлаў на картцы Trello,
				//тады шукаем па малюнку на фота
				double differenceWithBestMatchingAttachment = Double.MaxValue;
				if (bestMatchingAttachement == null)
				foreach(TrelloAttachment attachment in candidatesFromCard)
				{
					double difference = MeasureDifferenceBetween115белPhotoAndTrelloAttachment(
						a115белPhotoUri, a115белBrowser,
						attachment, trelloHttpClientToDownloadFiles);

					if (difference <= MAX_ALLOWED_DIFFERENCE_BETWEEN_IMAGES &&
					    difference < differenceWithBestMatchingAttachment)
					{
						differenceWithBestMatchingAttachment = difference;
						bestMatchingAttachement = attachment;
						if (difference <= WHEN_IMAGES_TREATED_FULLY_MATCHING)
						{
							//лічым, што супадзенне ўжо знойдзена
							break;
						}
					}
				}

				if (bestMatchingAttachement != null)
				{
					bool checksumOnAttachmentIsValid = ValidateChecksumOnAttachment(bestMatchingAttachement);
					// Калі праверачная сума правільная, то не патрэьна спампоўваць файл
					// каб параўнаць з фота з сервера 115.бел ці выдаліць мета-дадзеныя.
					// Правільная сума "сцвярджае", што ўсё гэта было зроблена для гэтага атачмента раней.

					if (!checksumOnAttachmentIsValid)
					{
						if (differenceWithBestMatchingAttachment > MAX_ALLOWED_DIFFERENCE_BETWEEN_IMAGES)
						{
							// гэта можа здарыцца калі кандыдат знойдзены праз параўнанне імён файлаў,
							// а розніца паміж кандыдатам і фота на серверы 115.бел яшчэ не вымяралася
							double difference = MeasureDifferenceBetween115белPhotoAndTrelloAttachment(
								a115белPhotoUri, a115белBrowser,
								bestMatchingAttachement, trelloHttpClientToDownloadFiles);
							if (difference > MAX_ALLOWED_DIFFERENCE_BETWEEN_IMAGES)
							{
								//трэба памеціць такую картку і спыніць апрацоўку карткі
								throw new NotImplementedException();
							}
						}

						IAttachment reUploadedAttachment;
						RemoveMetaDataAndReuploadAttachment(
							bestMatchingAttachement,
							trelloHttpClientToDownloadFiles,
							out reUploadedAttachment);

						if (reUploadedAttachment.Position != a115белPhotoOrder)
						{
							reUploadedAttachment.Position = a115белPhotoOrder;
						}

						UpdateAttachmentName(reUploadedAttachment, a115белPhotoUri, a115белBrowser);
					}

					// выдаляю гэты атачмент са спіска, каб ён не параўноўваўся з наступнымі
					// фота карыстальніка 115.бел
					candidatesFromCard.Remove(bestMatchingAttachement);

					continue;
				}

				// Калі зусім не атрымалася знайсці супадзенняў, тады трэба заліваць новае фота
				// на сервер, шукаючы па малюнку на фота сярод файлаў на кампутары
				// прыклад даты стварэння заяўкі - 12.10.2022 18:24
				DateTime requestWasCreatedOn = DateTime.ParseExact(request.CreatedOn, "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
				string pathToOriginalPhoto = FindOriginalPhotoOnAHardDrive(
					a115белPhotoUri, a115белBrowser,
					requestWasCreatedOn);
				if (pathToOriginalPhoto == null)
				{
					throw new NotImplementedException();
				}

				FileInfo originalPhoto = new FileInfo(pathToOriginalPhoto);
				FileInfo copyOfOriginalPhoto = originalPhoto.CopyTo(
					Path.Combine(environmentOnComputer.PathToTempFolder, originalPhoto.Name),
					true);
				RemoveMetaData(copyOfOriginalPhoto.DirectoryName, copyOfOriginalPhoto.Name);

				IAttachment newAttachement = CreateNewAttachement(copyOfOriginalPhoto.FullName, copyOfOriginalPhoto.Name);
				newAttachement.Position = a115белPhotoOrder;
				UpdateAttachmentName(newAttachement, a115белPhotoUri, a115белBrowser);
			}

			return CardUpdateResult.Success;
		}

		private CardUpdateResult TryUpdateDescription()
		{
			string descriptionOnCard = card.Description;
			string descriptionInRequest = request.Description;
			string newDescriptionForCard;

			if (String.IsNullOrWhiteSpace(descriptionOnCard))
			{
				newDescriptionForCard = descriptionInRequest;
			}
			else if (descriptionOnCard.StartsWith(descriptionInRequest, StringComparison.Ordinal))
			{
				newDescriptionForCard = descriptionOnCard;
			}
			else
			{
				//здараецца, што адзінае адрозненне гэта \n у Trello і \r\n у 115.бел
				string altDescriptionOnCard = descriptionOnCard.Replace("\n", "\r\n", StringComparison.Ordinal);
				if (altDescriptionOnCard.StartsWith(descriptionInRequest, StringComparison.Ordinal))
				{
					newDescriptionForCard = altDescriptionOnCard;
				}
				else
				{
					return CardUpdateResult.CardDescriptionDoesntMatchRequestDescription;
				}
			}

			Group jsonGroup;
			if (JsonInDescription.MatchesTextualDescriptionFollowedByJson(newDescriptionForCard, out jsonGroup))
			{
				if (jsonGroup.Index >= descriptionInRequest.Length)
				{
					//гэта азначае, што гэты JSON есць толькі ў Trello.
					//Такім чынам, можно змяняць яго, калі патрабуецца
					string jsonOnCard = jsonGroup.Value;
					JObject jObjectOnCard = JObject.Parse(jsonOnCard);

					string newJson = BuildJsonForCardDescription(jObjectOnCard);
					if (jsonOnCard != newJson)
					{
						newDescriptionForCard = String.Concat(newDescriptionForCard.AsSpan(0, jsonGroup.Index), newJson);
					}
				}
				else
				{
					//пакінем JSON як ёсць, каб апісанні ў 115.бел і Trello максымальна супадалі
				}
			}
			else
			{
				string newJson = BuildJsonForCardDescription(null);
				newDescriptionForCard += "\n\n" + newJson;
			}

			if (descriptionOnCard != newDescriptionForCard)
			{
				card.Description = newDescriptionForCard;
			}
			return CardUpdateResult.Success;
		}

		private void UpdateAttachmentName(
			IAttachment attachment,
			Uri a115белPhotoUri, Browser a115белBrowser)
		{
			string a115белPhotoUriAsString = a115белPhotoUri.AbsoluteUri;

			string a115белPhotoId = ExtractPhotoIdFrom115белPhotoUrl(a115белPhotoUriAsString);
			string a115белPhotoName = Get115белPhotoFileName(a115белPhotoUri, a115белBrowser);

			JObject oldJObject;
			JObject newJObject = new JObject();
			if (JsonInDescription.MatchesTextualDescriptionFollowedByJson(attachment.Name, out oldJObject))
			{
				if (
					!oldJObject.ContainsKey("1:i") &&
					!oldJObject.ContainsKey("1:n") &&
					!oldJObject.ContainsKey("1:u"))
				{
					newJObject["1:i"] = "";
					newJObject["1:n"] = "";
					newJObject["1:u"] = "";
				}

				foreach(JProperty p in oldJObject.Properties())
				{
					newJObject[p.Name] = p.Value;
				}
			}

			newJObject["1:i"] = a115белPhotoId;
			newJObject["1:n"] = a115белPhotoName;
			newJObject["1:u"] = a115белPhotoUriAsString;
			newJObject["iwb:c"] = CalculateChecksumForAttachmentJObject(attachment, newJObject);

			string newName = "фота карыстальніка №" + attachment.Position +
				"\n" +
				SerializeJObjectToPretyString(newJObject);

			if (attachment.Name != newName)
			{
				attachment.Name = newName;
			}
		}

		private void UpdateCardCover()
		{
			card.Attachments.Refresh().Wait();
			string attachmentToUseAsCover = null;
			foreach(TrelloAttachment candidate in card.Attachments)
			{
				if ((bool)candidate.IsUpload && (candidate.Position == 1))
				{
					attachmentToUseAsCover = candidate.Id;
					break;
				}
			}

			if (attachmentToUseAsCover != null)
			{
				JObject rootJObject = new JObject();
				JObject coverJObject = new JObject();
				rootJObject["cover"] = coverJObject;
				coverJObject["idAttachment"] = attachmentToUseAsCover;

				Uri cardUri = new Uri("https://trello.com/1/cards/" + card.Id);
				SendPutRequestToTrello(cardUri, rootJObject);
			}
		}

		private void UpdateColumn(
			IList rejectedColumn,
			IList onReviewColumn,
			IList inWorkColumn,
			IList toCheckColumn)
		{
			string currentColumnId = card.List.Id;
			switch (request.StatusCode)
			{
				case "-40": //Отклонено
					MoveCardToColumn(rejectedColumn);
					break;

				case "-20": //На рассмотрении
					MoveCardToColumn(onReviewColumn);
					break;


				case "10": //Новая заявка
				case "20": //Назначен исполнитель
				case "30": //Проведено обследование
				case "35": //В план текущего ремонта
					MoveCardToColumn(inWorkColumn);
					break;

				case "50": //Заявка закрыта
					//калі картка зачыненай заяўкі знаходзіцца ў нейкіх іншых калонках, то лічу, што карыстальнік сам яе туды
					//змясціў з нейкай мэтай
					if (
						currentColumnId == onReviewColumn.Id ||
						currentColumnId == inWorkColumn.Id)
					{
						MoveCardToColumn(toCheckColumn);
					}
					break;

				default:
					throw new NotImplementedException($"Не ведаю што рабіць для статуса заяўкі \"{request.StatusCode}\":\"{request.Status}\"");
			}
		}

		private void UpateName()
		{
			string standardName;
			string closedOnAsString;
			switch (request.StatusCode)
			{
				case "-40": //Отклонено
					closedOnAsString = GetClosedOnDateAsStringForName();
					standardName = $"{closedOnAsString} 1:{request.Id}";
					break;

				case "-20": //На рассмотрении
					standardName = $"1:{request.Id}";
					break;

				case "10": //Новая заявка
				case "20": //Назначен исполнитель
				case "30": //Проведено обследование
				case "35": //В план текущего ремонта
					standardName = $"1:{request.Id} {request.Number}";
					break;

				case "50": //Заявка закрыта
					closedOnAsString = GetClosedOnDateAsStringForName();
					standardName = $"{closedOnAsString} 1:{request.Id} {request.Number}";
					break;

				default:
					throw new NotImplementedException($"Не ведаю што рабіць для статуса заяўкі \"{request.StatusCode}\":\"{request.Status}\"");
			}

			if (card.Name != standardName)
			{
				card.Name = standardName;
			}
		}

		private void UpdateOrganizationPhotos(Browser a115белBrowser)
		{
			if (request.ListOfOrganizationPhotos == null || request.ListOfOrganizationPhotos.Count == 0)
			{
				return;
			}

			card.Attachments.Refresh().Wait();
			List<TrelloAttachment> candidatesFromCard = new List<TrelloAttachment>();
			foreach(TrelloAttachment attachment in card.Attachments)
			{
				if (!(bool)attachment.IsUpload)
				{
					candidatesFromCard.Add(attachment);
				}
				else if (attachment.Name.StartsWith("фота арганізацыі №", StringComparison.Ordinal))
				{
					// гэта на ўсялякі выпадак
					throw new NotImplementedException();
				}
			}

			int organizationPhotoNumber = 0;
			foreach(Uri organizationPhotoUri in request.ListOfOrganizationPhotos)
			{
				organizationPhotoNumber++;

				string organizationPhotoUriAsString = organizationPhotoUri.AbsoluteUri;
				TrelloAttachment matchingAttachment = null;

				foreach(TrelloAttachment candidateFromCard in candidatesFromCard)
				{
					if (candidateFromCard.Url == organizationPhotoUriAsString)
					{
						matchingAttachment = candidateFromCard;
						break;
					}
				}

				JObject newJObject = new JObject();
				newJObject["1:i"] = ExtractPhotoIdFrom115белPhotoUrl(organizationPhotoUriAsString);
				newJObject["1:u"] = organizationPhotoUriAsString;

				if (matchingAttachment == null)
				{
					string newName = "фота арганізацыі №" + organizationPhotoNumber +
						"\n" +
						SerializeJObjectToPretyString(newJObject);

					// 2022-10-27 на серверы Trello нейкі "баг або фіча". Спасылка на здымак ператвараецца ў
					// прычапленне з IsUpload==true. Гэта не тое, што патрэбна. Таму спачатку ствараю прычапленне, якое не
					// будзе ператворана ў спампаваны файл, а потым мяняю спасылку на сапраўдную
					IAttachment addedAttachment = card.Attachments.Add(
						"https://workaround-trello-bug.NotCom",
						newName).Result;
					Uri addedAttachmentUri = new Uri(
						"https://trello.com/1/cards/" + card.Id + "/attachments/" + addedAttachment.Id);
					JObject dataToSend = new JObject();
					dataToSend["url"] = organizationPhotoUriAsString;
					SendPutRequestToTrello(addedAttachmentUri, dataToSend);
				}
				else
				{
					JObject oldJObject;
					if (JsonInDescription.MatchesTextualDescriptionFollowedByJson(matchingAttachment.Name, out oldJObject))
					{
						foreach(JProperty p in oldJObject.Properties())
						{
							switch (p.Name)
							{
								case "1:i":
								case "1:u":
									continue;

								default:
									newJObject[p.Name] = p.Value;
									break;
							}
						}
					}

					string newName = "фота арганізацыі №" + organizationPhotoNumber +
						"\n" +
						SerializeJObjectToPretyString(newJObject);

					if (matchingAttachment.Name != newName)
					{
						matchingAttachment.Name = newName;
					}
					candidatesFromCard.Remove(matchingAttachment);
				}
			}
		}

		private static bool ValidateChecksumOnAttachment(TrelloAttachment attachment)
		{
			JObject jObject;
			if (JsonInDescription.MatchesTextualDescriptionFollowedByJson(attachment.Name, out jObject))
			{
				if (!jObject.ContainsKey("iwb:c"))
				{
					return false;
				}

				string providedChecksum = (string)jObject["iwb:c"];
				string calculatedChecksum = CalculateChecksumForAttachmentJObject(attachment, jObject);

				bool res = (providedChecksum == calculatedChecksum);
				return res;
			}
			else
			{
				return false;
			}
		}
	}
}