using Vintagestory.API.Datastructures;

namespace FieldsOfSalt.Utils
{
	public static class TreeAttributeUtils
	{
		public static unsafe ushort[] ReadUShortArray(this ITreeAttribute tree, string key, ushort[] defaultValue = null)
		{
			var bytes = tree[key] as ByteArrayAttribute;
			if(bytes != null && (bytes.value.Length & 1) == 0)
			{
				int len = bytes.value.Length >> 1;
				var value = new ushort[len];
				fixed(byte* dPtr = bytes.value)
				{
					var dataPtr = dPtr;
					fixed(ushort* vPtr = value)
					{
						var valuePtr = vPtr;
						for(int i = 0; i < len; i++)
						{
							*valuePtr = *dataPtr;
							dataPtr++;

							*valuePtr = (ushort)(*dataPtr << 8);
							dataPtr++;

							valuePtr++;
						}
					}
				}
				return value;
			}
			return defaultValue;
		}

		public static unsafe void WriteUShortArray(this ITreeAttribute tree, string key, ushort[] value)
		{
			var bytes = new byte[value.Length << 1];
			int len = value.Length;
			fixed(byte* dPtr = bytes)
			{
				var dataPtr = dPtr;
				fixed(ushort* vPtr = value)
				{
					var valuePtr = vPtr;
					for(int i = 0; i < len; i++)
					{
						*dataPtr = (byte)*valuePtr;
						dataPtr++;

						*dataPtr = (byte)(*valuePtr >> 8);
						dataPtr++;

						valuePtr++;
					}
				}
			}
			tree[key] = new ByteArrayAttribute(bytes);
		}
	}
}