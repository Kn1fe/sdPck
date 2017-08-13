using System;

namespace sdPck
{
    public static class Utils
    {
        public static int ToInt(this double value)
        {
            return value > 0 ? Convert.ToInt32(value) : 0;
        }
    }
}
