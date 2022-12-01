using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IWalkBy.https115бел.WebPortal
{
	/// <summary>
	/// Source code of the Dropzone plugin is here https://github.com/Dani3lSun/apex-plugin-dropzone
	/// </summary>
	public class DropzoneRegionPlugin
	{
		private static string allowedChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
		private static int maxIndexForAllowedChars = allowedChars.Length-1;

		private string oracleAjaxIdentifier;
		private string oracleRegionId;
		private Uri portalUri;
		private Uri refererUri;

		public DropzoneRegionPlugin(
			Uri portalUri,
			string oracleRegionId,
			Uri refererUri,
			string parentPageHtml)
		{
			this.portalUri = portalUri;
			this.oracleRegionId = oracleRegionId;
			this.refererUri = refererUri;
			string oracleAjaxIdentifierPrefix = "(function(){apexDropzone.pluginHandler(\"" + oracleRegionId + "\",{\"ajaxIdentifier\":\"";
			this.oracleAjaxIdentifier = TextUtils.GetTextBetweenPrefixAndPostfix(parentPageHtml, oracleAjaxIdentifierPrefix, "\"");
		}

		/// <summary>
		/// Not static in order to access Random instance from an instance of DropzoneRegionPlugin.
		/// This way if the DropzoneRegionPlugin instance is not accessed from different threads - less likely
		/// to get thread safety issues... yeah... poor design...
		/// </summary>
		public static string GenerateUniqueBoundaryString()
		{
			//this is how Chrome generates the boundary:
			//https://source.chromium.org/chromium/chromium/src/+/main:third_party/blink/renderer/platform/network/form_data_encoder.cc

			const int bufferSize = 16;
			byte[] randomBytes = RandomNumberGenerator.GetBytes(bufferSize);
			char[] buffer = new char[bufferSize];
			for (int i=0; i<bufferSize; i++)
			{
				int randomIndex = randomBytes[i] & maxIndexForAllowedChars;
				buffer[i] = allowedChars[randomIndex];
			}

			const string boundaryPrefix = "----WebKitFormBoundary";
			StringBuilder boundary = new StringBuilder(boundaryPrefix.Length + bufferSize);
			boundary.Append(boundaryPrefix);
			boundary.Append(buffer);
			return boundary.ToString();
		}

		private static StringContent BuildStringContent(string name, string value)
		{
			StringContent temp = null;
			StringContent res = null;
			try
			{
				temp = new StringContent(value);
				temp.Headers.ContentDisposition =  new ContentDispositionHeaderValue("form-data") {
						Name = "\"" + name + "\""
				};
				temp.Headers.ContentType = null;

				res = temp;
				temp = null;

				return res;
			}
			finally
			{
				#pragma warning disable CA1508
				if (temp != null)
				{
					temp.Dispose();
				}
				#pragma warning restore CA1508
			}
		}

		public HttpResponseMessage UploadFile(HttpClient httpClient, IList<KeyValuePair<string, string>> formFields, Attachment file)
		{
			if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
			if (formFields == null) throw new ArgumentNullException(nameof(formFields));
			if (file == null) throw new ArgumentNullException(nameof(file));

			httpClient.DefaultRequestHeaders.Add("Referer", refererUri.ToString());
			try
			{
				string uniqueBoundary = GenerateUniqueBoundaryString();
				using (MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent(uniqueBoundary))
				{
					//наступны радок патрэбен таму што .Net Framework дадае двайныя кавычкі вакол uniqueBoundary
					//А Oracle APEX памірае ад гэтага
					multipartFormDataContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=" + uniqueBoundary);
					multipartFormDataContent.Headers.Add("X-Requested-With", "XMLHttpRequest");

					formFields.Insert(0, new KeyValuePair<string, string>("p_request", "PLUGIN=" + oracleAjaxIdentifier));
					#pragma warning disable CA2000
					//Або ведаю як паправіць, або баг валідатара.
					//Сахраняць у лакальнуью пераменную і зануляць яе і правяраць спрабаваў - не працуе
					//sc = null; try{ sc = ...; do something; sc = null; } finally { if (sc != null) sc.Dispose(); }
					foreach(KeyValuePair<string, string> field in formFields)
					{
						multipartFormDataContent.Add(BuildStringContent(field.Key, field.Value));
					}

					StreamContent streamContent = new StreamContent(new MemoryStream(file.Bytes));
					streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") {
						Name = "\"F01\"",
						FileName = "\"" + file.FileName + "\""
					};
					streamContent.Headers.Add("Content-Type", file.MimeType);
					multipartFormDataContent.Add(streamContent);

					multipartFormDataContent.Add(BuildStringContent("X01", "UPLOAD"));
					multipartFormDataContent.Add(BuildStringContent("X02", file.FileName));
					multipartFormDataContent.Add(BuildStringContent("X03", file.MimeType));
					#pragma warning restore CA2000

					Uri wwv_flowAjaxUri = new Uri(portalUri, "wwv_flow.ajax");
					HttpResponseMessage res = httpClient.PostAsync(wwv_flowAjaxUri, multipartFormDataContent).Result;
					return res;
				}
			}
			finally
			{
				httpClient.DefaultRequestHeaders.Remove("Referer");
			}
		}
	}
}