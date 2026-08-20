namespace StreamOn.Core
{
    public readonly struct BroadcastResult
    {
        public readonly bool Succeeded;
        public readonly int Hits;
        public readonly int Misses;
        public readonly int SubscriberDelta;
        public readonly float MentalDelta;

        public BroadcastResult(bool succeeded, int hits, int misses, int subscriberDelta, float mentalDelta)
        {
            Succeeded = succeeded;
            Hits = hits;
            Misses = misses;
            SubscriberDelta = subscriberDelta;
            MentalDelta = mentalDelta;
        }
    }
}
