using System;

namespace Drexel.VidUp.Utils
{
    public class QuarterHourCalculator
    {
        public static DateTime GetRoundedToNextQuarterHour(DateTime dateTime)
        {
            //Always land on an exact quarter hour with zeroed seconds and the
            //Kind preserved. Previously a time already on a quarter hour kept
            //its seconds (10:15:37 stayed 10:15:37) while other times were
            //rounded up with zeroed seconds, and the reconstruction dropped the
            //Kind, so the results did not reliably match the schedule slots
            //(seconds = 0) they are compared against (BUG-25).
            DateTime quarterHour = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute / 15 * 15, 0, dateTime.Kind);
            if (quarterHour < dateTime)
            {
                quarterHour = quarterHour.AddMinutes(15);
            }

            return quarterHour;
        }

        //Drops everything below the minute, preserving the Kind. Comparisons of
        //publish times are done on values normalized this way (BUG-25).
        public static DateTime TruncateToMinute(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute, 0, dateTime.Kind);
        }
    }
}
