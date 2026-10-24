using System;
using Drexel.VidUp.Utils;
using NUnit.Framework;

namespace Drexel.VidUp.Test
{
    //BUG-25: GetRoundedToNextQuarterHour kept the seconds when the time was
    //already on a quarter hour (10:15:37 stayed 10:15:37) and dropped the
    //DateTime.Kind, so results did not match the schedule slots (seconds = 0)
    //they are compared against.
    [TestFixture]
    public class QuarterHourCalculatorTests
    {
        [Test]
        public void OnQuarterHourWithSeconds_RoundsUpToNextQuarterHour()
        {
            DateTime result = QuarterHourCalculator.GetRoundedToNextQuarterHour(new DateTime(2026, 1, 1, 10, 15, 37));
            Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 1, 10, 30, 0)));
        }

        [Test]
        public void ExactlyOnQuarterHour_Stays()
        {
            DateTime result = QuarterHourCalculator.GetRoundedToNextQuarterHour(new DateTime(2026, 1, 1, 10, 15, 0));
            Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 1, 10, 15, 0)));
        }

        [Test]
        public void OffQuarterHour_RoundsUpWithZeroedSeconds()
        {
            DateTime result = QuarterHourCalculator.GetRoundedToNextQuarterHour(new DateTime(2026, 1, 1, 10, 16, 37));
            Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 1, 10, 30, 0)));
        }

        [Test]
        public void LastQuarterOfDay_RollsToNextDay()
        {
            DateTime result = QuarterHourCalculator.GetRoundedToNextQuarterHour(new DateTime(2026, 1, 1, 23, 46, 0));
            Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 2, 0, 0, 0)));
        }

        [Test]
        public void KindIsPreserved()
        {
            DateTime utc = new DateTime(2026, 1, 1, 10, 16, 37, DateTimeKind.Utc);
            Assert.That(QuarterHourCalculator.GetRoundedToNextQuarterHour(utc).Kind, Is.EqualTo(DateTimeKind.Utc));

            DateTime local = new DateTime(2026, 1, 1, 10, 15, 0, DateTimeKind.Local);
            Assert.That(QuarterHourCalculator.GetRoundedToNextQuarterHour(local).Kind, Is.EqualTo(DateTimeKind.Local));
        }

        [Test]
        public void TruncateToMinute_DropsSecondsAndPreservesKind()
        {
            DateTime result = QuarterHourCalculator.TruncateToMinute(new DateTime(2026, 1, 1, 10, 15, 59, DateTimeKind.Utc));
            Assert.That(result, Is.EqualTo(new DateTime(2026, 1, 1, 10, 15, 0, DateTimeKind.Utc)));
            Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }
}
