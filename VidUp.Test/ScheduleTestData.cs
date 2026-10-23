using System;
using System.Linq;
using Drexel.VidUp.Business;

namespace Drexel.VidUp.Test
{
    //Shared builders for the schedule characterization tests (Phase 4).
    //GetNextDateTime is called with ignoreStartDateLimitations: true so the pure
    //schedule math is tested without the "at least 24h in the future" rule.
    internal static class ScheduleTestData
    {
        public static readonly DateTime WeeklyStart = new DateTime(2026, 1, 5); //a Monday
        public static readonly DateTime MonthlyStart = new DateTime(2026, 1, 1);

        //day indexes: 0 = Monday .. 6 = Sunday
        public static Schedule CreateWeekly(int weekFrequency, params int[] activeDayIndexes)
        {
            Schedule schedule = new Schedule();
            schedule.ScheduleFrequency = ScheduleFrequency.Weekly;
            schedule.StartDate = ScheduleTestData.WeeklyStart;
            schedule.WeeklyWeekFrequency = weekFrequency;
            schedule.WeeklyDefaultTime = new TimeSpan(12, 0, 0);
            foreach (int dayIndex in activeDayIndexes)
            {
                schedule.WeeklySetDayActive(dayIndex, true);
            }

            if (!activeDayIndexes.Contains(0))
            {
                schedule.WeeklySetDayActive(0, false);
            }

            return schedule;
        }

        public static Schedule CreateMonthlyDateBased(int monthFrequency, params int[] activeDays)
        {
            Schedule schedule = new Schedule();
            schedule.ScheduleFrequency = ScheduleFrequency.Monthly;
            schedule.StartDate = ScheduleTestData.MonthlyStart;
            schedule.MonthlyMonthFrequency = monthFrequency;
            schedule.MonthlyDateBased = true;
            schedule.MonthlyDefaultTime = new TimeSpan(12, 0, 0);
            foreach (int day in activeDays)
            {
                schedule.MonthlyMonthDateBasedSetDayActive(day - 1, true);
            }

            if (!activeDays.Contains(1))
            {
                schedule.MonthlyMonthDateBasedSetDayActive(0, false);
            }

            return schedule;
        }

        public static Schedule CreateMonthlyRelative(int monthFrequency, DayPosition dayPosition, DayOfWeek dayOfWeek)
        {
            Schedule schedule = new Schedule();
            schedule.ScheduleFrequency = ScheduleFrequency.Monthly;
            schedule.StartDate = ScheduleTestData.MonthlyStart;
            schedule.MonthlyMonthFrequency = monthFrequency;
            schedule.MonthlyDateBased = false;
            schedule.MonthlyDefaultTime = new TimeSpan(12, 0, 0);
            schedule.MonthlyMonthRelativeBasedSetDayActive(dayPosition, dayOfWeek, true);
            if (dayPosition != DayPosition.First || dayOfWeek != DayOfWeek.Monday)
            {
                schedule.MonthlyMonthRelativeBasedSetDayActive(DayPosition.First, DayOfWeek.Monday, false);
            }

            return schedule;
        }
    }
}
