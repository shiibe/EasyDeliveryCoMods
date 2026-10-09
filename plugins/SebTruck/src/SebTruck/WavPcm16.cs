using System;
using System.IO;
using System.Text;

namespace SebTruck
{
    internal static class WavPcm16
    {
        internal static float[] Read(byte[] bytes, out int channels, out int sampleRate)
        {
            channels = 0;
            sampleRate = 0;
            if (bytes == null || bytes.Length < 12 ||
                Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" ||
                Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
            {
                throw new InvalidDataException("Not a RIFF/WAVE file");
            }

            int riffSize = BitConverter.ToInt32(bytes, 4);
            if (riffSize < 4 || riffSize > bytes.Length - 8)
            {
                throw new InvalidDataException("Invalid RIFF size");
            }
            int end = riffSize + 8;
            int dataOffset = -1;
            int dataSize = 0;
            int blockAlign = 0;
            int pos = 12;
            while (pos < end)
            {
                if (end - pos < 8)
                {
                    throw new InvalidDataException("Truncated WAV chunk header");
                }
                string id = Encoding.ASCII.GetString(bytes, pos, 4);
                int size = BitConverter.ToInt32(bytes, pos + 4);
                int dataPos = pos + 8;
                if (size < 0 || size > end - dataPos)
                {
                    throw new InvalidDataException("Invalid WAV chunk size");
                }
                if (id == "fmt ")
                {
                    if (size < 16 || BitConverter.ToUInt16(bytes, dataPos) != 1 ||
                        BitConverter.ToUInt16(bytes, dataPos + 14) != 16)
                    {
                        throw new InvalidDataException("Only PCM16 WAV is supported");
                    }
                    channels = BitConverter.ToUInt16(bytes, dataPos + 2);
                    sampleRate = BitConverter.ToInt32(bytes, dataPos + 4);
                    blockAlign = BitConverter.ToUInt16(bytes, dataPos + 12);
                }
                else if (id == "data" && dataOffset < 0)
                {
                    dataOffset = dataPos;
                    dataSize = size;
                }
                pos = dataPos + size;
                if ((size & 1) != 0)
                {
                    if (pos == end) throw new InvalidDataException("Missing WAV chunk padding");
                    pos++;
                }
            }
            if (channels == 0 || sampleRate <= 0 || blockAlign != channels * 2 ||
                dataOffset < 0 || dataSize <= 0 || dataSize % blockAlign != 0)
            {
                throw new InvalidDataException("Invalid PCM16 format or data");
            }

            float[] data = new float[dataSize / 2];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = BitConverter.ToInt16(bytes, dataOffset + i * 2) / 32768f;
            }
            return data;
        }
    }
}
