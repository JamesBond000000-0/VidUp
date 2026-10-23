using System;
using Drexel.VidUp.Business;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Characterization tests for BUG-07: when the frequency advances the search into
    //the next scheduled week/month, the day search must restart at its beginning.
    [TestFixture]
    public class ScheduleNextDateTests
    {
        [TestCase("2026-01-07 09:00", "2026-01-19 12:00")] //target week, Monday already passed
        [TestCase("2026-01-14 09:00", "2026-01-19 12:00")] //off week (was 2026-02-02 before the fix)
        [TestCase("2026-01-19 11:00", "2026-01-19 12:00")] //target Monday before default time
        [TestCase("2026-01-19 13:00", "2026-02-02 12:00")] //target Monday after default time
        public void Weekly_EveryOtherWeekMonday(string query, string expected)
        {
            Schedule schedule = ScheduleTestData.CreateWeekly(2, 0);
            DateTime result = schedule.GetNextDateTime(DateTime.Parse(query), true);
            Assert.That(result, Is.EqualTo(DateTime.Parse(expected)));
        }

        [Test]
        public void Weekly_EveryThirdWeekFriday_AskedOnSaturdayOfOffWeek()
        {
            //target weeks start 2026-01-05, 2026-01-26, ...; Fridays: 2026-01-09, 2026-01-30
            Schedule schedule = ScheduleTestData.CreateWeekly(3, 4);
            DateTime result = schedule.GetNextDateTime(new DateTime(2026, 1, 17, 9, 0, 0), true);
            Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 30, 12, 0, 0)));
        }

        [Test]
        public void Weekly_EveryWeekMondayAndThursday_AskedOnTuesday()
        {
            Schedule schedule = ScheduleTestData.CreateWeekly(1, 0, 3);
            DateTime result = schedule.GetNextDateTime(new DateTime(2026, 1, 6, 9, 0, 0), true);
            Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 8, 12, 0, 0)));
        }

        [TestCase("2026-02-20 09:00", "2026-03-05 12:00")] //off month (was 2026-05-05 before the fix)
        [TestCase("2026-01-20 09:00", "2026-03-05 12:00")] //target month, day already passed
        [TestCase("2026-03-05 11:00", "2026-03-05 12:00")] //target day before default time
        public void Monthly_EveryOtherMonthOn5th(string query, string expected)
        {
            Schedule schedule = ScheduleTestData.CreateMonthlyDateBased(2, 5);
            DateTime result = schedule.GetNextDateTime(DateTime.Parse(query), true);
            Assert.That(result, Is.EqualTo(DateTime.Parse(expected)));
        }

        [Test]
        public void Monthly_EveryOtherMonthOn10thAnd20th_AskedInOffMonth()
        {
            Schedule schedule = ScheduleTestData.CreateMonthlyDateBased(2, 10, 20);
            DateTime result = schedule.GetNextDateTime(new DateTime(2026, 2, 15, 9, 0, 0), true);
            Assert.That(result, Is.EqualTo(new DateTime(2026, 3, 10, 12, 0, 0)));
        }

        [Test]
        public void MonthlyRelative_FirstMonday_AskedAfterJanuaryDate()
        {
            Schedule schedule = ScheduleTestData.CreateMonthlyRelative(1, DayPosition.First, DayOfWeek.Monday);
            DateTime result = schedule.GetNextDateTime(new DateTime(2026, 1, 20, 9, 0, 0), true);
            Assert.That(result, Is.EqualTo(new DateTime(2026, 2, 2, 12, 0, 0)));
        }

        [Test]
        public void MonthlyRelative_SecondFriday_EveryOtherMonth_AskedInOffMonth()
        {
            Schedule schedule = ScheduleTestData.CreateMonthlyRelative(2, DayPosition.Second, DayOfWeek.Friday);
            DateTime result = schedule.GetNextDateTime(new DateTime(2026, 2, 10, 9, 0, 0), true);
            Assert.That(result, Is.EqualTo(new DateTime(2026, 3, 13, 12, 0, 0)));
        }
    }
}
