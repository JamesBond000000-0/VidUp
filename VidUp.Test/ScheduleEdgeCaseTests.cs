using System;
using System.Linq;
using Drexel.VidUp.Business;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Characterization tests for BUG-12: degenerate schedules and the end of month
    //correction in the monthly fallback path.
    [TestFixture]
    public class ScheduleEdgeCaseTests
    {
        [Test]
        public void Monthly_On31st_AskedAfterJanuary31st_ClampsToEndOfFebruary()
        {
            Schedule schedule = ScheduleTestData.CreateMonthlyDateBased(1, 31);
            DateTime result = schedule.GetNextDateTime(new DateTime(2026, 1, 31, 13, 0, 0), true);
            Assert.That(result, Is.EqualTo(new DateTime(2026, 2, 28, 12, 0, 0)));
        }

        [Test]
        public void Monthly_On30th_AskedAfterJanuary31st_ClampsToEndOfFebruary()
        {
            //the fallback into the next month must clamp as well (was 2026-03-02 before the fix)
            Schedule schedule = ScheduleTestData.CreateMonthlyDateBased(1, 30);
            DateTime result = schedule.GetNextDateTime(new DateTime(2026, 1, 31, 13, 0, 0), true);
            Assert.That(result, Is.EqualTo(new DateTime(2026, 2, 28, 12, 0, 0)));
        }

        [Test]
        public void Weekly_NoActiveDay_ThrowsInvalidSchedule()
        {
            //the UI prevents this state, JSON data can still contain it
            Schedule schedule = JsonConvert.DeserializeObject<Schedule>(
                "{\"scheduleFrequency\":2,\"startDate\":\"2026-01-05T00:00:00\",\"weeklyWeekFrequency\":1," +
                "\"weeklyDefaultTime\":\"12:00:00\",\"weeklyDays\":[false,false,false,false,false,false,false]}");
            Assert.Throws<InvalidScheduleException>(() => schedule.GetNextDateTime(new DateTime(2026, 1, 7), true));
        }

        [Test]
        public void MonthlyDateBased_NoActiveDay_ThrowsInvalidSchedule()
        {
            Schedule schedule = JsonConvert.DeserializeObject<Schedule>(
                "{\"scheduleFrequency\":3,\"startDate\":\"2026-01-01T00:00:00\",\"monthlyMonthFrequency\":1," +
                "\"monthlyMonthDateBased\":true,\"monthlyDefaultTime\":\"12:00:00\"," +
                "\"monthlyMonthDateBasedDates\":[" + string.Join(",", Enumerable.Repeat("false", 31)) + "]}");
            Assert.Throws<InvalidScheduleException>(() => schedule.GetNextDateTime(new DateTime(2026, 1, 7), true));
        }

        [Test]
        public void MonthlyRelative_NoCombinations_ThrowsInvalidSchedule()
        {
            //an empty combination list made the relative calculation recurse until
            //the process died with a stack overflow
            Schedule schedule = JsonConvert.DeserializeObject<Schedule>(
                "{\"scheduleFrequency\":3,\"startDate\":\"2026-01-01T00:00:00\",\"monthlyMonthFrequency\":1," +
                "\"monthlyMonthDateBased\":false,\"monthlyDefaultTime\":\"12:00:00\",\"monthlyMonthRelativeBasedCombinations\":[]}");
            Assert.Throws<InvalidScheduleException>(() => schedule.GetNextDateTime(new DateTime(2026, 1, 7), true));
        }
    }
}
