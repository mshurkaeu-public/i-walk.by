using IWalkBy.Credentials;
using IWalkBy.https115белToTrelloSync;
using System.IO;

namespace IWalkBy.ConsoleUtilities
{
	internal class EnvironmentOnComputer : IEnvironmentOnComputer
	{
		ICredentialsProvider credentialsProvider;

		internal EnvironmentOnComputer(ICredentialsProvider credentialsProvider)
		{
			this.credentialsProvider = credentialsProvider;
		}

		public string AndroidDeviceSerial
		{
			get
			{
				return "";
			}
		}

		public string OracleDataSource
		{
			get
			{
				string res = credentialsProvider.GetOracleDataSource();
				return res;
			}
		}

		public string OraclePassword
		{
			get
			{
				string res = credentialsProvider.GetOraclePassword();
				return res;
			}
		}

		public string OracleUserId
		{
			get
			{
				string res = credentialsProvider.GetOracleUserId();
				return res;
			}
		}

		public string PathToAndroidDebugBridge
		{
			get
			{
				return "";
			}
		}

		public string PathToExiftool
		{
			get
			{
				return "";
			}
		}

		public string PathToOriginalPhotosFolder
		{
			get
			{
				return "";
			}
		}

		public string PathToTempFolder
		{
			get
			{
				string res = Path.Combine(Directory.GetCurrentDirectory(), "temp");
				return res;
			}
		}
	}
}