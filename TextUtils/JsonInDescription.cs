using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace IWalkBy.TextUtils
{
	public static class JsonInDescription
	{
		private static Regex JsonFollowedByTextualDescriptionRegex = new Regex(
			@"^\s*(?<json>\{.+\})\s*(?<textDescription>.*\S)\s*$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		//Вось як павінна выглядаць апісанне дашкі Trello каб я яго пазнаў:
		//колькі заўгодна прабельных сымбалаў у пачатку тэкста,
		//непустое тэкставае апісанне з непрабельным сымбалам у канцы гэтага апісання,
		//колькі заўгодна прабельных сымбалаў пасля тэкставага апісання перад JSON,
		//JSON,
		//колькі заўгодна прабельных сымбалаў у канцы апісанне дашкі Trello.
		private static Regex TextualDescriptionFollowedByJsonRegex = new Regex(
			@"^\s*(?<textDescription>.+\S)\s*(?<json>\{.+\})\s*$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

		public static bool MatchesJsonFollowedByTextualDescription(string text, out JObject jObject)
		{
			string description;
			bool res = MatchesRegexWithJsonAndDescriptionGroups(
				JsonFollowedByTextualDescriptionRegex, text,
				out description, out jObject);
			return res;
		}

		public static bool MatchesJsonFollowedByTextualDescription(
			string text, out JObject jObject, out string description)
		{
			bool res = MatchesRegexWithJsonAndDescriptionGroups(
				JsonFollowedByTextualDescriptionRegex, text,
				out description, out jObject);
			return res;
		}

		public static bool MatchesTextualDescriptionFollowedByJson(string text, out JObject jObject)
		{
			string description;
			bool res = MatchesRegexWithJsonAndDescriptionGroups(
				TextualDescriptionFollowedByJsonRegex, text,
				out description, out jObject);
			return res;
		}

		public static bool MatchesTextualDescriptionFollowedByJson(string text, out Group json)
		{
			Group descriptionGroup;
			bool res = MatchesRegexWithJsonAndDescriptionGroups(
				TextualDescriptionFollowedByJsonRegex, text,
				out descriptionGroup, out json);
			return res;
		}

		private static bool MatchesRegexWithJsonAndDescriptionGroups(
			Regex regex, string text, out Group description, out Group json)
		{
			Match match = regex.Match(text);
			if (match.Success)
			{
				description = match.Groups["textDescription"];
				json = match.Groups["json"];
				return true;
			}

			description = null;
			json = null;
			return false;
		}

		private static bool MatchesRegexWithJsonAndDescriptionGroups(
			Regex regex, string text, out string description, out JObject jObject)
		{
			Group descriptionGroup;
			Group jsonGroup;
			bool res = MatchesRegexWithJsonAndDescriptionGroups(regex, text, out descriptionGroup, out jsonGroup);
			if (res)
			{
				string json = jsonGroup.Value;
				JsonLoadSettings settings = new JsonLoadSettings();
				settings.CommentHandling = CommentHandling.Ignore;
				try
				{
					jObject = JObject.Parse(json, settings);
				}
				catch (JsonReaderException)
				{
					description = null;
					jObject = null;
					return false;
				}
				description = descriptionGroup.Value;
			}
			else
			{
				description = null;
				jObject = null;
			}

			return res;
		}
	}
}