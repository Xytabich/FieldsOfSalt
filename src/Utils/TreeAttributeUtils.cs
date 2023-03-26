using Vintagestory.API.Datastructures;

namespace FieldsOfSalt.Utils
{
	public static class TreeAttributeUtils
	{
		public static unsafe ushort[] ReadPackedUShortArray(this ITreeAttribute tree, string key, ushort maxValue, int count, ushort[] defaultValue = null)
		{
			int byteSize = BitPackUtil.CalcBytesCount(maxValue, count);
			if(tree[key] is ByteArrayAttribute bytes && bytes.value.Length == byteSize)
			{
				var value = new ushort[count];
				fixed(byte* dataPtr = bytes.value)
				{
					fixed(ushort* vPtr = value)
					{
						var valuePtr = vPtr;
						var reader = new PackedUshortArrayReader(dataPtr, byteSize, maxValue);
						for(int i = 0; i < count; i++)
						{
							*valuePtr = reader.Read();
							valuePtr++;
						}
					}
				}
				return value;
			}
			return defaultValue;
		}

		public static unsafe void WritePackedUShortArray(this ITreeAttribute tree, string key, ushort[] value, ushort maxValue, int count)
		{
			if(value == null) return;
			int byteSize = BitPackUtil.CalcBytesCount(maxValue, count);
			var bytes = new byte[byteSize];
			fixed(byte* dataPtr = bytes)
			{
				fixed(ushort* vPtr = value)
				{
					var valuePtr = vPtr;
					var writer = new PackedUshortArrayWriter(dataPtr, byteSize, maxValue);
					for(int i = 0; i < count; i++)
					{
						writer.Write(*valuePtr);
						valuePtr++;
					}
					writer.Flush();
				}
			}
			tree[key] = new ByteArrayAttribute(bytes);
		}
	}
}