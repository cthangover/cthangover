using System;

namespace Cthangover.Core.Settings
{

    [Serializable]
    public class TimeData
    {

        public long Tick;

        public int Years   { get; private set; }
        public int Months  { get; private set; }
        public int Days    { get; private set; }
        public int Hours   { get; private set; }
        public int Minutes { get; private set; }

        public TimeData(long time)
        {
            SetTime(time);
        }

        public bool IsMorning => Hours >= 6  && Hours < 10;
        public bool IsDay     => Hours >= 10 && Hours < 18;
        public bool IsEvening => Hours >= 18 && Hours < 22;
        public bool IsNight   => (Hours >= 22 && Hours < 24) || (Hours >= 0 && Hours < 6);

        public float Normalized => Hours + (Minutes / 60f);
        
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

        public string Text => $"{Hours:D2}:{Minutes:D2}";

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

        public void AddTick()
        {
            SetTime(++Tick);
        }

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
