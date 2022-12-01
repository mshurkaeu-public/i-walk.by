using System;

namespace IWalkBy.Credentials
{
	public class HardCodedCredentials : ICredentialsProvider
	{
		private const string a115белPassword = "";
		private const string a115белUsername = "";

		private const string oracleDataSource = "";
		private const string oraclePassword = "";
		private const string oracleUserId = "";

		private const string telegramBotTokenБоціка = "";

		private const string trelloAppKey = "";
		private const string trelloUserToken = "";

		public string Get115белPassword()
		{
			if (String.IsNullOrWhiteSpace(a115белPassword))
			{
				throw new NotImplementedException();
			}

			return a115белPassword;
		}

		public string Get115белUsername()
		{
			if (String.IsNullOrWhiteSpace(a115белUsername))
			{
				throw new NotImplementedException();
			}

			return a115белUsername;
		}

		public string GetOracleDataSource()
		{
			if (String.IsNullOrWhiteSpace(oracleDataSource))
			{
				throw new NotImplementedException();
			}

			return oracleDataSource;
		}

		public string GetOraclePassword()
		{
			if (String.IsNullOrWhiteSpace(oraclePassword))
			{
				throw new NotImplementedException();
			}

			return oraclePassword;
		}

		public string GetOracleUserId()
		{
			if (String.IsNullOrWhiteSpace(oracleUserId))
			{
				throw new NotImplementedException();
			}

			return oracleUserId;
		}

		public string GetTelegramBotTokenБоціка()
		{
			if (String.IsNullOrWhiteSpace(telegramBotTokenБоціка))
			{
				throw new NotImplementedException();
			}

			return telegramBotTokenБоціка;
		}

		public string GetTrelloAppKey()
		{
			if (String.IsNullOrWhiteSpace(trelloAppKey))
			{
				throw new NotImplementedException();
			}

			return trelloAppKey;
		}

		public string GetTrelloUserToken()
		{
			if (String.IsNullOrWhiteSpace(trelloUserToken))
			{
				throw new NotImplementedException();
			}

			return trelloUserToken;
		}
	}
}