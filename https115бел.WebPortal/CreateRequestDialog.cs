using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace IWalkBy.https115бел.WebPortal
{
	public class CreateRequestDialog : OracleApexDialog
	{
		public CreateRequestDialog(Uri portalUri,
		                           string oracleApexAppId,
		                           string oracleApexSessionId,
		                           string oracleApexDialogChecksum):
			base(portalUri, oracleApexAppId, "9", oracleApexSessionId, oracleApexDialogChecksum)
		{
			//выглядае так, што гэтая налада ачышчае серверны кэш з ужо загружанымі файламі.
			//Без яе да заяўкі прычэпяцца ўсе файлы, якія загружаны на сервер і знаходзяцца ў тым кэшы.
			this.oracleApexClearCache = "RP,9";
		}

		public void CreateRequest(HttpClient httpClient, Request request)
		{
			if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
			if (request == null) throw new ArgumentNullException(nameof(request));

			string html = NavigateAndParse(httpClient);

			if (request.Attachments != null)
			{
				Uri thisPageUri = this.BuidPageUri();
				DropzoneRegionPlugin dropzoneRegionPlugin = new DropzoneRegionPlugin(
					portalUri,
					"R67032236963703517",
					thisPageUri,
					html);
				foreach(Attachment attachment in request.Attachments)
				{
					IList<KeyValuePair<string, string>> formFields = this.GetCommonFormFieldsValues();
					HttpResponseMessage uploadResult = dropzoneRegionPlugin.UploadFile(httpClient, formFields, attachment);
					if (uploadResult.StatusCode != HttpStatusCode.OK)
					{
						throw new NotImplementedException("Падтрымка кода " + uploadResult.StatusCode + " не зроблена.");
					}
					Console.WriteLine(attachment.FileName + " uploaded");
				}
			}

			JArray provideContacts = new JArray();
			provideContacts.Add("1");//каб не прадастаўляць кантакты - закаментуй гэты радок кода. Атрамаецца пусты масів provideContacts.
			string json = BuildJsonToPost(
				new KeyValuePair<string, object>("P9_REQUEST_TYPE", "2"),
				new KeyValuePair<string, object>("P9_REGION", "21"),
				new KeyValuePair<string, object>("P9_ADDRESS", ""),
				new KeyValuePair<string, object>("P9_ENTRANCE", ""),
				new KeyValuePair<string, object>("P9_FLOOR", ""),
				new KeyValuePair<string, object>("P9_APARTMENT", ""),
				new KeyValuePair<string, object>("P9_PROBLEM", request.Description),
				new KeyValuePair<string, object>("P9_LNG", request.Longitude),
				new KeyValuePair<string, object>("P9_LAT", request.Latitude),
				new KeyValuePair<string, object>("P9_TESTFU", ""),
				new KeyValuePair<string, object>("P9_PROVIDE_CONTACTS", provideContacts)
			);

			HttpResponseMessage httpResponseMessage = Submit(httpClient, "CreateRequest", "S", json).Result;

			string responseText = httpResponseMessage.Content.ReadAsStringAsync().Result;
			if (responseText != "{\"redirectURL\":\"javascript:apex.navigation.dialog.close(true,{dialogPageId: 9});\"}\n")
			{
				throw new NotImplementedException("Behavior for such response is not implemented");
			}
		}
	}
}