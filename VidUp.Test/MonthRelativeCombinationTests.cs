using System;
using System.Collections.Generic;
using Drexel.VidUp.Business;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Characterization tests for BUG-22: MonthRelativeCombination is a dictionary key,
    //so lookups must work by value even when the day times dictionary and the
    //combinations list hold different instances of the same combination.
    [TestFixture]
    public class MonthRelativeCombinationTests
    {
        [Test]
        public void Combination_UsesValueEquality()
        {
            MonthRelativeCombination first = new MonthRelativeCombination(DayPosition.Second, DayOfWeek.Friday);
            MonthRelativeCombination second = new MonthRelativeCombination(DayPosition.Second, DayOfWeek.Friday);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));

            Dictionary<MonthRelativeCombination, int> dictionary = new Dictionary<MonthRelativeCombination, int>();
            dictionary.Add(first, 42);
            Assert.That(dictionary[second], Is.EqualTo(42));
        }

        [Test]
        public void AdvancedRelative_LoadRoundtrip_CalculatesWithDayTimes()
        {
            //loading and copying a schedule with an advanced relative schedule must
            //resolve the day times for the combination (was KeyNotFoundException)
            Schedule schedule = ScheduleTestData.CreateMonthlyRelative(1, DayPosition.Second, DayOfWeek.Friday);
            schedule.MonthlyHasAdvancedSchedule = true;
            schedule.MonthlyMonthRelativeBasedSetDayTime(DayPosition.Second, DayOfWeek.Friday, 0, new TimeSpan(8, 30, 0));

            Schedule loaded = JsonConvert.DeserializeObject<Schedule>(JsonConvert.SerializeObject(schedule));

            DateTime result = loaded.GetNextDateTime(new DateTime(2026, 1, 20, 9, 0, 0), true);
            Assert.That(result, Is.EqualTo(new DateTime(2026, 2, 13, 8, 30, 0)));

            Schedule copy = new Schedule(loaded);
            Assert.That(copy.GetNextDateTime(new DateTime(2026, 1, 20, 9, 0, 0), true), Is.EqualTo(result));
        }

        [Test]
        public void DeactivatedCombination_KeepsDayTimes_AndAppliesThemOnReactivation()
        {
            //deactivating a combination keeps its day times entry (by design, so the
            //times are still there when it gets reactivated). The entry must neither
            //silently reactivate the combination on load nor lose its times.
            Schedule schedule = ScheduleTestData.CreateMonthlyRelative(1, DayPosition.Second, DayOfWeek.Friday);
            schedule.MonthlyMonthRelativeBasedSetDayActive(DayPosition.First, DayOfWeek.Monday, true);
            schedule.MonthlyHasAdvancedSchedule = true;
            schedule.MonthlyMonthRelativeBasedSetDayTime(DayPosition.First, DayOfWeek.Monday, 0, new TimeSpan(7, 15, 0));
            schedule.MonthlyMonthRelativeBasedSetDayTime(DayPosition.Second, DayOfWeek.Friday, 0, new TimeSpan(8, 30, 0));
            schedule.MonthlyMonthRelativeBasedSetDayActive(DayPosition.First, DayOfWeek.Monday, false);

            Schedule loaded = JsonConvert.DeserializeObject<Schedule>(JsonConvert.SerializeObject(schedule));

            //still inactive after the roundtrip: the second Friday wins
            Assert.That(loaded.GetNextDateTime(new DateTime(2026, 1, 20, 9, 0, 0), true),
                Is.EqualTo(new DateTime(2026, 2, 13, 8, 30, 0)));

            //after reactivation its kept day time applies: the first Monday at 7:15
            loaded.MonthlyMonthRelativeBasedSetDayActive(DayPosition.First, DayOfWeek.Monday, true);
            Assert.That(loaded.GetNextDateTime(new DateTime(2026, 1, 20, 9, 0, 0), true),
                Is.EqualTo(new DateTime(2026, 2, 2, 7, 15, 0)));
        }
    }
}
