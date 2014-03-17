using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace GameOfDrones
{
    public struct Point
    {
        public Point(int x, int y)
            : this()
        {
            this.X = x;
            this.Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }
    }

    public class Drone
    {
        private Point _position;

        public Drone(int teamId, int id)
        {
            this.TeamId = teamId;
            this.Id = id;
        }

        public int TeamId { get; set; }
        public int Id { get; set; }
        public Point PreviousPosition { get; set; }

        public Point Position
        {
            get { return _position; }
            set
            {
                this.PreviousPosition = this.Position;
                _position = value;
            }
        }
    }

    public class Zone
    {
        public const int Radius = 100;

        public Zone(int id, Point center)
        {
            this.Id = id;
            this.Center = center;
            this.OwnerId = -1;
        }

        public int Id { get; set; }
        public Point Center { get; set; }
        public int OwnerId { get; set; }
        public bool HasOwner { get { return this.OwnerId != -1; } }
    }

    public class Team
    {
        public Team(int id, int droneCount)
        {
            this.Id = id;
            this.Drones = Enumerable.Range(0, droneCount)
                .Select(droneId => new Drone(id, droneId)).ToList();
        }

        public int Id { get; set; }
        public IList<Drone> Drones { get; private set; }
    }

    public class GameContext
    {
        public const int FieldWidth = 4000;
        public const int FieldHeight = 1800;
        public const int MaxTurns = 200;
        public const int MaxMoveDistance = 100;

        public GameContext()
        {
            this.RemainingTurns = MaxTurns;
        }

        public int RemainingTurns { get; set; }
        public IList<Zone> Zones { get; set; }
        public IList<Team> Teams { get; set; }
    }

    public interface IPlayer
    {
        int TeamId { get; set; }

        void Initialize(GameContext context);

        IEnumerable<Point> Play(GameContext context);
    }
}
