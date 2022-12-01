using System;

namespace IWalkBy.https115бел.WebPortal
{
	/// <summary>
	/// В Oracle APEX ёсць памылка з-за якой у адказе з сервера знікаюць 2 выпадковых сымбалы
	/// https://stackoverflow.com/questions/52076322/emoji-characters-breaking-the-apex-http-engine
	/// выглядае так, што гэта здараецца, калі ў тэксце есць эмодзі. Пакуль што не вельмі зразумела
	/// як дакладна адсачыць гэта здарэнне. Сымбалы могуць знікнуць і пашкодзіць HTML, а могуць знікнуць у тэксце...
	/// </summary>
	public class LooksLikeOracleApexReportBugException: Exception
	{
		public LooksLikeOracleApexReportBugException(): base()
		{
		}

		public LooksLikeOracleApexReportBugException(string message): base(message)
		{
		}

		public LooksLikeOracleApexReportBugException(string message, Exception internalException): base(message, internalException)
		{
		}
	}
}