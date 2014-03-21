using System;
using System.Collections.Generic;

namespace GameOfDrones.Simulator
{
    class Program
    {
        static void Main(string[] args)
        {
            //var player1 = new ContestPlayerWrapper(@"C:\Projects\GameOfDrones\History\Mark1\GameOfDrones.ContestClient.exe");
            //var player2 = new ContestPlayerWrapper(@"C:\Projects\GameOfDrones\History\Mark1\GameOfDrones.ContestClient.exe");
            var player1 = new TaskBasedPlayer();
            var player2 = new TaskBasedPlayer();

            using(var client = new ClientSimulator(6, 3, player1, player2))
            {
                client.Initialize();

                while(!client.HasFinished)
                    client.Update();                
            }
        }
    }
}
