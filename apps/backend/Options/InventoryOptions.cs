namespace EvCharge.Api.Options
{
    public class InventoryOptions
    {
        /// <summary>How many days ahead to keep inventory generated.</summary>
        public int HorizonDays { get; set; } = 14;

        /// <summary>Background regeneration loop interval (minutes).</summary>
        public int RegenIntervalMinutes { get; set; } = 360; // 6 hours

        /// <summary>If true, try to heal capacity when schedule/overrides change (best-effort).</summary>
        public bool EnableHealing { get; set; } = true;
    }
}
