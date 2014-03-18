using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfDrones
{
    public class ClientSimulator : IDisposable
    {
        private readonly int _nbrDrones;
        private readonly int _nbrZones;
        private IPlayer[] _players;
        private Point[] _droneStartingPoints;

        public ClientSimulator(int nbrDrones, int nbrZones, params IPlayer[] players)
        {
            _nbrDrones = nbrDrones;
            _nbrZones = nbrZones;
            _players = players;
            int id = 0;
            foreach(var player in _players)
                player.TeamId = id++;

            this.GenerateContext();
        }

        public void Dispose()
        {
            foreach(var player in _players)
                player.Dispose();
        }

        public void GenerateContext()
        {
            this.Context = new GameContext();
            this.Context.Teams = Enumerable.Range(0, _players.Length)
                .Select(teamId => new Team(teamId, _nbrDrones)).ToList();

            var random = new Random();

            var minZoneX = Zone.Radius;
            var maxZoneX = GameContext.FieldWidth - 2 * Zone.Radius;
            var minZoneY = Zone.Radius;
            var maxZoneY = GameContext.FieldHeight - 2 * Zone.Radius;
            int minDistanceBetweenZones = 300;

            // zones
            this.Context.Zones = new List<Zone>();
            for(int i = 0; i < _nbrZones; i++)
            {
                Point zoneCenter;
                do
                {
                    zoneCenter = new Point(random.Next(minZoneX, maxZoneX), random.Next(minZoneY, maxZoneY));
                } while(this.Context.Zones.Any(z => zoneCenter.DistanceTo(z.Center) < minDistanceBetweenZones));

                this.Context.Zones.Add(new Zone(i, zoneCenter));
            }

            // drone starting points
            _droneStartingPoints = new Point[_nbrDrones];
            for(int i = 0; i < _droneStartingPoints.Length; i++)
                _droneStartingPoints[i] = new Point(random.Next(GameContext.FieldWidth), random.Next(GameContext.FieldHeight));
        }

        public void Initialize()
        {
            foreach(var zone in this.Context.Zones)
                zone.OwnerId = -1;

            foreach(var team in this.Context.Teams)
                for(int i = 0; i < team.Drones.Count; i++)
                    team.Drones[i].Position = team.Drones[i].PreviousPosition = _droneStartingPoints[i];

            this.PlayerScores = new int[_players.Length];

            foreach(var player in _players)
                player.Initialize(this.Context);

            this.Context.RemainingTurns = GameContext.MaxTurns;
        }

        public void Update()
        {
            var droneDestinations = _players.ToDictionary(p => p.TeamId, p => p.Play(this.Context).ToArray());

            // move drones
            foreach(var team in this.Context.Teams)
            {
                foreach(var drone in team.Drones)
                {
                    var dronePosition = drone.Position;
                    var droneDestination = droneDestinations[team.Id][drone.Id];
                    drone.Position = drone.Position.GetReachablePoint(droneDestination);
                }
            }

            // update zone ownerships
            foreach(var zone in this.Context.Zones)
            {
                var dronesByTeam = this.Context.GetDronesInZone(zone).ToLookup(d => d.TeamId);
                if(dronesByTeam.Any())
                {
                    var maxSquadSize = dronesByTeam.Max(team => team.Count());
                    if(!zone.HasOwner || dronesByTeam[zone.OwnerId].Count() < maxSquadSize)
                        zone.OwnerId = dronesByTeam.First(team => team.Count() == maxSquadSize).Key;
                }
            }

            // update scores
            var zonesByOwner = this.Context.Zones.Where(z => z.HasOwner).GroupBy(z => z.OwnerId);
            foreach(var zoneGroup in zonesByOwner)
                this.PlayerScores[zoneGroup.Key] += zoneGroup.Count();

            this.Context.RemainingTurns--;
        }

        public bool HasFinished { get { return this.Context.RemainingTurns <= 0; } }

        public int WinnerId { get { return this.PlayerScores.IndexOfMax(); } }

        public GameContext Context { get; set; }
        public int[] PlayerScores { get; set; }
    }
}