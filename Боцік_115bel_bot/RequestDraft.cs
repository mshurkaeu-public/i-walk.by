using System.Collections.Generic;
using System.Text;
using Telegram.Bot.Types;

namespace IWalkBy.TelegramBots.Боцік_115bel_bot
{
	internal class RequestDraft
	{
		public const int MaxDescriptionLength = 1000;

		public List<Message> ListOfPhotos;
		public StringBuilder Description;
		public Location Location;

		public RequestDraft()
		{
			Description = new StringBuilder();
			ListOfPhotos = new List<Message>();
			this.Location = null;
		}
	}
}