// Downloaded from http://davy.landman.googlepages.com/IHashAlgorithm.cs

using System;

namespace HashTableHashing
{
	public interface IHashAlgorithm
	{
		UInt32 Hash(Byte[] data);
	}
	public interface ISeededHashAlgorithm : IHashAlgorithm
	{
		UInt32 Hash(Byte[] data, UInt32 seed);
	}
}
