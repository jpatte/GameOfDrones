using System.Collections.Generic;

namespace GameOfDrones
{
    public class StupidPlayer : IPlayer
    {
        public int TeamId { get; set; }

        public void Initialize(GameContext context)
        {
        }

        public IEnumerable<Point> Play(GameContext context)
        {
            var myDrones = context.GetDronesOfTeam(this.TeamId);

            // here I always ask my drones to reach the bottom right corner... stupid :-) 
            foreach(var drone in myDrones)
            {
                yield return new Point(GameContext.FieldWidth - 1, GameContext.FieldHeight - 1);
            }
        }
    }
}