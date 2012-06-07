﻿using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace Nikse.SubtitleEdit.Logic.VobSub
{
    public class VobSubWriter
    {
        /// <summary>
        /// 14 bytes Mpeg 2 pack header
        /// </summary>
        private static readonly byte[] Mpeg2PackHeaderBuffer =
        {
            0x00, 0x00, 0x01,       // Start code
            0xba,                   // MPEG-2 Pack ID
            0x44, 0x02, 0xec, 0xdf, // System clock reference
            0xfe, 0x57,
            0x01, 0x89, 0xc3,       // Program mux rate
            0xf8                    // stuffing byte
        };

        /// <summary>
        /// 9 bytes packetized elementary stream header (PES)
        /// </summary>
        private static readonly byte[] PacketizedElementaryStreamHeaderBufferFirst =
        {
            0x00, 0x00, 0x01,       // Start code
            0xbd,                   // bd = Private stream 1 (non MPEG audio, subpictures)
            0x00, 0x00,             // 18-19=PES packet length
            0x81,                   // 20=Flags: PES scrambling control, PES priority, data alignment indicator, copyright, original or copy
            0x81,                   // 21=Flags: PTS DTS flags, ESCR flag, ES rate flag, DSM trick mode flag, additional copy info flag, PES CRC flag, PES extension flag
            0x08                    // 22=PES header data length
        };

        /// <summary>
        /// 9 bytes packetized elementary stream header (PES)
        /// </summary>
        private static readonly byte[] PacketizedElementaryStreamHeaderBufferNext =
        {
            0x00, 0x00, 0x01,       // Start code
            0xbd,                   // bd = Private stream 1 (non MPEG audio, subpictures)
            0x00, 0x00,             // PES packet length
            0x81,                   // 20=Flags: PES scrambling control, PES priority, data alignment indicator, copyright, original or copy
            0x00,                   // 21=Flags: PTS DTS flags, ESCR flag, ES rate flag, DSM trick mode flag, additional copy info flag, PES CRC flag, PES extension flag
            0x00                    // 22=PES header data length
        };

        /// <summary>
        /// 5 bytes presentation time stamp (PTS)
        /// </summary>
        private static readonly byte[] PresentationTimeStampBuffer =
        {
            0x21,                   // 0010 3=PTS 32..30 1
            0x00, 0x01,             // 15=PTS 29..15 1
            0x00, 0x01              // 15=PTS 14..00 1
        };

        private const int PacketizedElementaryStreamMaximumLength = 2028;

        private readonly string _subFileName;
        private FileStream _subFile;
        readonly StringBuilder _idx = new StringBuilder();
        readonly int _screenWidth = 720;
        readonly int _screenHeight = 480;
        readonly int _bottomMargin = 15;
        readonly int _languageStreamId = 32;
        Color _background = Color.Transparent;
        Color _pattern = Color.White;
        Color _emphasis1 = Color.Black;
        Color _emphasis2 = Color.FromArgb(240, Color.Black);
        readonly string _languageName = "English";
        readonly string _languageNameShort = "en";

        public VobSubWriter(string subFileName, int screenWidth, int screenHeight, int bottomMargin, int languageStreamId, Color pattern, Color emphasis1, Color emphasis2,
                            string languageName, string languageNameShort)
        {
            _subFileName = subFileName;
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _bottomMargin = bottomMargin;
            _languageStreamId = languageStreamId;
            _pattern = pattern;
            _emphasis1 = emphasis1;
            _emphasis2 = emphasis2;
            _languageName = languageName;
            _languageNameShort = languageNameShort;
            _idx = CreateIdxHeader();
            _subFile = new FileStream(subFileName, FileMode.Create);
        }

        public void WriteEndianWord(int i, Stream stream)
        {
            stream.WriteByte((byte)(i / 256));
            stream.WriteByte((byte)(i % 256));
        }

        private byte[] GetSubImageBuffer(RunLengthTwoParts twoPartBuffer, NikseBitmap nbmp, Paragraph p)
        {
            var ms = new MemoryStream();

            // sup picture datasize
            WriteEndianWord(twoPartBuffer.Length + 33, ms);

            // first display control sequence table address
            int startDisplayControlSequenceTableAddress = twoPartBuffer.Length + 4;
            WriteEndianWord(startDisplayControlSequenceTableAddress, ms);

            // Write image
            const int imageTopFieldDataAddress = 4;
            ms.Write(twoPartBuffer.Buffer1, 0, twoPartBuffer.Buffer1.Length);
            int imageBottomFieldDataAddress = 4 + twoPartBuffer.Buffer1.Length;
            ms.Write(twoPartBuffer.Buffer2, 0, twoPartBuffer.Buffer2.Length);

            // Write zero delay
            ms.WriteByte(0);
            ms.WriteByte(0);

            // next display control sequence table address (use current is last)
            WriteEndianWord(startDisplayControlSequenceTableAddress + 24, ms); // start of display control sequence table address

            // Control command 1 = ForcedStartDisplay
            ms.WriteByte(1);

            // Control command 3 = SetColor
            WriteColors(ms); // 3 bytes

            // Control command 4 = SetContrast
            WriteContrast(ms); // 3 bytes

            // Control command 5 = SetDisplayArea
            WriteDisplayArea(ms, nbmp); // 7 bytes

            // Control command 6 = SetPixelDataAddress
            WritePixelDataAddress(ms, imageTopFieldDataAddress, imageBottomFieldDataAddress); // 5 bytes

            // Control command exit
            ms.WriteByte(255); // 1 bytes

            // Control Sequence Table
            // Write delay - subtitle duration
            WriteEndianWord(Convert.ToInt32(p.Duration.TotalMilliseconds * 90.0 - 1023) >> 10, ms);

            // next display control sequence table address (use current is last)
            WriteEndianWord(startDisplayControlSequenceTableAddress + 24, ms); // start of display control sequence table address

            // Control command 2 = StopDisplay
            ms.WriteByte(2);

            return ms.ToArray();
        }

        public void WriteParagraph(Paragraph p, Bitmap bmp)
        {
            // timestamp: 00:00:33:900, filepos: 000000000
            _idx.AppendLine(string.Format("timestamp: {0:00}:{1:00}:{2:00}:{3:000}, filepos: {4}", p.StartTime.Hours, p.StartTime.Minutes, p.StartTime.Seconds, p.StartTime.Milliseconds, _subFile.Position.ToString("X").PadLeft(9, '0').ToLower()));

            // write binary vobsub file (duration + image)
            _subFile.Write(Mpeg2PackHeaderBuffer, 0, Mpeg2PackHeaderBuffer.Length);

            var nbmp = new NikseBitmap(bmp);
            nbmp.ConverToFourColors(_background, _pattern, _emphasis1, _emphasis2);
            var twoPartBuffer = nbmp.RunLengthEncodeForDvd(_background, _pattern, _emphasis1, _emphasis2);
            var imageBuffer = GetSubImageBuffer(twoPartBuffer, nbmp, p);

            // PES size
            WritePesSize(0, imageBuffer, PacketizedElementaryStreamHeaderBufferFirst);
            _subFile.Write(PacketizedElementaryStreamHeaderBufferFirst, 0, PacketizedElementaryStreamHeaderBufferFirst.Length);

            // PTS (timestamp)
            FillPTS(p.StartTime);
            _subFile.Write(PresentationTimeStampBuffer, 0, PresentationTimeStampBuffer.Length);

            _subFile.WriteByte(0x1e); // ??
            _subFile.WriteByte(0x60); // ??
            _subFile.WriteByte(0x3a); // ??

            _subFile.WriteByte((byte)_languageStreamId); //sub-stream number, 32=english

            if (Mpeg2PackHeaderBuffer.Length + imageBuffer.Length + 7 > PacketizedElementaryStreamMaximumLength)
            {
                int index = 0;
                while (index < imageBuffer.Length)
                {
                    _subFile.WriteByte(imageBuffer[index]);
                    if (_subFile.Position % 0x800 == 0) // write header for next PES packet
                    {
                        _subFile.Write(Mpeg2PackHeaderBuffer, 0, Mpeg2PackHeaderBuffer.Length);
                        WritePesSize(index, imageBuffer, PacketizedElementaryStreamHeaderBufferNext);
                        _subFile.Write(PacketizedElementaryStreamHeaderBufferNext, 0, PacketizedElementaryStreamHeaderBufferNext.Length);
                        _subFile.WriteByte((byte)_languageStreamId); //sub-stream number, 32=english
                    }
                    index++;
                }
            }
            else
            {
                _subFile.Write(imageBuffer, 0, imageBuffer.Length); // write image bytes + control sequence
            }

            while  (_subFile.Position % 0x800 != 0) // 2048 packet size - pad with 0xff
                _subFile.WriteByte(0xff);
        }

        private static void WritePesSize(int subtract, byte[] imageBuffer, byte[] writeBuffer)
        {
            int length = Mpeg2PackHeaderBuffer.Length + imageBuffer.Length - subtract;
            if (length > PacketizedElementaryStreamMaximumLength)
            {
                writeBuffer[4] = PacketizedElementaryStreamMaximumLength / 256;
                writeBuffer[5] = PacketizedElementaryStreamMaximumLength % 256;
            }
            else
            {
                writeBuffer[4] = (byte)(length / 256);
                writeBuffer[5] = (byte)(length % 256);
            }
        }

        private void WritePixelDataAddress(Stream stream, int imageTopFieldDataAddress, int imageBottomFieldDataAddress)
        {
            stream.WriteByte(6);
            WriteEndianWord(imageTopFieldDataAddress, stream);
            WriteEndianWord(imageBottomFieldDataAddress, stream);
        }

        private void WriteDisplayArea(Stream stream, NikseBitmap nbmp)
        {
            stream.WriteByte(5);

            // Write 6 bytes of area - starting X, ending X, starting Y, ending Y, each 12 bits
            ushort startX = (ushort) ((_screenWidth - nbmp.Width) / 2);
            ushort endX = (ushort)(startX + nbmp.Width-1);
            ushort startY = (ushort)(_screenHeight - nbmp.Height - _bottomMargin);
            ushort endY = (ushort)(startY + nbmp.Height-1);

            WriteEndianWord((ushort)(startX << 4 | endX >> 8), stream); // 16 - 12 start x + 4 end x
            WriteEndianWord((ushort)(endX << 8 | startY >> 4), stream); // 16 - 8 endx + 8 starty
            WriteEndianWord((ushort)(startY << 12 | endY), stream);     // 16 - 4 start y + 12 end y
        }

        /// <summary>
        /// Directly provides the four contrast (alpha blend) values to associate with the four pixel values. One nibble per pixel value for a total of 2 bytes. 0x0 = transparent, 0xF = opaque
        /// </summary>
        private void WriteContrast(Stream stream)
        {
            stream.WriteByte(4);
            stream.WriteByte((byte)((_emphasis2.A << 4) | _emphasis1.A)); // emphasis2 + emphasis1
            stream.WriteByte((byte)((_pattern.A << 4) | _background.A)); // pattern + background
        }

        /// <summary>
        /// provides four indices into the CLUT for the current PGC to associate with the four pixel values. One nibble per pixel value for a total of 2 bytes.
        /// </summary>
        private void WriteColors(Stream stream)
        {
            // Index to palette
            const byte emphasis2 = 3;
            const byte emphasis1 = 2;
            const byte pattern = 1;
            const byte background = 0;

            stream.WriteByte(3);
            stream.WriteByte((emphasis2 << 4) | emphasis1); // emphasis2 + emphasis1
            stream.WriteByte((pattern << 4) | background); // pattern + background
        }

        /// <summary>
        /// Write the 5 PTS bytes to buffer
        /// </summary>
        private void FillPTS(TimeCode timeCode)
        {
            const string pre = "0011"; // 0011 or 0010 ?
            long newPts = (long)(timeCode.TotalSeconds * 90000.0 + 0.5);
            string bString = Convert.ToString(newPts, 2).PadLeft(33, '0');
            string fiveBytesString = pre + bString.Substring(0, 3) + "1" + bString.Substring(3, 15) + "1" + bString.Substring(18, 15) + "1";
            for (int i = 0; i < 5; i++)
            {
                byte b = Convert.ToByte(fiveBytesString.Substring((i * 8), 8), 2);
                PresentationTimeStampBuffer[i] = b;
            }
        }

        public void CloseSubFile()
        {
            if (_subFile != null)
                _subFile.Close();
            _subFile = null;
        }

        public void WriteIdxFile()
        {
            string idxFileName = _subFileName.Substring(0, _subFileName.Length - 3) + "idx";
            File.WriteAllText(idxFileName, _idx.ToString().Trim());
        }

        private StringBuilder CreateIdxHeader()
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"# VobSub index file, v7 (do not modify this line!)
#
# To repair desyncronization, you can insert gaps this way:
# (it usually happens after vob id changes)
#
#    delay: [sign]hh:mm:ss:ms
#
# Where:
#    [sign]: +, - (optional)
#    hh: hours (0 <= hh)
#    mm/ss: minutes/seconds (0 <= mm/ss <= 59)
#    ms: milliseconds (0 <= ms <= 999)
#
#    Note: You can't position a sub before the previous with a negative value.
#
# You can also modify timestamps or delete a few subs you don't like.
# Just make sure they stay in increasing order.


# Settings

# Original frame size
size: " + _screenWidth + "x" + _screenHeight + @"

# Origin, relative to the upper-left corner, can be overloaded by aligment
org: 0, 0

# Image scaling (hor,ver), origin is at the upper-left corner or at the alignment coord (x, y)
scale: 100%, 100%

# Alpha blending
alpha: 100%

# Smoothing for very blocky images (use OLD for no filtering)
smooth: OFF

# In millisecs
fadein/out: 50, 50

# Force subtitle placement relative to (org.x, org.y)
align: OFF at LEFT TOP

# For correcting non-progressive desync. (in millisecs or hh:mm:ss:ms)
# Note: Not effective in DirectVobSub, use 'delay: ... ' instead.
time offset: 0

# ON: displays only forced subtitles, OFF: shows everything
forced subs: OFF

# The original palette of the DVD
palette: 000000, " + ToHexColor(_pattern) + ", " + ToHexColor(_emphasis1) + ", " + ToHexColor(_emphasis2) + @", 828282, 828282, 828282, ffffff, 828282, bababa, 828282, 828282, 828282, 828282, 828282, 828282

# Custom colors (transp idxs and the four colors)
custom colors: OFF, tridx: 0000, colors: 000000, 000000, 000000, 000000

# Language index in use
langidx: 0

# " + _languageName + @"
id: " + _languageNameShort + @", index: 0
# Decomment next line to activate alternative name in DirectVobSub / Windows Media Player 6.x
# alt: English");
            return sb;
        }

        private static string ToHexColor(Color c)
        {
            return (c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2")).ToLower();
        }

    }
}