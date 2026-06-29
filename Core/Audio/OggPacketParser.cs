using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Audio
{
    /// <summary>
    /// Low-level Ogg Vorbis page parser. Reads raw OGG container bytes and
    /// produces an <c>OggPacketSequence</c> that Godot's
    /// <c>AudioStreamOggVorbis</c> can consume directly.
    /// Handles multi-page continuation (a packet split across page
    /// boundaries via the continuation flag), extracts granule positions
    /// for accurate seeking, and reads the Vorbis identification header
    /// to recover the sampling rate. Falls back to 44100 Hz if the header
    /// is absent or unparseable. Returns <c>null</c> on any corruption,
    /// logging the offset of the invalid page.
    /// </summary>
    public static class OggPacketParser
    {
        private const int OGG_PAGE_HEADER_SIZE = 27;

		/// <summary>
		/// Creates a new <c>AudioStreamOggVorbis</c> with audio data
		/// starting from the given time. Header packets (identification,
		/// comment, setup) are preserved. Granule positions are normalised
		/// so <c>Play(0)</c> starts at the target time without the O(N)
		/// seek penalty that <c>Play(fromPosition)</c> incurs on a full
		/// OGG stream.
		/// Returns <c>null</c> if the stream cannot be trimmed (e.g. not
		/// an OGG, too short, or corrupted).
		/// </summary>
		public static AudioStreamOggVorbis CreateTrimmedStream(
			AudioStream source, double startTimeSeconds)
		{
			if (source is not AudioStreamOggVorbis ogg || ogg.PacketSequence == null)
				return null;

			var seq = ogg.PacketSequence;
			var packetData = seq.PacketData;
			var granulePositions = seq.GranulePositions;
			var samplingRate = seq.SamplingRate;
			const int HEADER_COUNT = 3;

			if (packetData == null || packetData.Count <= HEADER_COUNT)
				return null;
			if (granulePositions == null || granulePositions.Length < packetData.Count)
				return null;
			if (startTimeSeconds <= 0 || samplingRate <= 0)
				return null;

			int startPacketIndex = -1;
			long baseGranule = 0;

			for (int i = HEADER_COUNT; i < packetData.Count; i++)
			{
				if (i >= granulePositions.Length)
					break;

				long granule = granulePositions[i];
				if (granule < 0)
					continue;

				double timeSeconds = (double)granule / samplingRate;
				if (timeSeconds >= startTimeSeconds)
				{
					startPacketIndex = i;
					baseGranule = granule;
					break;
				}
			}

			if (startPacketIndex < 0)
				return null;

			var trimmedPacketData = new Godot.Collections.Array<Godot.Collections.Array>();
			var trimmedGranulePositions = new long[packetData.Count - startPacketIndex + HEADER_COUNT];

			for (int i = 0; i < HEADER_COUNT; i++)
			{
				trimmedPacketData.Add(packetData[i]);
				trimmedGranulePositions[i] = 0;
			}

			for (int i = startPacketIndex; i < packetData.Count; i++)
			{
				int trimmedIdx = HEADER_COUNT + (i - startPacketIndex);
				trimmedPacketData.Add(packetData[i]);

				if (i < granulePositions.Length)
				{
					long granule = granulePositions[i];
					trimmedGranulePositions[trimmedIdx] = granule >= 0
						? granule - baseGranule
						: -1;
				}
			}

			var trimmedSeq = new OggPacketSequence();
			trimmedSeq.PacketData = trimmedPacketData;
			trimmedSeq.GranulePositions = trimmedGranulePositions;
			trimmedSeq.SamplingRate = samplingRate;

			var trimmedStream = new AudioStreamOggVorbis();
			trimmedStream.PacketSequence = trimmedSeq;
			return trimmedStream;
		}

		public static OggPacketSequence CreateFromOggBytes(byte[] data)
        {
            if (data == null || data.Length < OGG_PAGE_HEADER_SIZE)
            {
                GameLogger.Log("AUDIO", "OggPacketParser: data is null or too short", LogLevel.Error);
                return null;
            }

            try
            {
                var packets = new List<byte[]>();
                var granulePositions = new List<long>();
                int offset = 0;
                byte[] pendingPacket = null;
                float samplingRate = 0;
                bool gotSamplingRate = false;

                while (offset < data.Length)
                {
                    if (offset + OGG_PAGE_HEADER_SIZE > data.Length)
                        break;

                    if (!IsOggPage(data, offset))
                    {
                        GameLogger.Log("AUDIO", $"OggPacketParser: invalid page marker at offset {offset}", LogLevel.Error);
                        return null;
                    }

                    bool isContinued = (data[offset + 5] & 0x01) != 0;
                    long granulePos = ReadInt64LE(data, offset + 6);

                    int segmentCount = data[offset + 26];
                    int segTableOffset = offset + OGG_PAGE_HEADER_SIZE;

                    if (segTableOffset + segmentCount > data.Length)
                    {
                        GameLogger.Log("AUDIO", $"OggPacketParser: segment table exceeds data at offset {offset}", LogLevel.Error);
                        return null;
                    }

                    int segDataOffset = segTableOffset + segmentCount;
                    int segDataEnd = CalculateSegmentDataEnd(data, offset, segmentCount);

                    int packetsBefore = packets.Count;
                    packets.AddRange(ExtractPackets(
                        data, segTableOffset, segmentCount, segDataOffset,
                        ref pendingPacket, isContinued));
                    int packetsAdded = packets.Count - packetsBefore;

                    for (int i = 0; i < packetsAdded; i++)
                        granulePositions.Add(granulePos);

                    if (!gotSamplingRate && packets.Count >= 1)
                    {
                        samplingRate = ExtractSamplingRate(packets[0]);
                        gotSamplingRate = true;
                    }

                    offset = segDataEnd;
                }

                if (pendingPacket != null && pendingPacket.Length > 0)
                {
                    packets.Add(pendingPacket);
                    granulePositions.Add(granulePositions.Count > 0
                        ? granulePositions[granulePositions.Count - 1]
                        : 0);
                }

                if (packets.Count == 0)
                {
                    GameLogger.Log("AUDIO", "OggPacketParser: no packets extracted", LogLevel.Error);
                    return null;
                }

                if (granulePositions.Count == 0)
                {
                    GameLogger.Log("AUDIO", "OggPacketParser: no granule positions extracted", LogLevel.Error);
                    return null;
                }

                return BuildPacketSequence(packets, granulePositions, samplingRate);
            }
            catch (Exception ex)
            {
                GameLogger.Log("AUDIO", $"OggPacketParser: error: {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        private static bool IsOggPage(byte[] data, int offset)
        {
            return data[offset] == 0x4F && data[offset + 1] == 0x67
                && data[offset + 2] == 0x67 && data[offset + 3] == 0x53;
        }

        private static long ReadInt64LE(byte[] data, int offset)
        {
            return (long)data[offset]
                | ((long)data[offset + 1] << 8)
                | ((long)data[offset + 2] << 16)
                | ((long)data[offset + 3] << 24)
                | ((long)data[offset + 4] << 32)
                | ((long)data[offset + 5] << 40)
                | ((long)data[offset + 6] << 48)
                | ((long)data[offset + 7] << 56);
        }

        private static int CalculateSegmentDataEnd(byte[] data, int pageOffset, int segmentCount)
        {
            int segTableOffset = pageOffset + OGG_PAGE_HEADER_SIZE;
            int end = segTableOffset + segmentCount;
            for (int i = 0; i < segmentCount; i++)
                end += data[segTableOffset + i];
            return end;
        }

        private static float ExtractSamplingRate(byte[] firstPacket)
        {
            if (firstPacket == null || firstPacket.Length < 16)
                return 44100f;

            if (firstPacket[0] == 0x01
                && firstPacket[1] == 'v'
                && firstPacket[2] == 'o'
                && firstPacket[3] == 'r'
                && firstPacket[4] == 'b'
                && firstPacket[5] == 'i'
                && firstPacket[6] == 's')
            {
                return BitConverter.ToInt32(firstPacket, 12);
            }

            return 44100f;
        }

        private static List<byte[]> ExtractPackets(
            byte[] data, int segTableOffset, int segmentCount,
            int segDataOffset, ref byte[] pendingPacket, bool isContinued)
        {
            var packets = new List<byte[]>();

            if (!isContinued && pendingPacket != null && pendingPacket.Length > 0)
            {
                packets.Add(pendingPacket);
            }

            int currentOffset = segDataOffset;
            var currentPacketChunks = new List<byte[]>();

            if (isContinued && pendingPacket != null)
            {
                currentPacketChunks.Add(pendingPacket);
            }

            for (int i = 0; i < segmentCount; i++)
            {
                int segSize = data[segTableOffset + i];
                if (currentOffset + segSize > data.Length)
                    break;

                if (segSize > 0)
                {
                    var chunk = new byte[segSize];
                    Buffer.BlockCopy(data, currentOffset, chunk, 0, segSize);
                    currentPacketChunks.Add(chunk);
                    currentOffset += segSize;
                }

                bool isLastSegment = (i == segmentCount - 1);
                bool packetComplete = segSize < 255 || isLastSegment;

                if (packetComplete && currentPacketChunks.Count > 0)
                {
                    packets.Add(ConcatChunks(currentPacketChunks));
                    currentPacketChunks.Clear();
                }
            }

            pendingPacket = currentPacketChunks.Count > 0
                ? ConcatChunks(currentPacketChunks)
                : null;

            return packets;
        }

        private static byte[] ConcatChunks(List<byte[]> chunks)
        {
            if (chunks.Count == 1)
                return chunks[0];

            int total = 0;
            foreach (var c in chunks)
                total += c.Length;

            var result = new byte[total];
            int offset = 0;
            foreach (var c in chunks)
            {
                Buffer.BlockCopy(c, 0, result, offset, c.Length);
                offset += c.Length;
            }
            return result;
        }

        private static OggPacketSequence BuildPacketSequence(
            List<byte[]> packets, List<long> granulePositions, float samplingRate)
        {
            var packetArray = new Godot.Collections.Array<Godot.Collections.Array>();
            foreach (var packet in packets)
            {
                var inner = new Godot.Collections.Array();
                inner.Add(packet);
                packetArray.Add(inner);
            }

            var packetSequence = new OggPacketSequence();
            packetSequence.PacketData = packetArray;
            packetSequence.GranulePositions = granulePositions.ToArray();
            packetSequence.SamplingRate = samplingRate;
            return packetSequence;
        }
    }
}
