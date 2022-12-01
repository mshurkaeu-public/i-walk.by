namespace IWalkBy.TelegramBots.Боцік_115bel_bot
{
	internal class SessionState
	{
		public RequestDraft RequestDraft;
		public bool UserWantsHisDraftUpdates;

		public SessionState(RequestDraft requestDraft)
		{
			this.RequestDraft = requestDraft;
			this.UserWantsHisDraftUpdates = false;
		}

		public SessionState(bool userWantsHisDraftUpdates)
		{
			this.RequestDraft = null;
			this.UserWantsHisDraftUpdates = userWantsHisDraftUpdates;
		}
	}
}