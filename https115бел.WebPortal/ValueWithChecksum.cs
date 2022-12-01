namespace IWalkBy.https115бел.WebPortal
{
	public class ValueWithChecksum
	{
		public string Value { get; private set; }
		public string Checksum { get; private set; }

		public ValueWithChecksum(string value, string checksum)
		{
			this.Value = value;
			this.Checksum = checksum;
		}
	}
}