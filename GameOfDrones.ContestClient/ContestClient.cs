using System;
using System.IO;
using System.Linq;

namespace GameOfDrones
{
    public class ContestClient : IDisposable
    {
        private readonly TextReader _input;
        private readonly TextWriter _output;
        private IPlayer _player;

        public ContestClient(TextReader input, TextWriter output, IPlayer player)
        {
            _input = input;
            _output = output;
            _player = player;
        }

        public void Initialize()
        {
            var pidz = this.ReadIntegers();

            _player.TeamId = pidz[1];

            this.Context = new GameContext();
            this.Context.Teams = Enumerable.Range(0, pidz[0])
                .Select(teamId => new Team(teamId, pidz[2])).ToList();
            this.Context.Zones = Enumerable.Range(0, pidz[3])
                .Select(zoneId => new Zone(zoneId, this.ReadPoint())).ToList();

            _player.Initialize(this.Context);
        }

        public void Dispose()
        {
            _player.Dispose();
        }

        public void Update()
        {
            // update context
            foreach(var zone in this.Context.Zones)
                zone.OwnerId = this.ReadIntegers()[0];

            foreach(var team in this.Context.Teams)
            {
                foreach(var drone in team.Drones)
                {
                    drone.Position = this.ReadPoint();
                    if(this.Context.RemainingTurns == GameContext.MaxTurns)
                        drone.PreviousPosition = drone.Position;
                }
            }

            // play turn
            var playerDroneDestinations = _player.Play(this.Context);
            foreach(var droneDestination in playerDroneDestinations)
                this.WritePoint(droneDestination);

            this.Context.RemainingTurns--;
        }

        private int[] ReadIntegers()
        {
            // ReSharper disable once PossibleNullReferenceException
            return _input.ReadLine().Split(' ').Select(int.Parse).ToArray();
        }

        private Point ReadPoint()
        {
            var xy = this.ReadIntegers();
            return new Point { X = xy[0], Y = xy[1] };
        }

        public bool HasFinished { get { return this.Context.RemainingTurns <= 0; } }

        private void WritePoint(Point point)
        {
            _output.WriteLine("{0} {1}", point.X, point.Y);
        }

        public GameContext Context { get; set; }
    }
}