using System;

namespace IWalkBy.https115бел.WebPortal
{
	internal static class TextUtils
	{
		public static string GetTextBetweenPrefixAndPostfix(string inText, string prefix, string postfix)
		{
			if (inText == null) throw new ArgumentNullException(nameof(inText));
			if (prefix == null) throw new ArgumentNullException(nameof(prefix));
			if (postfix == null) throw new ArgumentNullException(nameof(postfix));

			int startIndex = inText.IndexOf(prefix, StringComparison.Ordinal);
			if (startIndex == -1)
			{
				throw new NotImplementedException("The behavior for a case when prefix is not found is not defined yet");
			}
			
			int prefixLength = prefix.Length;
			int endIndex = inText.IndexOf(postfix, startIndex + prefixLength, StringComparison.Ordinal);
			if (endIndex == -1)
			{
				throw new NotImplementedException("The behavior for a case when postfix is not found is not defined yet");
			}
			
			string result = inText.Substring(startIndex + prefixLength, endIndex - startIndex - prefixLength);
			return result;
		}
	}
}