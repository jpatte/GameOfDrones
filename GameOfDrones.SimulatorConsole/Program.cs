using System;
using System.Collections.Generic;

namespace GameOfDrones.Simulator
{
    class Program
    {
        static void Main(string[] args)
        {
            var player1 = new ContestPlayerWrapper(@"C:\Projects\GameOfDrones\History\Mark1\GameOfDrones.ContestClient.exe");
            var player2 = new ContestPlayerWrapper(@"C:\Projects\GameOfDrones\History\Mark1\GameOfDrones.ContestClient.exe");

            using(var client = new ClientSimulator(3, 6, player1, player2))
            {
                client.Initialize();

                while(!client.HasFinished)
                    client.Update();                
            }
        }
    }
}
