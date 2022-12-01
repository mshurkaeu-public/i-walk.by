using IWalkBy.TextUtils;
using Manatee.Trello;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace IWalkBy.Trello
{
	public static class BoardsFinder
	{
		public static IList<IBoard> GetBoardsWithAGivenPurpose(string purpose, ITrelloFactory trelloFactory)
		{
			if (String.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("Мэта дошкі павінна быць указана", nameof(purpose));
			if (trelloFactory == null) throw new ArgumentNullException(nameof(trelloFactory));

			const string purposeKeyName = "мэта дошкі";
			List<IBoard> res = new List<IBoard>();

			IMe me = trelloFactory.Me().Result;
			me.Boards.Refresh().Wait();
			foreach (IBoard candidateBoard in me.Boards)
			{
				JObject jObject;
				if (JsonInDescription.MatchesTextualDescriptionFollowedByJson(candidateBoard.Description, out jObject))
				{
					if (jObject.ContainsKey(purposeKeyName) && (string)jObject[purposeKeyName] == purpose)
					{
						res.Add(candidateBoard);
					}
				}
			}

			return res;
		}

		public static IBoard GetTheBoardWithAGivenPurpose(string purpose, ITrelloFactory trelloFactory)
		{
			IList<IBoard> candidateBoards = GetBoardsWithAGivenPurpose(purpose, trelloFactory);

			if (candidateBoards == null || candidateBoards.Count == 0)
			{
				throw new NotImplementedException($"Не ведаю што рабіць, калі няма дошкі \"{purpose}\"");
			}
			if (candidateBoards.Count > 1)
			{
				throw new NotImplementedException($"Не ведаю што рабіць, калі ёсць некалькі дошак \"{purpose}\"");
			}

			IBoard res = candidateBoards[0];
			return res;
		}
	}
}