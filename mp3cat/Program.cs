using CommandLine;
using CommandLine.Text;
using MP3catSharp;
using MP3libSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace mp3cat
{
    public class Program
    {
        //public const string version = "4.2.2";
        public const string version = "1.0.0";
        public static void Main(string[] args)
        {
            var wait = false;
            if (args.Length == 0)
            {
                wait = true;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("mp3cat");
                Console.ResetColor();
                Console.Write(" > ");

                var cmd = Console.ReadLine();
                if (string.IsNullOrEmpty(cmd))
                    Environment.Exit(0);
                args = splitArgs(cmd).ToArray();
            }

            // Parse the command line arguments.
            var parser = new Parser(with =>
            {
                with.HelpWriter = null;
                with.AutoHelp = false;
                with.AutoVersion = false;
            });

            try
            {
                var parserResult = parser.ParseArguments<Options>(args);
                parserResult.WithParsed(o => DoWork(o));
                Environment.ExitCode = 1;
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 0;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }

            if (wait)
                Console.ReadLine();
        }

        public static void DoWork(Options options)
        {
            if (options.help)
            {
                Console.WriteLine(GetHelp());
                return;
            }
            if (options.version)
            {
                Console.WriteLine(version);
                return;
            }

            // Make sure we have a list of files to merge.
            var files = null as string[];

            if (options.dir != "")
            {
                var temp = new List<string>();
                var filePaths = Directory.GetFiles(options.dir);
                foreach (var path in filePaths)
                {
                    if (Path.GetExtension(path) == ".mp3")
                        temp.Add(path);
                }
                if (temp.Count == 0)
                {
                    throw new Exception("Error: no files found.");
                }
                files = temp.ToArray();
            }
            else if (options.files.Any())
            {
                files = options.files.ToArray();
            }
            else
            {
                throw new Exception("Error: you must specify files to merge.");
            }

            // Are we copying the ID3 tag from the n-th input file?
            var tagpath = "";
            if (options.meta != 0)
            {
                var tagindex = options.meta - 1;
                if (tagindex < 0 || tagindex > files.Length - 1)
                    throw new Exception("Error: --meta argument is out of range.");
                tagpath = files[tagindex];
            }

            // Are we interlacing a spacer file?
            if (options.interlace != "")
            {
                files = interlace(files, options.interlace);
            }

            // Make sure all the files in the list actually exist.
            validateFiles(files);

            // Set debug mode if the user supplied a --debug flag.
            if (options.debug)
            {
                MP3lib.DebugMode = true;
            }

            // Merge the input files.
            MP3cat.merge(options._out, tagpath, files, options.force, options.quiet);
        }

        public static string GetHelp()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Usage: mp3cat [files]");
            builder.AppendLine("");
            builder.AppendLine("  This tool concatenates MP3 files without re-encoding. Input files can be");
            builder.AppendLine("  specified as a list of filenames:");
            builder.AppendLine("");
            builder.AppendLine("    $ mp3cat one.mp3 two.mp3 three.mp3");
            builder.AppendLine("");
            builder.AppendLine("  Alternatively, an entire directory of .mp3 files can be concatenated:");
            builder.AppendLine("");
            builder.AppendLine("    $ mp3cat --dir /path/to/directory");
            builder.AppendLine("");
            builder.AppendLine("Arguments:");
            builder.AppendLine("  [files]                 List of files to merge.");
            builder.AppendLine("");
            builder.AppendLine("Options:");
            builder.AppendLine("  -d, --dir <path>        Directory of files to merge.");
            builder.AppendLine("  -m, --meta <n>          Copy ID3 metadata from the n-th input file.");
            builder.AppendLine("  -o, --out <path>        Output filepath. Defaults to 'output.mp3'.");
            builder.AppendLine("");
            builder.AppendLine("Flags:");
            builder.AppendLine("  -f, --force             Overwrite an existing output file.");
            builder.AppendLine("  -h, --help              Display this help text and exit.");
            builder.AppendLine("  -q, --quiet             Quiet mode. Only output error messages.");
            builder.AppendLine("  -v, --version           Display the version number and exit.");
            return builder.ToString();
        }

        public class Options
        {
            [Value(0, Hidden = true)]
            public IEnumerable<string> files { get; set; }

            [Option('d', "dir", Default = "")]
            public string dir { get; set; }

            [Option('m', "meta", Default = 0)]
            public int meta { get; set; }

            [Option('o', "out", Default = "output.mp3")]
            public string _out { get; set; }

            [Option('i', "interlace", Default = "")]
            public string interlace { get; set; }

            [Option('f', "force", Default = false)]
            public bool force { get; set; }

            [Option('h', "help", Default = false)]
            public bool help { get; set; }

            [Option('q', "quiet", Default = false)]
            public bool quiet { get; set; }

            [Option('v', "version", Default = false)]
            public bool version { get; set; }

            [Option("debug", Default = false)]
            public bool debug { get; set; }
        }

        // Check that all the files in the list exist.
        public static void validateFiles(string[] files)
        {
            foreach (var file in files)
            {
                if (!File.Exists(file))
                    throw new Exception($"Error: the file '{file}' does not exist.");
            }
        }

        // Interlace a spacer file between each file in the list.
        public static string[] interlace(string[] files, string spacer)
        {
            var interlaced = new List<string>();
            foreach (var file in files)
            {
                interlaced.Add(file);
                interlaced.Add(spacer);
            }
            interlaced.RemoveAt(interlaced.Count - 1);
            return interlaced.ToArray();
        }

        private static IEnumerable<string> splitArgs(string commandLine)
        {
            var result = new StringBuilder();

            var quoted = false;
            var escaped = false;
            var started = false;
            var allowcaret = false;
            for (int i = 0; i < commandLine.Length; i++)
            {
                var chr = commandLine[i];

                if (chr == '^' && !quoted)
                {
                    if (allowcaret)
                    {
                        result.Append(chr);
                        started = true;
                        escaped = false;
                        allowcaret = false;
                    }
                    else if (i + 1 < commandLine.Length && commandLine[i + 1] == '^')
                    {
                        allowcaret = true;
                    }
                    else if (i + 1 == commandLine.Length)
                    {
                        result.Append(chr);
                        started = true;
                        escaped = false;
                    }
                }
                else if (escaped)
                {
                    result.Append(chr);
                    started = true;
                    escaped = false;
                }
                else if (chr == '"')
                {
                    quoted = !quoted;
                    started = true;
                }
                else if (chr == '\\' && i + 1 < commandLine.Length && commandLine[i + 1] == '"')
                {
                    escaped = true;
                }
                else if (chr == ' ' && !quoted)
                {
                    if (started) yield return result.ToString();
                    result.Clear();
                    started = false;
                }
                else
                {
                    result.Append(chr);
                    started = true;
                }
            }
            if (started) yield return result.ToString();
        }
    }
}
