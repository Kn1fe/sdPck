using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

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
