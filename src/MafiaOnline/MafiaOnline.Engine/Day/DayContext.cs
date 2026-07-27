using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MafiaOnline.Engine.Day
{
    internal class DayContext
    {
        /// <summary>
        /// Players that cannot be voted out during this day. For now there should be only up to 1 enshrouded player at once.
        /// </summary>
        public HashSet<Guid> EnshroudedPlayers { get; set; } = new HashSet<Guid>();
    }
}
