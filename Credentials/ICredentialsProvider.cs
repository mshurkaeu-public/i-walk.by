namespace IWalkBy.Credentials
{
	public interface ICredentialsProvider
	{
		string Get115белPassword();
		string Get115белUsername();

		string GetOracleDataSource();
		string GetOraclePassword();
		string GetOracleUserId();

		string GetTelegramBotTokenБоціка();

		string GetTrelloAppKey();
		string GetTrelloUserToken();
	}
}