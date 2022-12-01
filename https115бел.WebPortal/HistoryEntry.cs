using System;

namespace IWalkBy.https115бел.WebPortal
{
	public class HistoryEntry
	{
		public string Description { get; private set; }
		public DateTime When { get; private set; }
		public string Who { get; private set; }

		internal HistoryEntry(string who, DateTime when, string description)
		{
			this.Who = who;
			this.When = when;
			this.Description = description;
		}
	}
}