using System;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// In-game clock driven by a minute counter (<see cref="Tick"/>).
    /// Decomposes the total minutes into years, months, days, hours, and
    /// minutes, and exposes time-of-day convenience properties
    /// (<see cref="IsMorning"/>, <see cref="IsDay"/>, <see cref="IsEvening"/>,
    /// <see cref="IsNight"/>) that map to a <see cref="PhaseType"/>.
    /// The clock advances one minute per timer tick via
    /// <see cref="AddTick"/>, or can be jumped forward in bulk via
    /// <see cref="AddTime"/>. All arithmetic assumes 30-day months and
    /// 360-day years (12 months of 30 days) — the game does not simulate a real calendar.
    /// </summary>
    [Serializable]
    public class TimeData
    {
        /// <summary>Total elapsed in-game minutes. Incremented by
        /// <c>TimeTickController.OnTimerTick</c> (time_base mod) each timer cycle.</summary>
        public long Tick;

        // A simplified calendar: 30-day months, 12 months a year (360 days).
        // The year used to be 365 days in SetTime but 12 × 30 in AddTime, so the
        // tick and the components told different dates near the end of a year.
        private const long MinutesPerHour  = 60;
        private const long MinutesPerDay   = 24 * MinutesPerHour;
        private const long MinutesPerMonth = 30 * MinutesPerDay;
        private const long MinutesPerYear  = 12 * MinutesPerMonth;

        /// <summary>Year component extracted from <see cref="Tick"/>.</summary>
        public int Years   { get; private set; }
        /// <summary>Month component (0–11).</summary>
        public int Months  { get; private set; }
        /// <summary>Day component (0–29).</summary>
        public int Days    { get; private set; }
        /// <summary>Hour component (0–23).</summary>
        public int Hours   { get; private set; }
        /// <summary>Minute component (0–59).</summary>
        public int Minutes { get; private set; }

        /// <summary>Creates the clock seeded with an absolute minute value.</summary>
        /// <param name="time">Initial minute value to seed the clock.</param>
        public TimeData(long time)
        {
            SetTime(time);
        }

        /// <summary>True between 06:00 and 09:59 inclusive.</summary>
        public bool IsMorning => Hours >= 6  && Hours < 10;
        /// <summary>True between 10:00 and 17:59 inclusive.</summary>
        public bool IsDay     => Hours >= 10 && Hours < 18;
        /// <summary>True between 18:00 and 21:59 inclusive.</summary>
        public bool IsEvening => Hours >= 18 && Hours < 22;
        /// <summary>True between 22:00 and 05:59 inclusive (spans midnight).</summary>
        public bool IsNight   => (Hours >= 22 && Hours < 24) || (Hours >= 0 && Hours < 6);

        /// <summary>Floating-point hour of day (0.0–23.98), used for
        /// smooth interpolation of day/night visual effects. Equals
        /// <c>Hours + Minutes / 60f</c>.</summary>
        public float Normalized => Hours + (Minutes / 60f);
        
        /// <summary>Derived <see cref="PhaseType"/> based on the current hour.</summary>
        public PhaseType Phase
        {
            get
            {
                if (IsMorning)
                    return PhaseType.Morning;
                if (IsDay)
                    return PhaseType.Day;
                
                return IsEvening ? PhaseType.Evening : PhaseType.Night;
            }
        }

        /// <summary>Formatted clock string (e.g. "14:05").</summary>
        public string Text => $"{Hours:D2}:{Minutes:D2}";

        /// <summary>
        /// Sets the absolute time from a minute value. Decomposes
        /// <paramref name="time"/> into years/months/days/hours/minutes
        /// using the game's simplified calendar divisions:
        /// 60 min/hour, 1440 min/day, 43200 min/month, 518400 min/year.
        /// </summary>
        public void SetTime(long time)
        {
            Tick = time;

            Years   =  (int)(time / MinutesPerYear);
            time    %= MinutesPerYear;
            Months  =  (int)(time / MinutesPerMonth);
            time    %= MinutesPerMonth;
            Days    =  (int)(time / MinutesPerDay);
            time    %= MinutesPerDay;
            Hours   =  (int)(time / MinutesPerHour);
            time    %= MinutesPerHour;
            Minutes =  (int)time;
        }

        /// <summary>
        /// Advances the clock by exactly one minute. Called by
        /// <c>TimeTickController.OnTimerTick</c> (time_base mod) on each
        /// scene-event timer pulse.
        /// </summary>
        public void AddTick()
        {
            SetTime(++Tick);
        }

        /// <summary>
        /// Jumps the clock forward by the given amounts. Each unit is
        /// added to its respective component with carry propagation
        /// (60 min → 1 hour, 24 hours → 1 day, 30 days → 1 month,
        /// 12 months → 1 year). <see cref="Tick"/> is recalculated
        /// from the final components to stay consistent.
        /// </summary>
        public void AddTime(int years, int months, int days, int hours, int minutes)
        {
            Years   += years;
            Months  += months;
            Days    += days;
            Hours   += hours;
            Minutes += minutes;

            Hours   += Minutes / 60;
            Minutes %= 60;

            Days  += Hours / 24;
            Hours %= 24;

            Months += Days / 30;
            Days   %= 30;

            Years  += Months / 12;
            Months %= 12;

            Tick = Years * MinutesPerYear + Months * MinutesPerMonth + Days * MinutesPerDay + Hours * MinutesPerHour + Minutes;
        }

    }

}
