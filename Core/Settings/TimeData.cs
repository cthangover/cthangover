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
    /// 365-day years — the game does not simulate a real calendar.
    /// </summary>
    [Serializable]
    public class TimeData
    {
        /// <summary>Total elapsed in-game minutes. Incremented by
        /// <see cref="TimeTickController.OnTimerTick"/> each timer cycle.</summary>
        public long Tick;

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
        /// 60 min/hour, 1440 min/day, 43200 min/month, 525600 min/year.
        /// </summary>
        public void SetTime(long time)
        {
            const int minutesPerHour  = 60;
            const int minutesPerDay   = 1440;
            const int minutesPerMonth = 43200;
            const int minutesPerYear  = 525600;

            Tick = time;

            Years   =  (int)(time / minutesPerYear);
            time    %= minutesPerYear;
            Months  =  (int)(time / minutesPerMonth);
            time    %= minutesPerMonth;
            Days    =  (int)(time / minutesPerDay);
            time    %= minutesPerDay;
            Hours   =  (int)(time / minutesPerHour);
            time    %= minutesPerHour;
            Minutes =  (int)time;
        }

        /// <summary>
        /// Advances the clock by exactly one minute. Called by
        /// <see cref="TimeTickController.OnTimerTick"/> on each
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

            Tick += years * 525600 + months * 43200 + days * 1440 + hours * 60 + minutes;
        }

    }

}
