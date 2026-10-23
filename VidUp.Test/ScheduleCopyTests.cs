using System;
using Drexel.VidUp.Business;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //Characterization tests for BUG-11: the copy constructor dropped UploadedUntil
    //and IgnoreUploadsBefore, so duplicated templates lost their schedule progress.
    [TestFixture]
    public class ScheduleCopyTests
    {
        [Test]
        public void Copy_KeepsUploadedUntilAndIgnoreUploadsBefore()
        {
            Schedule schedule = ScheduleTestData.CreateWeekly(1, 0);
            schedule.UploadedUntil = new DateTime(2026, 3, 1, 10, 30, 0);
            schedule.IgnoreUploadsBefore = new DateTime(2026, 2, 1, 0, 0, 0);

            Schedule copy = new Schedule(schedule);

            Assert.That(copy.UploadedUntil, Is.EqualTo(schedule.UploadedUntil));
            Assert.That(copy.IgnoreUploadsBefore, Is.EqualTo(schedule.IgnoreUploadsBefore));
            Assert.That(copy.GetNextDateTime(new DateTime(2026, 1, 7, 9, 0, 0), true),
                Is.EqualTo(schedule.GetNextDateTime(new DateTime(2026, 1, 7, 9, 0, 0), true)));
        }

        [Test]
        public void Copy_SerializesIdenticallyToOriginal()
        {
            //guards the copy constructor against any other silently dropped field
            Schedule schedule = ScheduleTestData.CreateMonthlyRelative(2, DayPosition.Second, DayOfWeek.Friday);
            schedule.MonthlyHasAdvancedSchedule = true;
            schedule.MonthlyMonthRelativeBasedSetDayTime(DayPosition.Second, DayOfWeek.Friday, 0, new TimeSpan(8, 30, 0));
            schedule.UploadedUntil = new DateTime(2026, 3, 1, 10, 30, 0);
            schedule.IgnoreUploadsBefore = new DateTime(2026, 2, 1, 0, 0, 0);

            Schedule copy = new Schedule(schedule);

            Assert.That(JsonConvert.SerializeObject(copy), Is.EqualTo(JsonConvert.SerializeObject(schedule)));
        }
    }
}
