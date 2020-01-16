using FTP;
using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using static System.Console;
using Console_Server_Asynchronous;

namespace Console_Server_Asynchronous
{
    class Program
    {
        public static void Main(string[] arguments)
        {
            ServerConsole.ServerInterface();
        }
    }

}
//knuth morris pratt