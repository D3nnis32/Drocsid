namespace Drocsid.HenrikDennis2025.Core.Utilities;

public static class DistributedIdGenerator
{
    private static readonly long Epoch = new DateTime(2025, 1, 1).Ticks;
    private static readonly object Lock = new object();
    private static long lastTimestamp = 0L;
    private static int sequence = 0;
    private static readonly int NodeBits = 10;
    private static readonly int SequenceBits = 12;
    private static readonly int NodeId = System.Net.Dns.GetHostName().GetHashCode() % 1024;

    public static long GenerateId()
    {
        lock (Lock)
        {
            var timestamp = DateTime.UtcNow.Ticks - Epoch;

            if (timestamp < lastTimestamp)
            {
                throw new Exception("Clock moved backwards. Refusing to generate ID.");
            }

            if (lastTimestamp == timestamp)
            {
                sequence = (sequence + 1) & ((1 << SequenceBits) - 1);
                if (sequence == 0)
                {
                    // Wait until next millisecond
                    timestamp = WaitNextMillis(lastTimestamp);
                }
            }
            else
            {
                sequence = 0;
            }

            lastTimestamp = timestamp;
                
            long id = (timestamp << (NodeBits + SequenceBits))
                      | ((long)NodeId << SequenceBits)
                      | (long)sequence;
                
            return id;
        }
    }

    private static long WaitNextMillis(long lastTimestamp)
    {
        long timestamp = DateTime.UtcNow.Ticks - Epoch;
        while (timestamp <= lastTimestamp)
        {
            timestamp = DateTime.UtcNow.Ticks - Epoch;
        }
        return timestamp;
    }
}