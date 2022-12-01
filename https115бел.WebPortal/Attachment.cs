namespace IWalkBy.https115бел.WebPortal
{
	public class Attachment
	{
		public string FileName { get; private set; }
		public string MimeType { get; private set; }
		internal byte[] Bytes { get; private set; }

		public Attachment(string fileName, string mimeType, byte[] bytes)
		{
			this.FileName = fileName;
			this.MimeType = mimeType;
			this.Bytes = bytes;
		}
	}
}