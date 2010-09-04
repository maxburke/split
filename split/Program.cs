using System;

namespace Split
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (Split game = new Split())
            {
                game.Run();
            }
        }
    }
}

