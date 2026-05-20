# FATVidSplitter

FAT32-compatible video splitter that automatically splits large video files into <4GB chunks and generates M3U playlists for seamless playback across devices.

---

## 🚀 Features

- ✅ Splits large video files into FAT32-safe chunks (<4GB)
- ✅ Preserves playback order
- ✅ Automatically generates `.m3u` playlists
- ✅ Ideal for USB drives, SD cards, and embedded media systems
- ✅ Works with common video formats (via FFmpeg)

---

## 📦 Use Case

FAT32 file systems have a **4GB file size limit**, which causes issues when:

- Copying large movies to USB drives
- Using car media systems
- Playing videos on TVs or embedded devices

**FATVidSplitter solves this by:**
1. Splitting the video into smaller parts
2. Generating a playlist so playback is seamless

---

## 🛠 Requirements

- [FFmpeg](https://ffmpeg.org/) installed and available in PATH

Verify:
```bash
ffmpeg -version
```

---

## ⚙️ Usage

```

# Example (adjust based on your script/entrypoint)
./fatvidsplitter input.mp4

```
---

## Output
```

input_part001.mp4
input_part002.mp4
input_part003.mp4
input.m3u
```

---

## ▶️ Playback
Use the generated .m3u file:

- VLC
- Kodi
- Smart TVs
- Car infotainment systems

This ensures continuous playback across split files.

---

## 📁 Example Workflow
```

# Split video
./fatvidsplitter movie.mp4

# Copy to USB
cp movie_part* /media/usb/
cp movie.m3u /media/usb/

# Play via playlist
```

---

## ⚡ How It Works

- Uses FFmpeg to segment video without re-encoding (fast + lossless where possible)
- Ensures each segment stays under FAT32 limits
- Builds a playlist referencing all segments in order

---

## 🧠 Design Goals

- Simplicity
- Compatibility
- Zero manual stitching
- Minimal dependencies

---

## 🐛 Limitations
- Requires FFmpeg installed
- Some players may not fully support .m3u playlists
- Exact split points may vary depending on encoding

---

## 🤝 Contributing
PRs welcome! Feel free to:

- Improve splitting logic
- Add platform-specific enhancements
- Expand format support

---

## 📄 License
This project is licensed under the MIT License.

See the full license in the LICENSE file.

---

## 👨‍💻 Author

**Jeffrey Lassen**  
Version: `1.0.1.1`  
Last Updated: `05/19/2026`

https://github.com/jhlassen17

---

## ☕ Support

If you find this useful:  
👉 https://buymeacoffee.com/hanf

---