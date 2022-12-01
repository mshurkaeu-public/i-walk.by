using IWalkBy.Credentials;
using System;
using System.Reflection;

namespace IWalkBy.ConsoleUtilities
{
	public class ArgumentsParser
	{
		private string[] copiedArguments;

		public ArgumentsParser(params string[] arguments)
		{
			if (arguments == null)
			{
				throw new ArgumentNullException(nameof(arguments));
			}

			copiedArguments = new string[arguments.Length];
			arguments.CopyTo(this.copiedArguments, 0);
		}

		public ICredentialsProvider GetCredentialsProvider()
		{
			string credentialsProviderName = GetParameterValue("ПастаўшчыкУліковыхДадзеных", "пуд");
			if (credentialsProviderName == null)
			{
				credentialsProviderName = "IWalkBy.Credentials.HardCodedCredentials, IWalkBy.Credentials, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
			}

			Type credentialsProviderType = Type.GetType(credentialsProviderName, true);
			ICredentialsProvider res = (ICredentialsProvider)Activator.CreateInstance(credentialsProviderType);
			return res;
		}

		public string GetParameterValue(params string[] parameterSynonims)
		{
			if (parameterSynonims == null) throw new ArgumentNullException(nameof(parameterSynonims));

			string[] prefixes = new string[parameterSynonims.Length];
			for (int i=0; i<parameterSynonims.Length; i++)
			{
				string synonim = parameterSynonims[i];
				string prefix = $"--{synonim}=";
				prefixes[i] = prefix;
			}

			string res = null;
			foreach(string argument in copiedArguments)
			{
				foreach(string prefix in prefixes)
				{
					if (argument.StartsWith(prefix, StringComparison.Ordinal))
					{
						res = argument.Substring(prefix.Length);
						break;
					}
				}
			}

			return res;
		}
	}
}