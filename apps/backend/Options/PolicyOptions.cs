namespace EvCharge.Api.Options
{
    public class PolicyOptions
    {
        /// <summary>Max days ahead an Owner can book (inclusive).</summary>
        public int MaxBookingHorizonDays { get; set; } = 7;

        /// <summary>Modify/cancel lock window for Owner (hours before slot start).</summary>
        public int OwnerModifyLockHours { get; set; } = 12;

        /// <summary>Earliest check-in window for Operator (minutes before slot start).</summary>
        public int EarliestCheckInMinutes { get; set; } = 15;

        /// <summary>Latest check-in grace window after slot start + duration (minutes).</summary>
        public int LatestCheckInGraceMinutes { get; set; } = 15;

        /// <summary>Turn on background auto no-show sweep.</summary>
        public bool EnableNoShowSweeper { get; set; } = true;

        /// <summary>How often to run the auto no-show sweep (minutes).</summary>
        public int NoShowSweepIntervalMinutes { get; set; } = 5;

    }
}
