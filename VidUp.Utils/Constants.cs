namespace Drexel.VidUp.Utils
{
    public class Constants
    {
        //2^20; was 1048567 (a typo) until BUG-24, which skewed every MB display
        public const int ByteMegaByteFactor = 1048576;
    }
}
