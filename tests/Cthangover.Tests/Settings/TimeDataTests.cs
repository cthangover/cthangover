using Cthangover.Core.Settings;

namespace Engine.Tests.Settings
{
    public class TimeDataTests
    {
        [Fact]
        public void SetTime_Zero_AllComponentsZero()
        {
            var t = new TimeData(0);
            Assert.Equal(0L, t.Tick);
            Assert.Equal(0, t.Years);
            Assert.Equal(0, t.Months);
            Assert.Equal(0, t.Days);
            Assert.Equal(0, t.Hours);
            Assert.Equal(0, t.Minutes);
        }

        [Fact]
        public void SetTime_OneMinute()
        {
            var t = new TimeData(1);
            Assert.Equal(1L, t.Tick);
            Assert.Equal(1, t.Minutes);
            Assert.Equal(0, t.Hours);
        }

        [Fact]
        public void SetTime_OneHour()
        {
            var t = new TimeData(60);
            Assert.Equal(1, t.Hours);
            Assert.Equal(0, t.Minutes);
        }

        [Fact]
        public void SetTime_OneDay()
        {
            var t = new TimeData(1440);
            Assert.Equal(1, t.Days);
            Assert.Equal(0, t.Hours);
            Assert.Equal(0, t.Minutes);
        }

        [Fact]
        public void SetTime_OneMonth()
        {
            var t = new TimeData(43200);
            Assert.Equal(1, t.Months);
            Assert.Equal(0, t.Days);
            Assert.Equal(0, t.Hours);
        }

        [Fact]
        public void SetTime_OneYear()
        {
            var t = new TimeData(525600);
            Assert.Equal(1, t.Years);
            Assert.Equal(0, t.Months);
            Assert.Equal(0, t.Days);
            Assert.Equal(0, t.Hours);
        }

        [Fact]
        public void SetTime_Noon()
        {
            var t = new TimeData(720);
            Assert.Equal(12, t.Hours);
            Assert.Equal(0, t.Minutes);
        }

        [Fact]
        public void SetTime_DayBoundary_2359()
        {
            var t = new TimeData(1439);
            Assert.Equal(23, t.Hours);
            Assert.Equal(59, t.Minutes);
        }

        [Fact]
        public void SetTime_YearMonthDayBoundary()
        {
            var t = new TimeData(525600 + 43200 + 1440);
            Assert.Equal(1, t.Years);
            Assert.Equal(1, t.Months);
            Assert.Equal(1, t.Days);
            Assert.Equal(0, t.Hours);
            Assert.Equal(0, t.Minutes);
        }

        [Fact]
        public void SetTime_FullExample()
        {
            var t = new TimeData(525600 + 43200 + 1440 + 360 + 30);
            Assert.Equal(1, t.Years);
            Assert.Equal(1, t.Months);
            Assert.Equal(1, t.Days);
            Assert.Equal(6, t.Hours);
            Assert.Equal(30, t.Minutes);
        }

        [Fact]
        public void AddTick_SingleMinute()
        {
            var t = new TimeData(10);
            t.AddTick();
            Assert.Equal(11L, t.Tick);
            Assert.Equal(11, t.Minutes);
        }

        [Fact]
        public void AddTick_OverflowsHour()
        {
            var t = new TimeData(59);
            t.AddTick();
            Assert.Equal(1, t.Hours);
            Assert.Equal(0, t.Minutes);
        }

        [Fact]
        public void AddTick_OverflowsDay()
        {
            var t = new TimeData(1439);
            t.AddTick();
            Assert.Equal(1, t.Days);
            Assert.Equal(0, t.Hours);
            Assert.Equal(0, t.Minutes);
        }

        [Fact]
        public void AddTime_MinutesCarryToHours()
        {
            var t = new TimeData(0);
            t.AddTime(0, 0, 0, 0, 120);
            Assert.Equal(2, t.Hours);
            Assert.Equal(0, t.Minutes);
        }

        [Fact]
        public void AddTime_HoursCarryToDays()
        {
            var t = new TimeData(0);
            t.AddTime(0, 0, 0, 30, 0);
            Assert.Equal(1, t.Days);
            Assert.Equal(6, t.Hours);
        }

        [Fact]
        public void AddTime_DaysCarryToMonths()
        {
            var t = new TimeData(0);
            t.AddTime(0, 0, 35, 0, 0);
            Assert.Equal(1, t.Months);
            Assert.Equal(5, t.Days);
        }

        [Fact]
        public void AddTime_MonthsCarryToYears()
        {
            var t = new TimeData(0);
            t.AddTime(0, 14, 0, 0, 0);
            Assert.Equal(1, t.Years);
            Assert.Equal(2, t.Months);
        }

        [Fact]
        public void AddTime_AllCarriesSimultaneously()
        {
            var t = new TimeData(0);
            t.AddTime(0, 0, 0, 0, 125);
            Assert.Equal(2, t.Hours);
            Assert.Equal(5, t.Minutes);
        }

        [Fact]
        public void AddTime_TickRecalculated()
        {
            var t = new TimeData(0);
            t.AddTime(0, 0, 0, 1, 10);
            Assert.Equal(70L, t.Tick);
            Assert.Equal(1, t.Hours);
            Assert.Equal(10, t.Minutes);
        }

        [Fact]
        public void IsMorning_Boundaries()
        {
            Assert.False(new TimeData(359).IsMorning);
            Assert.True(new TimeData(360).IsMorning);
            Assert.True(new TimeData(599).IsMorning);
            Assert.False(new TimeData(600).IsMorning);
        }

        [Fact]
        public void IsDay_Boundaries()
        {
            Assert.False(new TimeData(599).IsDay);
            Assert.True(new TimeData(600).IsDay);
            Assert.True(new TimeData(1079).IsDay);
            Assert.False(new TimeData(1080).IsDay);
        }

        [Fact]
        public void IsEvening_Boundaries()
        {
            Assert.False(new TimeData(1079).IsEvening);
            Assert.True(new TimeData(1080).IsEvening);
            Assert.True(new TimeData(1319).IsEvening);
            Assert.False(new TimeData(1320).IsEvening);
        }

        [Fact]
        public void IsNight_EveningHours()
        {
            Assert.True(new TimeData(1320).IsNight);
            Assert.True(new TimeData(1439).IsNight);
        }

        [Fact]
        public void IsNight_MidnightSpan()
        {
            Assert.True(new TimeData(0).IsNight);
            Assert.True(new TimeData(359).IsNight);
            Assert.False(new TimeData(360).IsNight);
        }

        [Fact]
        public void Phase_MapsCorrectly()
        {
            Assert.Equal(PhaseType.Morning, new TimeData(360).Phase);
            Assert.Equal(PhaseType.Day, new TimeData(600).Phase);
            Assert.Equal(PhaseType.Evening, new TimeData(1080).Phase);
            Assert.Equal(PhaseType.Night, new TimeData(1320).Phase);
        }

        [Fact]
        public void Normalized_FloatingPointHour()
        {
            var t = new TimeData(750);
            Assert.Equal(12.5f, t.Normalized);
        }

        [Fact]
        public void Normalized_2359()
        {
            var t = new TimeData(1439);
            Assert.True(t.Normalized > 23.9f && t.Normalized < 24.0f);
        }

        [Fact]
        public void Text_Formatting()
        {
            Assert.Equal("14:05", new TimeData(845).Text);
            Assert.Equal("00:00", new TimeData(0).Text);
            Assert.Equal("23:59", new TimeData(1439).Text);
        }
    }
}
