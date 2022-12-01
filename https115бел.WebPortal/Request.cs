using System;
using System.Collections.Generic;

namespace IWalkBy.https115бел.WebPortal
{
	public class Request
	{
		internal Attachment[] Attachments { get; private set; }

		public string CreatedOn { get; internal set; }

		public string Description { get; internal set; }

		public IList<HistoryEntry> History { get; internal set; }

		public string Id { get; internal set; }
		/// <summary>
		/// напрыклад "52,152039"
		/// </summary>
		public string Latitude { get; internal set; }

		public IList<Uri> ListOfOrganizationPhotos { get; internal set; }
		public IList<Uri> ListOfUserPhotos { get; internal set; }

		/// <summary>
		/// напрыклад "25,548084"
		/// </summary>
		public string Longitude { get; internal set; }
		public string ModifiedOn { get; internal set; }
		public string Number { get; internal set; }
		public string OrganizationComment { get; internal set; }
		public string Status { get; internal set; }
		public string StatusCode { get; internal set; }

		internal Request()
		{
		}

		public Request(
			string description,
			string latitude,
			string longitude,
			Attachment[] attachments)
		{
			if (latitude == null) throw new ArgumentNullException(nameof(latitude));
			if (longitude == null) throw new ArgumentNullException(nameof(longitude));

			this.Description = description;
			this.Latitude = latitude.Replace('.', ',');//53.897854 -> 53,897854
			this.Longitude = longitude.Replace('.', ',');//27.459566 -> 27,459566
			this.Attachments = attachments;
		}
	}
}