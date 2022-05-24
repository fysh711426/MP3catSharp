using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MP3libSharp;

namespace MP3catSharp
{
	public class MP3cat
	{
		public static void merge(string outpath, string[] inpaths, string tagpath = "", bool force = false, bool quiet = false)
		{
			merge(outpath, tagpath, inpaths, force, quiet);
		}

		// Create a new file at [outpath] containing the merged contents of the list of input files.
		public static void merge(string outpath, string tagpath, string[] inpaths, bool force, bool quiet)
		{
			var totalFrames = (UInt32)0;
			var totalBytes = (UInt32)0;
			var totalFiles = 0;
			var firstBitRate = 0;
			var isVBR = false;

			// Only overwrite an existing file if the --force flag has been used.
			if (!force)
			{
				if (File.Exists(outpath))
					throw new Exception($"Error: the file '{outpath}' already exists.");
			}

			// If the list of input files includes the output file we'll end up in an infinite loop.
			foreach (var filepath in inpaths)
			{
				if (filepath == outpath)
					throw new Exception("Error: the list of input files includes the output file.");
			}

			// Create the output file.
			using (var outfile = new FileStream(outpath, FileMode.Create, FileAccess.Write))
			{
				if (!quiet)
				{
					printLine();
				}

				// Loop over the input files and append their MP3 frames to the output file.
				foreach (var inpath in inpaths)
				{
					if (!quiet)
					{
						Console.WriteLine($"+ {inpath}");
					}

					using (var infile = new FileStream(inpath, FileMode.Open, FileAccess.Read))
					{
						var isFirstFrame = true;

						while (true)
						{
							// Read the next frame from the input file.
							var frame = MP3lib.NextFrame(infile);
							if (frame == null)
								break;

							// Skip the first frame if it's a VBR header.
							if (isFirstFrame)
							{
								isFirstFrame = false;
								if (MP3lib.IsXingHeader(frame) || MP3lib.IsVbriHeader(frame))
									continue;
							}

							// If we detect more than one bitrate we'll need to add a VBR header to the output file.
							if (firstBitRate == 0)
								firstBitRate = frame.BitRate;

							else if (frame.BitRate != firstBitRate)
								isVBR = true;

							// Write the frame to the output file.
							outfile.Write(frame.RawBytes, 0, frame.RawBytes.Length);

							totalFrames++;
							totalBytes += (UInt32)frame.RawBytes.Length;
						}
					}
					totalFiles++;
				}
			}

			if (!quiet)
				printLine();

			// If we detected multiple bitrates, prepend a VBR header to the file.
			if (isVBR)
			{
				if (!quiet)
					Console.Write("• Multiple bitrates detected. Adding VBR header.");
				addXingHeader(outpath, totalFrames, totalBytes);
			}

			// Copy the ID3v2 tag from the n-th input file if requested. Order of operations is important
			// here. The ID3 tag must be the first item in the file - in particular, it must come *before*
			// any VBR header.
			if (tagpath != "")
			{
				if (!quiet)
					Console.WriteLine($"• Copying ID3 tag from: {tagpath}");
				addID3v2Tag(outpath, tagpath);
			}

			// Print a count of the number of files merged.
			if (!quiet)
			{
				Console.WriteLine($"• {totalFiles} files merged.");
				printLine();
			}
		}

		// Prepend an Xing VBR header to the specified MP3 file.
		public static void addXingHeader(string filepath, UInt32 totalFrames, UInt32 totalBytes)
		{
			using (var outputFile = new FileStream($"{filepath}.mp3cat.tmp", FileMode.Create, FileAccess.Write))
			using (var inputFile = new FileStream(filepath, FileMode.Open, FileAccess.Read))
			{
				var xingHeader = MP3lib.NewXingHeader(totalFrames, totalBytes);
				outputFile.Write(xingHeader.RawBytes, 0, xingHeader.RawBytes.Length);
				inputFile.CopyTo(outputFile);
			}
			File.Delete(filepath);
			File.Move($"{filepath}.mp3cat.tmp", filepath);
		}

		// Prepend an ID3v2 tag to the MP3 file at mp3Path, copying from tagPath.
		public static void addID3v2Tag(string mp3Path, string tagPath)
		{
			var id3tag = null as ID3v2Tag;
			using (var tagFile = new FileStream(tagPath, FileMode.Open, FileAccess.Read))
			{
				id3tag = MP3lib.NextID3v2Tag(tagFile);
			}

			if (id3tag != null)
			{
				using (var outputFile = new FileStream($"{mp3Path}.mp3cat.tmp", FileMode.Create, FileAccess.Write))
				using (var inputFile = new FileStream(mp3Path, FileMode.Open, FileAccess.Read))
				{
					outputFile.Write(id3tag.RawBytes, 0, id3tag.RawBytes.Length);
					inputFile.CopyTo(outputFile);
				}
				File.Delete(mp3Path);
				File.Move($"{mp3Path}.mp3cat.tmp", mp3Path);
			}
		}

		// Print a line to stdout if we're running in a terminal.
		public static void printLine()
		{
			for (var i = 0; i < Console.WindowWidth; i++)
				Console.Write("-");
			Console.WriteLine();
		}
	}
}
