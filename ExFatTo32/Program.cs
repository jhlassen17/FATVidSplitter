using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace ExFatTo32
{
    /// <summary>
    /// Program to split large video files into smaller chunks suitable for FAT32 file systems, 
    /// while preserving directory structure and generating playlists for split files.
    /// </summary>
    class Program
    {
        // FAT32 Max File Size is approx 4GB. We use a safe limit of 3.99 GB to be sure.
        private const long FAT32_LIMIT_BYTES = 4L * 1024 * 1024 * 1024;

        /// <summary>
        /// Entry point of the application that processes video files from a source directory to a destination
        /// directory.
        /// </summary>
        /// <remarks>Requires at least two arguments for source and destination paths when not attached to
        /// a debugger. Usage: VideoSplitter.exe &lt;SourcePath&gt; &lt;DestPath&gt; [--skip-small]
        /// [--generate-playlists]</remarks>
        /// <param name="args">Command-line arguments containing source path, destination path, and optional 
        /// flags (--skip-small, --generate-playlists).</param>
        static void Main(string[] args)
        {
            //
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== Video Splitter for FAT32 ===");
            Console.WriteLine("This tool splits large video files into smaller chunks suitable for FAT32 file systems, " +
                "while preserving directory structure and generating playlists for split files.");

            // For normal execution, require at least 2 arguments. For debugging, allow hardcoded paths.
            if (args.Length < 2 && !Debugger.IsAttached)
            {
                Console.WriteLine("Usage: VideoSplitter.exe <SourcePath> <DestPath>");
                return;
            }
            else if (Debugger.IsAttached)
            {
                // For debugging, you can hardcode paths here
                args = new string[]
                {
                    @"J:\jeff\files\Travel\Movies",
                    @"J:\jeff\files\Travel\SplitOutput",
                    "--skip-small", // Optional flag to skip files under 1MB
                    "--generate-playlists" // Optional flag to generate playlists for split files
                };
            }

            // Set up paths and options
            string sourcePath = Path.GetFullPath(args[0]);
            string destPath = Path.GetFullPath(args[1]);
            bool skipSmallFiles = args.Contains("--skip-small");
            bool generatePlaylists = args.Contains("--generate-playlists");

            // Validate source and destination paths
            if (!Directory.Exists(sourcePath))
            {
                Console.WriteLine($"Error: Source path '{sourcePath}' does not exist.");
                return;
            }

            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
                Console.WriteLine($"Created destination folder: {destPath}");
            }

            // Define video file extensions to process
            var videoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" };

            Console.WriteLine($"Scanning folder recursively: {sourcePath}");

            try
            {
                // Process files in the source directory and split as needed
                ProcessFiles(sourceDir: sourcePath, destDir: destPath, extensions: videoExtensions,
                    skipSmallFiles: skipSmallFiles);

                // After processing, optionally generate playlists for split files
                if (generatePlaylists)
                {
                    GeneratePlaylists(destPath);
                }

                // Final message after processing all files
                Console.WriteLine("Processing complete.");
            }
            catch (Exception ex)
            {
                // Catch any unexpected exceptions during processing and log them
                Console.WriteLine($"Error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes files from a source directory, copying or splitting them to a destination directory 
        /// based on file size.
        /// </summary>
        /// <remarks>Files smaller than 1 MB are skipped. Files exceeding the FAT32 limit are split, while
        /// smaller files are copied directly unless <paramref name="skipSmallFiles"/> is true.</remarks>
        /// <param name="sourceDir">The source directory path to search for files.</param>
        /// <param name="destDir">The destination directory path where files will be copied or split.</param>
        /// <param name="extensions">The collection of file extensions to process (e.g., ".txt", ".jpg").</param>
        /// <param name="skipSmallFiles">Indicates whether to skip copying files below the FAT32 size limit.</param>
        static void ProcessFiles(string sourceDir, string destDir, IEnumerable<string> extensions, bool skipSmallFiles)
        {
            // Get all files in the source directory and subdirectories
            var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);

            // Process each file found in the source directory
            foreach (var fullPath in files)
            {
                // Calculate the relative path from the source directory to maintain subfolder structure
                string relPath = Path.GetRelativePath(sourceDir, fullPath);
                string subFolderPath = Path.GetDirectoryName(relPath);

                // Get the file extension and check if it's in the list of extensions to process
                string ext = Path.GetExtension(fullPath).ToLowerInvariant();
                if (!extensions.Contains(ext))
                    continue;

                // Get the file name and size for logging and processing decisions
                var fileName = Path.GetFileName(fullPath);
                long fileSize = new FileInfo(fullPath).Length;

                // Skip files that are 0 bytes and are not suitable for splitting
                if (fileSize <= 0)
                {
                    Console.WriteLine($"  [!] Skipping file with size: {fileSize} bytes");
                    continue;
                }
                else if (fileSize < 1 * 1024 * 1024)
                {
                    // Direct copy small files (under 1 MB) without splitting, as they are not suitable for splitting and are small enough

                    if (!skipSmallFiles)
                    {
                        // Ensure destination directory exists
                        Directory.CreateDirectory(Path.Combine(destDir, subFolderPath));
                        // Copy the small file directly to the destination
                        File.Copy(fullPath, Path.Combine(destDir, fileName), overwrite: true);
                        Console.WriteLine($"  -> Copied directly (OK size).");
                    }

                    continue;
                }

                // Log the file being processed, showing the relative folder path for clarity
                string folderRel = subFolderPath ?? "";
                Console.WriteLine($"[{folderRel}] Processing: {fileName}");

                try
                {
                    // If the file size is within the FAT32 limit and we're not skipping small files, copy it directly
                    if (fileSize <= FAT32_LIMIT_BYTES && !skipSmallFiles)
                    {
                        // Ensure destination directory exists
                        Directory.CreateDirectory(Path.Combine(destDir, subFolderPath));
                        // Copy the file directly to the destination
                        File.Copy(fullPath, Path.Combine(destDir, fileName), overwrite: true);
                        Console.WriteLine($"  -> Copied directly (OK size).");
                        continue;
                    }
                    else if (fileSize > FAT32_LIMIT_BYTES)
                    {
                        // Ensure destination directory exists
                        Directory.CreateDirectory(Path.Combine(destDir, subFolderPath));
                    }

                    // If the file exceeds the FAT32 limit, split it into smaller chunks
                    SplitLargeFile(inputPath: fullPath, inputFileName: fileName, outputBaseDir: destDir, subFolderPath: subFolderPath);
                }
                catch (Exception ex)
                {
                    // Log any errors that occur during processing of this file, but continue with the next files
                    Console.WriteLine($"  ERROR with {fileName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Splits a large video file into smaller chunks if it exceeds the FAT32 file size limit using FFmpeg.
        /// </summary>
        /// <remarks>Files exceeding 4GB are split into approximately 3GB chunks. Uses FFprobe to
        /// determine duration and bitrate for optimal chunk calculation. Output files are named with a sequential
        /// suffix pattern (_part_001, _part_002, etc.).</remarks>
        /// <param name="inputPath">Full path to the input video file.</param>
        /// <param name="inputFileName">Name of the input file.</param>
        /// <param name="outputBaseDir">Base output directory for the split files.</param>
        /// <param name="subFolderPath">Subfolder path relative to the base directory where split files will be saved.</param>
        static void SplitLargeFile(string inputPath, string inputFileName, string outputBaseDir, string subFolderPath)
        {
            // Get the file size to determine if splitting is necessary
            long fileSize = new FileInfo(inputPath).Length;
            if (fileSize <= FAT32_LIMIT_BYTES) return;

            // Use FFprobe to get video duration and average bitrate for better split time calculation
            double durationSeconds = 3600.0;
            long avgBitrate = 12_000_000;
            var info = RunFfProbe(inputPath);

            // If FFprobe returns valid information, use it to calculate split times. Otherwise, fall back to defaults.
            if (info != null)
            {
                durationSeconds = info.DurationSeconds;
                avgBitrate = info.AvgBitrate;
            }

            // Log the file size and calculated duration/bitrate for debugging purposes
            Console.WriteLine($"  [!] File exceeds FAT32 limit ({fileSize / (1024 * 1024 * 1024):0.##} GB).");
            if (durationSeconds < 1) durationSeconds = 5400.0;
            if (avgBitrate <= 0 || avgBitrate == -1) avgBitrate = 12_000_000;

            // Calculate split time based on target chunk size (3GB) and average bitrate, ensuring a minimum of 60 seconds per chunk
            long targetBytes = 3 * 1024 * 1024 * 1024L;
            double secondsPerChunk = (targetBytes * 8.0) / avgBitrate;
            if (secondsPerChunk < 60) secondsPerChunk = 60.0;

            // Prefer -loglevel info to see progress, or error for silence. 
            // Here using 'error' matches RunFfProbe usage.
            string logLevel = "-loglevel error";

            // Ensure we don't calculate a split time longer than the total duration of the video
            double finalSplitTime = Math.Min(secondsPerChunk, durationSeconds);
            Console.WriteLine($"  -> Calculated split: ~3GB ({finalSplitTime:F2}s)");

            // Prepare the output path for the split files, maintaining the subfolder structure and using a sequential naming pattern
            string baseName = Path.GetFileNameWithoutExtension(inputFileName);
            string destSubPath = Path.Combine(outputBaseDir, subFolderPath);
            Directory.CreateDirectory(destSubPath); // Ensure parent dirs exist
            string outputBasePath = Path.Combine(destSubPath, baseName + "_part_%03d.mkv");

            // Log the output path for debugging purposes
            Console.WriteLine($"  -> Writing to: {outputBasePath}");

            try
            {
                // Use FFmpeg to split the video file into chunks based on the calculated split time, while preserving audio and video streams
                using (var process = new Process())
                {
                    // Set the FFmpeg command and arguments to split the video file, ensuring proper quoting for paths with spaces and using modern FFmpeg flags
                    string ffCmd = "ffmpeg.exe";
                    process.StartInfo.FileName = ffCmd;

                    // Set FFmpeg arguments to split the video while preserving audio and video streams, using the calculated split time, and ensuring proper quoting for paths with spaces.
                    // Note: Quotes around the variables inside the string interpolation are crucial.
                    process.StartInfo.Arguments = $"-hide_banner {logLevel} -y -i \"{inputPath}\" " +
                        $"-map 0:v -map 0:a " +
                        $"-c copy -f segment " +
                        $"-segment_time {finalSplitTime} " +
                        $"-avoid_negative_ts make_zero " +
                        $"\"{outputBasePath}\"";

                    // Configure the process to redirect output and error streams for logging, and to not use the shell
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    // Set up event handlers to log FFmpeg output and errors in real-time
                    process.ErrorDataReceived += (s, e) =>
                        Console.WriteLine($"  [FFmpeg]: {e.Data}");
                    // Optionally, you can also log standard output if needed, but FFmpeg typically uses standard error for progress and messages
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Console.Write(e.Data);
                    };

                    // Start the FFmpeg process and begin reading output and error streams asynchronously
                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();

                    // Wait for the FFmpeg process to exit, with a timeout of 60 seconds. If it doesn't exit in time, kill the process to prevent hanging.
                    bool hasExited = process.WaitForExit(60000);
                    if (hasExited)
                    {
                        // Check the exit code to determine if FFmpeg completed successfully or if there was an error
                        int exitCode = process.ExitCode;
                        if (exitCode != 0)
                        {
                            Console.WriteLine($"  [!] FFmpeg exited with error code: {exitCode}");
                        }
                        else
                        {
                            Console.WriteLine($"  -> Split completed successfully.");
                        }
                    }
                    else
                    {
                        // If FFmpeg does not exit within the timeout, log a warning and kill the process to prevent it from hanging indefinitely
                        Console.WriteLine($"  [!] FFmpeg timed out after 60 seconds, killing process...");
                        process.Kill();
                    }
                }
            }
            catch (Exception exe)
            {
                // Log any exceptions that occur during the FFmpeg process execution, which could be due to issues with FFmpeg itself, file access, or other unexpected errors
                Console.WriteLine($"  -> FFmpeg Failed: {exe.Message}");
            }
        }

        /// <summary>
        /// Executes ffprobe to extract video duration and bitrate information from the specified file.
        /// </summary>
        /// <remarks>Warnings are written to the console if ffprobe execution fails.</remarks>
        /// <param name="inputPath">The path to the video file to analyze.</param>
        /// <returns>A <see cref="VideoInfo"/> object containing the video duration in seconds and average bitrate in bits per
        /// second, or an empty <see cref="VideoInfo"/> if the operation fails.</returns>
        static VideoInfo RunFfProbe(string inputPath)
        {
            try
            {
                // Set up the process start information to execute ffprobe with arguments to extract duration and bitrate in a CSV format for easy parsing
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe.exe",
                    Arguments = $"-hide_banner -v error -select_streams v:0 -show_entries stream=duration,bit_rate -of csv=p=0 \"{inputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                // Start the ffprobe process and read the output to extract duration and bitrate information.
                // The output is expected to be in the format "duration,bitrate".
                using (var process = new Process { StartInfo = startInfo })
                {
                    // Start the ffprobe process and read the output synchronously. The output is expected to be a
                    // single line in CSV format containing duration and bitrate.
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // If the output is not empty, parse the duration and bitrate values from the CSV output.
                    // The output is expected to be in the format "duration,bitrate".
                    if (!string.IsNullOrEmpty(output))
                    {
                        // Split the output by comma and trim whitespace to extract duration and bitrate values. The first part is expected to be the duration in seconds,
                        // and the second part is expected to be the average bitrate in bits per second.
                        var parts = output.Trim().Split(',');

                        // Parse the duration and bitrate values from the output. If parsing fails, the default values defined in the VideoInfo class will be used.
                        double.TryParse(parts[0].Trim(), out double duration);
                        long.TryParse(parts[1].Trim(), out long bitrate);

                        // Return a new VideoInfo object containing the parsed duration and bitrate values. If parsing fails, the default values will be returned.
                        return new VideoInfo { FilePath = inputPath, DurationSeconds = duration, AvgBitrate = bitrate };
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions that occur during the execution of ffprobe, which could be due to issues with ffprobe itself, file access, or other unexpected errors. The method will return an empty VideoInfo object in case of failure.
                Console.WriteLine($"[Warning] FFprobe failed: {ex.Message}");
            }

            // If ffprobe execution fails or the output is empty, return an empty VideoInfo object with default values.
            // The caller can check for valid values and decide how to proceed.
            return new VideoInfo { FilePath = inputPath };
        }

        /// <summary>
        /// Generates M3U playlist files for split video parts in all subdirectories of the specified destination.
        /// </summary>
        /// <param name="destDir">The root directory path to search for split video files.</param>
        static void GeneratePlaylists(string destDir)
        {
            // Log the start of the playlist generation process for split files
            Console.WriteLine("Generating playlists for split files...");

            // Find all directories in dest
            var directories = Directory.GetDirectories(destDir, "*", SearchOption.AllDirectories);

            // Process each directory to find split video parts and generate a playlist for them
            foreach (var dirPath in directories)
            {
                try
                {
                    // Get part files in this directory
                    var partFiles = Directory.GetFiles(dirPath, "*_part_*.mkv")
                        .OrderBy(f => Path.GetFileName(f))
                        .ToArray();

                    // If no part files are found in this directory, skip to the next directory
                    if (partFiles.Length == 0)
                        continue;

                    // Extract video name from first part file
                    string baseName = Path.GetFileNameWithoutExtension(partFiles.First())
                        //.Split('_').First(p => p.Contains("_") || !p.Contains("_")) + "_video";
                        .Split('_').First(p => p.Contains("_") || !p.Contains("_"));
                    string originalName = baseName.Split('_')[0];

                    // Create playlist in this folder
                    string playlistPath = Path.Combine(dirPath, $"{baseName}.m3u");

                    // Write the M3U playlist file with the list of part files, using absolute paths to ensure compatibility with media players. The playlist will contain entries for each part file in the directory,
                    // allowing media players to play the split video as a single continuous stream.
                    using (StreamWriter writer = new StreamWriter(playlistPath))
                    {
                        // Write the M3U header to indicate that this is a playlist file
                        writer.WriteLine("#EXTM3U");

                        // Write each part file to the playlist, using the full absolute path to ensure that media players can locate the files correctly regardless of the current working directory. The part files are
                        // ordered by their file name to ensure they are played in the correct sequence.
                        foreach (var partFile in partFiles)
                        {
                            // Write full absolute path to playlist
                            writer.WriteLine(partFile);
                            Console.WriteLine($"  [!] Added: {Path.GetFileName(partFile)}");
                        }

                        // Log the creation of the playlist file, including the number of part files included in the playlist for this directory
                        Console.WriteLine($"  -> Created playlist: {playlistPath} ({partFiles.Length} parts)");
                    }
                }
                catch (Exception ex)
                {
                    // Log any exceptions that occur during the playlist generation process for this directory,
                    // which could be due to file access issues, write permissions, or other unexpected errors.
                    // The method will continue processing other directories even if one fails.
                    Console.WriteLine($"  ERROR in directory {dirPath}: {ex.Message}");
                }
            }
        }
    }
}
