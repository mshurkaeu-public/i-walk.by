using Manatee.Trello;
using System;
using System.Globalization;
using System.Net.Http;

namespace IWalkBy.Trello
{
	public static class HttpClientFactory
	{
		private static readonly Lazy<HttpClient> myHttpClient = new Lazy<HttpClient>(
			() =>
			{
				HttpClient res = TrelloConfiguration.HttpClientFactory.Invoke();
				string authorizationHeader = String.Format(CultureInfo.InvariantCulture,
					"OAuth oauth_consumer_key=\"{0}\", oauth_token=\"{1}\"",
					Uri.EscapeDataString(TrelloAuthorization.Default.AppKey),
					Uri.EscapeDataString(TrelloAuthorization.Default.UserToken));
				res.DefaultRequestHeaders.Add("Authorization", authorizationHeader);
				return res;
			});

		#pragma warning disable CA1024
		public static HttpClient GetSingletone()
		#pragma warning restore CA1024
		{
			return myHttpClient.Value;
		}
	}
}