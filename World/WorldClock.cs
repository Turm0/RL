using System;

namespace RoguelikeEngine.World;

public enum Season : byte { Spring, Summer, Autumn, Winter }

public enum TimeOfDay : byte { Night, Dawn, Morning, Day, Afternoon, Dusk }

/// <summary>
/// Tracks in-game time. One real second = one in-game minute by default.
/// Provides time-of-day, day count, and season.
/// </summary>
public class WorldClock
{
    /// <summary>Real seconds per in-game minute.</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>How many in-game days make one season.</summary>
    public int DaysPerSeason { get; set; } = 30;

    /// <summary>Total elapsed in-game minutes.</summary>
    public double TotalMinutes { get; private set; }

    /// <summary>Current hour (0-23).</summary>
    public int Hour => (int)(TotalMinutes / 60.0) % 24;

    /// <summary>Current minute within the hour (0-59).</summary>
    public int Minute => (int)TotalMinutes % 60;

    /// <summary>Fractional hour (0.0 - 23.99) for smooth transitions.</summary>
    public float HourF => (float)(TotalMinutes % 1440.0 / 60.0);

    /// <summary>Elapsed in-game days (starting at day 1).</summary>
    public int Day => (int)(TotalMinutes / 1440.0) + 1;

    public Season Season
    {
        get
        {
            if (DaysPerSeason <= 0) DaysPerSeason = 30;
            int dayInYear = ((Day - 1) % (DaysPerSeason * 4));
            return (Season)(dayInYear / DaysPerSeason);
        }
    }

    public TimeOfDay TimeOfDay
    {
        get
        {
            int h = Hour;
            if (h < 5) return TimeOfDay.Night;
            if (h < 7) return TimeOfDay.Dawn;
            if (h < 10) return TimeOfDay.Morning;
            if (h < 16) return TimeOfDay.Day;
            if (h < 19) return TimeOfDay.Afternoon;
            if (h < 21) return TimeOfDay.Dusk;
            return TimeOfDay.Night;
        }
    }

    /// <summary>Normalized day progress (0.0 = midnight, 0.5 = noon).</summary>
    public float DayProgress => (float)(TotalMinutes % 1440.0 / 1440.0);

    public WorldClock(double startMinutes = 480.0) // default: 8:00 AM
    {
        TotalMinutes = startMinutes;
    }

    public void Update(float realDeltaSeconds)
    {
        if (TimeScale <= 0f) TimeScale = 1f;
        TotalMinutes += realDeltaSeconds / TimeScale;
    }

    public override string ToString() => $"Day {Day} {Hour:D2}:{Minute:D2} ({Season})";
}
