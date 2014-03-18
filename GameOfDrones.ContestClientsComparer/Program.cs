using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommandLine;
using CommandLine.Text;

namespace GameOfDrones.ContestClientsComparer
{
    class Program
    {
        private sealed class Options
        {
            [ValueList(typeof(List<string>), MaximumElements = 4)]
            [DefaultValue(null)]
            public IList<string> PlayerPaths { get; set; }

            [Option('g', "games", DefaultValue = 200, HelpText = "Number of games to run (default is 200).")]
            public int NbrGames { get; set; }

            [HelpOption]
            public string GetUsage()
            {
                return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
            }
        }

        static Random random = new Random();

        static void Main(string[] args)
        {
            var options = new Options();
            if(!Parser.Default.ParseArguments(args, options))
                Environment.Exit(1);

            if(options.PlayerPaths.Count < 2)
            {
                Console.WriteLine("you must specify at least 2 paths to client executables.");
                Environment.Exit(1);
            }

            var scores = new int[options.PlayerPaths.Count];
            for(int i = 0; i < options.NbrGames; i++)
            {
                var gameWinnerId = RunGame(options.PlayerPaths);
                scores[gameWinnerId]++;
            }

            var ranking = scores
                .Select((s, i) => new {Score = s, Index = i})
                .OrderByDescending(s => s.Score)
                .Select(s => s.Index)
                .ToArray();
            Console.WriteLine(string.Join(" ", ranking));

            Environment.Exit(0);
        }

        static int RunGame(IEnumerable<string> playerPaths)
        {
            var players = playerPaths.Select(p => new ContestPlayerWrapper(p)).ToArray<IPlayer>();
            var minNbrZones = Math.Max(players.Length + 1, 4);
            var maxNbrZones = 8;

            var minNbrDrones = 3;
            var maxNbrDrones = 11;

            var nbrZones = random.Next(minNbrZones, maxNbrZones + 1);
            var nbrDrones = random.Next(minNbrDrones, maxNbrDrones + 1);

            using(var client = new ClientSimulator(nbrDrones, nbrZones, players))
            {
                client.Initialize();

                while(!client.HasFinished)
                    client.Update();

                return client.WinnerId;
            }
        }
    }
}
