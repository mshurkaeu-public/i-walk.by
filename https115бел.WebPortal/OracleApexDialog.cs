using System;

namespace IWalkBy.https115бел.WebPortal
{
	/// <summary>
	/// Oracle APEX dialogs seems to be special pages with additional p_dialog_cs parameter in URL
	/// </summary>
	public class OracleApexDialog: OracleApexPage
	{
		protected string oracleApexDialogChecksum { get; private set; }

		public OracleApexDialog(Uri portalUri,
		                        string oracleApexAppId,
		                        string oracleApexPageId,
		                        string oracleApexSessionId,
		                        string oracleApexDialogChecksum)
			:base(portalUri, oracleApexAppId, oracleApexPageId, oracleApexSessionId)
		{
			this.oracleApexDialogChecksum = oracleApexDialogChecksum;
		}
		
		public override Uri BuidPageUri()
		{
			Uri regularPageUri = base.BuidPageUri();
			UriBuilder uriBuilder = new UriBuilder(regularPageUri);
			string currentQuery = uriBuilder.Query;
			if (currentQuery[0] != '?')
			{
				throw new NotImplementedException("Behavior for queries, not starting with '?' is not implemented");
			}
			//need to remove the leading '?' from query because of bug in UriBuilder class, which uncoditionally adds '?'
			//on each set of Query property
			uriBuilder.Query = String.Concat(uriBuilder.Query.AsSpan(1), "&p_dialog_cs=", oracleApexDialogChecksum);
			Uri dialogUri = uriBuilder.Uri;

			return dialogUri;
		}
	}
}