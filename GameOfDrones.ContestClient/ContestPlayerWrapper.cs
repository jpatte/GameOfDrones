using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GameOfDrones
{
    public class ContestPlayerWrapper : IPlayer
    {
        private Process _playerProcess;
        private TextWriter _playerInput;
        private TextReader _playerOutput;

        public int TeamId { get; set; }

        public ContestPlayerWrapper(string exePath)
        {
            _playerProcess = new Process();
            _playerProcess.StartInfo.FileName = exePath;
            _playerProcess.StartInfo.RedirectStandardInput = true;
            _playerProcess.StartInfo.RedirectStandardOutput = true;
            _playerProcess.StartInfo.UseShellExecute = false;
        }

        public void Initialize(GameContext context)
        {
            _playerProcess.Start();
            _playerInput = _playerProcess.StandardInput;
            _playerOutput = _playerProcess.StandardOutput;

            _playerInput.WriteLine("{0} {1} {2} {3}",
                context.Teams.Count, this.TeamId, context.Teams[0].Drones.Count, context.Zones.Count);

            foreach(var zone in context.Zones)
                _playerInput.WriteLine("{0} {1}", zone.Center.X, zone.Center.Y);
        }

        public IEnumerable<Point> Play(GameContext context)
        {
            // write input
            foreach(var zone in context.Zones)
                _playerInput.WriteLine("{0}", zone.OwnerId);

            foreach(var team in context.Teams)
                foreach(var drone in team.Drones)
                    _playerInput.WriteLine("{0} {1}", drone.Position.X, drone.Position.Y);

            // get output
            foreach(var drone in context.GetDronesOfTeam(this.TeamId))
            {
                // ReSharper disable once PossibleNullReferenceException
                var xy = _playerOutput.ReadLine().Split(' ').Select(int.Parse).ToArray();
                yield return new Point { X = xy[0], Y = xy[1] };
            }
        }

        public void Dispose()
        {
            _playerProcess.Dispose();
        }
    }
}
