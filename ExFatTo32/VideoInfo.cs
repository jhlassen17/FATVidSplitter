using System;
using System.Collections.Generic;
using System.Text;

namespace ExFatTo32
{
    /// <summary>
    /// Represents information about a video file, such as its duration and average bitrate.
    /// </summary>
    public class VideoInfo
    {
        public string FilePath { get; set; } = string.Empty; // Path to the video file
        public double DurationSeconds { get; set; } = 3600.0;   // Default 1 hour
        public long AvgBitrate { get; set; } = 12_000_000;    // Default 12Mbps
    }
}
