using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaSimulator
{
    // ShipInfo describes the layout of a ship and all modules on it, as well as their stat modifiers.
    internal class ShipInfo
    {

    }

    internal class Ship : GameObject
    {
        public Ship(ShipInfo info, Game game) : base(game)
        {
        }
    }
}
