using System;
using System.IO;
using CommandLine.Utility;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileSync
{
    class Program
    {
        static StreamWriter logFile;
        static int totalFiles = 0;
        static int totalErrors = 0;
        static int totalCopies = 0;
        static bool verbose = false;

        //threading
        private static System.Object todoLock = new System.Object();
        private static List<Task> todo = new List<Task>();

        static int Main(string[] args)
        {
            Arguments commandline = new Arguments(args);

            if (commandline["help"] != null || commandline["h"] != null)
            {
                Console.WriteLine("usage: FileSync.exe /s=<source> /d=<destination> [/l=<logfile>] [-v]\n");
                return 0;
            }

            string source, dest;

            if (commandline["s"] != null)
            {
                source = commandline["s"];
            }
            else
            {
                Console.Error.WriteLine("Missing source parameter\n");
                return 1;
            }


            if (commandline["d"] != null)
            {
                dest = commandline["d"];
            }
            else
            {
                Console.Error.WriteLine("Missing destination parameter\n");
                return 1;
            }

            if (commandline["l"] != null)
            {
                logFile = File.AppendText(commandline["l"]);
            }
            else
            {
                logFile = new StreamWriter(Console.OpenStandardOutput());
            }

            if (commandline["v"] != null)
            {
                verbose = true;
            }

            logFile.WriteLine("[INFO] " + DateTime.Now + "\tStarting Sync. Source=" + source + " Dest=" + dest);
            logFile.Flush();

            //create the inital task
            Task newTask = new Task(() => Sync(source, dest));
            lock (todoLock)
            {
                todo.Add(newTask);
            }
            newTask.Start();

            //block until a thread dies, check for empty todo list (it will be empty when all tasks are complete)
            while (true)
            {
                Task.WaitAny(todo.ToArray());
                lock (todoLock)
                {
                    //remove all completed tasks from the list
                    todo.RemoveAll(t => t.IsCompleted || t.IsCanceled || t.IsFaulted);

                    //if the todo list is empty, no more threads are left
                    if (todo.Count == 0)
                    {
                        break;
                    }
                }
            }

            logFile.WriteLine("[INFO] " + DateTime.Now + "\tFinished Sync. Total Files=" + Program.totalFiles + " Total Copies=" + Program.totalCopies + " Total Errors=" + Program.totalErrors);
            logFile.Close();

            return 0;
        }

        static void Sync(string f1, string f2)
        {
            //sync all folders from f1->f2 
            foreach (string folder in Directory.GetDirectories(f1))
            {
                if (verbose)
                {
                    logFile.WriteLine("[DEBUG] " + DateTime.Now + " Checking folder: Folder=" + folder);
                    logFile.Flush();
                }

                //skip over any symlink folder 
                if ((new FileInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint)))
                {
                    continue;
                }

                string foldername = Path.GetFileName(folder);

                if (!Directory.Exists(Path.Combine(f2, foldername)))
                {
                    //create the directory 
                    Directory.CreateDirectory(Path.Combine(f2, foldername));
                }

                if (Directory.Exists(Path.Combine(f2, foldername)))
                {
                    Task newTask = new Task(() => Sync(folder, Path.Combine(f2, foldername)));
                    lock (todoLock)
                    {
                        todo.Add(newTask);
                    }
                    newTask.Start();
                }
                else
                {
                    //an error has occured because the directory was not created
                    logFile.WriteLine("[ERROR] " + DateTime.Now + "\tCould not create directory: " + Path.Combine(f2, foldername));
                }
            }


            //sync all files from f1->f2
            foreach (string file in Directory.GetFiles(f1))
            {
                Interlocked.Increment(ref totalFiles);
                if (verbose)
                {
                    logFile.WriteLine("[DEBUG] " + DateTime.Now + " Checking file: File=" + file);
                    logFile.Flush();
                }

                string filename = Path.GetFileName(file);

                //check if file exists in f2
                if (!File.Exists(Path.Combine(f2, filename)))
                {
                    bool result = Copy(file, Path.Combine(f2, filename));
                    if (!result)
                    {
                        logFile.WriteLine("[ERROR] " + DateTime.Now + "\tCould not sync file: " + file);
                    }
                }
                //check if file is newer in f1 than in f2
                else if (isDifferent(File.GetLastWriteTimeUtc(file), File.GetLastWriteTimeUtc(Path.Combine(f2, filename))))
                {
                    bool result = Copy(file, Path.Combine(f2, filename));
                    if (!result)
                    {
                        logFile.WriteLine("[ERROR] " + DateTime.Now + "\tCould not sync file: " + file);
                    }
                }
                //file is up to date
                else
                {

                }
            }




        }

        public static bool Copy(string source, string dest)
        {
            try
            {
                if (verbose)
                {
                    logFile.WriteLine("[DEBUG] " + DateTime.Now + "Copy operation: Source=" + source + " Dest=" + dest);
                    logFile.Flush();
                }
                File.Copy(source, dest, true);
                Interlocked.Increment(ref totalCopies);
                return true;

            }
            catch
            {
                Interlocked.Increment(ref totalErrors);
                return false;
                //check error to see if file is in use. 
            }
        }

        public static bool isDifferent(DateTime x, DateTime y)
        {
            return (x - y) >= TimeSpan.FromSeconds(1);
        }
    }
}
