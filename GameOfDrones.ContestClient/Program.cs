using System;

namespace GameOfDrones.Client
{
    static class Program
    {
        static void Main()
        {
            using(var client = new ContestClient(Console.In, Console.Out, new TaskBasedPlayer()))
            {
                client.Initialize();

                while(!client.HasFinished)
                    client.Update();            
            }       
        }
    }
}