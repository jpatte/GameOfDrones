using System;

namespace GameOfDrones
{
    static class Program
    {
        static void Main()
        {
            var client = new ContestClient(Console.In, Console.Out, new TaskBasedPlayer());
            client.Initialize();

            while(true)
                client.Update();
        }
    }
}