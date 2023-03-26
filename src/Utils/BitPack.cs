using System;

namespace FieldsOfSalt.Utils
{
	public unsafe ref struct PackedUshortArrayWriter
	{
		private readonly int width;
		private readonly uint mask;
		private readonly int outDataSize;
		private ulong buffer;
		private int bufferOffset;
		private int outDataOffset;
		private byte* outData;

		public PackedUshortArrayWriter(byte* outBuffer, int outBufferSize, ushort maxValue)
		{
			this.outData = outBuffer;
			this.outDataSize = outDataSize = outBufferSize;
			this.buffer = 0;
			this.bufferOffset = 0;
			this.outDataOffset = 0;

			BitPackUtil.CalcIntegerParams((uint)maxValue + 1, out width, out mask);
		}

		public void Write(ushort value)
		{
			ulong v = value == ushort.MaxValue ? mask : value;
			if(bufferOffset + width > 64)
			{
				if(bufferOffset < 64)
				{
					int w = 64 - bufferOffset;
					buffer |= v << bufferOffset;
					Flush();

					buffer |= v >> w;
					bufferOffset = width - w;

					return;
				}
				Flush();
				bufferOffset = 0;
			}
			buffer |= v << bufferOffset;
			bufferOffset += width;
		}

		public void Flush()
		{
			int count = Math.Min(8, outDataSize - outDataOffset);
			for(int i = 0; i < count; i++)
			{
				*outData = (byte)buffer;
				buffer >>= 8;
				outData++;
				outDataOffset++;
			}
		}
	}

	public unsafe ref struct PackedUshortArrayReader
	{
		private readonly int width;
		private readonly uint mask;
		private readonly int inDataSize;
		private ulong buffer;
		private int bufferOffset;
		private int inDataOffset;
		private byte* inData;

		public PackedUshortArrayReader(byte* inBuffer, int inBufferSize, ushort maxValue)
		{
			this.inData = inBuffer;
			this.inDataSize = inDataSize = inBufferSize;
			this.buffer = 0;
			this.bufferOffset = 0;
			this.inDataOffset = 0;

			BitPackUtil.CalcIntegerParams((uint)maxValue + 1, out width, out mask);
			Fill();
		}

		public ushort Read()
		{
			ulong v;
			if(bufferOffset + width > 64)
			{
				if(bufferOffset < 64)
				{
					int w = 64 - bufferOffset;
					v = buffer >> bufferOffset;
					Fill();

					v |= buffer << w;
					bufferOffset = width - w;

					v &= mask;
					return v == mask ? ushort.MaxValue : (ushort)v;
				}
				Fill();
				bufferOffset = 0;
			}
			v = buffer >> bufferOffset;
			bufferOffset += width;
			v &= mask;
			return v == mask ? ushort.MaxValue : (ushort)v;
		}

		private void Fill()
		{
			int count = Math.Min(8, inDataSize - inDataOffset);
			buffer = 0;
			for(int i = 0, b = 0; i < count; i++, b += 8)
			{
				buffer |= (ulong)*inData << b;
				inData++;
				inDataOffset++;
			}
		}
	}

	public static class BitPackUtil
	{
		public static int CalcBytesCount(uint maxValue, int count)
		{
			CalcIntegerParams(maxValue + 1, out var width, out _);
			if(count == 0 || width == 0) return 0;
			return (((width * count) - 1) >> 3) + 1;
		}

		public static void CalcIntegerParams(uint maxValue, out int width, out uint mask)
		{
			uint v = 1;
			int counter = 0;
			while(v <= maxValue)
			{
				v <<= 1;
				counter++;
			}
			width = counter;
			mask = v - 1;
		}
	}
}