using System;
using System.CodeDom;
using Newtonsoft.Json;

namespace Drexel.VidUp.Business
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class MonthRelativeCombination
    {
        [JsonProperty]
        private DayPosition dayPosition;
        [JsonProperty]
        private DayOfWeek dayOfWeek;

        public DayPosition DayPosition
        {
            get => this.dayPosition;
        }

        public DayOfWeek DayOfWeek
        {
            get => this.dayOfWeek;
        }

        public MonthRelativeCombination(DayPosition dayPosition, DayOfWeek dayOfWeek)
        {
            this.dayPosition = dayPosition;
            this.dayOfWeek = dayOfWeek;
        }

        //combinations are used as dictionary keys; with reference identity, lookups
        //failed whenever the list and the dictionary held different instances of
        //the same combination (BUG-22)
        public override bool Equals(object obj)
        {
            MonthRelativeCombination other = obj as MonthRelativeCombination;
            return other != null && other.dayPosition == this.dayPosition && other.dayOfWeek == this.dayOfWeek;
        }

        public override int GetHashCode()
        {
            return ((int)this.dayPosition * 397) ^ (int)this.dayOfWeek;
        }
    }
}
