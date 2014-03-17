using System;
using System.Collections.Generic;

namespace GameOfDrones
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new ClientSimulator(3, 6, new TaskBasedPlayer(), new TaskBasedPlayer());
            client.Initialize();

            while(!client.HasFinished)
                client.Update();
        }
    }
}
