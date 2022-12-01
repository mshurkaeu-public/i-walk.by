using System;

namespace IWalkBy.https115бел.WebPortal
{
	public class RequestNotFoundException : Exception
	{
		public RequestNotFoundException()
		{
		}

		public RequestNotFoundException(string message) : base(message)
		{
		}

		public RequestNotFoundException(string message, Exception inner) : base(message, inner)
		{
		}
	}
}