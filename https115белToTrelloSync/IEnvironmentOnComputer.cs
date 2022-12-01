namespace IWalkBy.https115белToTrelloSync
{
	public interface IEnvironmentOnComputer
	{
		/// <summary>
		/// Каб высветліць серыйны нумар прылады карыстайся камандай
		/// <c>adb devices</c>
		/// </summary>
		string AndroidDeviceSerial { get; }

		string OracleDataSource { get; }

		string OraclePassword { get; }

		string OracleUserId { get; }

		string PathToAndroidDebugBridge { get; }

		string PathToExiftool { get; }

		string PathToOriginalPhotosFolder { get; }

		string PathToTempFolder { get; }
	}
}