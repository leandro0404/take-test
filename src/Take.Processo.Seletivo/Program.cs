using System;

namespace Take.Processo.Seletivo
{
    public class Program
    {
        private static void Main(string[] args)
        {
            WebSocketServer.Start("http://localhost:8080/");
            Console.WriteLine("Press any key to exit...\n");
            Console.ReadKey();
        }
    }
}
