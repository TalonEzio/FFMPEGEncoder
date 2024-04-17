using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;

namespace FFMPEGEncoder
{
    record Settings
    {
        public string FontName { get; set; } = "UVN Van Bold";
        public int FontSize { get; set; } = 20;
        public int FontSizeMax { get; set; } = 50;
        public double FontOutline { get; set; } = 0.75;
        public int Bitrate { get; set; } = 5000;
        public int MinBitrateInput { get; set; } = 1000;
        public int MaxBitrateInput { get; set; } = 10000;
        public string TempFolder { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
    }
    internal class Program
    {
        static readonly Settings Settings = new();

        private static readonly string BasePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()!.Location)!;
        static async Task Main()
        {
            #region Config

            Console.InputEncoding = Console.OutputEncoding = Encoding.Unicode;

            var config = new ConfigurationBuilder()
                .SetBasePath(BasePath)
                .AddJsonFile("appsettings.json")
                .Build();

            config.Bind(nameof(FFMPEGEncoder.Settings), Settings);


            var tempPath = Path.GetTempPath();

            if (Settings.TempFolder.Contains("'") || tempPath.Contains("'"))
            {
                Console.WriteLine(Settings.TempFolder.Equals(tempPath)
                    ? $"Mặc định, đường dẫn TempFolder là {tempPath}"
                    : $"Đường dẫn TempFolder hiện tại:{Settings.TempFolder}");

                Console.WriteLine("Đường dẫn TempFolder (chứa file phụ đề tạm) không được chứa ký tự '");
                Console.WriteLine("Hãy đổi đường dẫn TempFolder tại nơi có quyền tạo file mới.");

                if (!tempPath.Contains("'"))
                {
                    Console.WriteLine("Có thể để chuỗi rỗng (\"\") cho TempFolder ");
                }
                Console.WriteLine($"Thay đổi đường dẫn TempFolder tại: {Path.Combine(BasePath, "appsettings.json")}");
                return;
            }

            if (string.IsNullOrEmpty(Settings.TempFolder))
            {
                Settings.TempFolder = tempPath;
            }

            bool enableCmd = Convert.ToBoolean(config[nameof(enableCmd)]);

            bool enableQuery = Convert.ToBoolean(config[nameof(enableQuery)]);

            bool deleteAfterEncode = Convert.ToBoolean(config[nameof(deleteAfterEncode)]);

            #endregion

            #region Input

            Console.Write($"Font name (mặc định {Settings.FontName}): ");
            var fontName = Console.ReadLine();

            if (string.IsNullOrEmpty(fontName))
            {
                Console.WriteLine($"Font name để mặc định :{Settings.FontName}");
                fontName = Settings.FontName;
            }

            Console.WriteLine($"Nhớ cài đặt font \"{fontName}\" trước khi chạy nhé!");


            Console.Write($"Kích cỡ font (mặc định {Settings.FontSize}, 0 <= FontSize <= {Settings.FontSizeMax}): ");


            var isNumber = int.TryParse(Console.ReadLine() ?? "-1", out var fontSize);

            if (!isNumber || fontSize <= 0 && fontSize > Settings.FontSizeMax)
            {
                Console.WriteLine($"Kích cỡ font để mặc định : {Settings.FontSize}");
                fontSize = Settings.FontSize;
            }

            string logo = File.Exists(Settings.Logo) ? Settings.Logo : Path.Combine(BasePath, "Images", "Logo.png");


            Console.Write($"Bitrate(kbps) ({Settings.MinBitrateInput} <= Bitrate <= {Settings.MaxBitrateInput}): ");

            var checkBitrate = int.TryParse(Console.ReadLine(), out var bitRate);

            if (!checkBitrate || bitRate < Settings.MinBitrateInput || bitRate > Settings.MaxBitrateInput)
            {
                Console.WriteLine($"Bitrate(kbps) để mặc định:{Settings.Bitrate}");
                bitRate = Settings.Bitrate;
            }

            Console.Write($"Outline (0 <= Outline <= 2 , số thực được, mặc định {Settings.FontOutline}): ");

            var checkFontOutline = double.TryParse(Console.ReadLine(), out var fontOutline);
            if (!checkFontOutline || fontOutline < 0 || fontOutline > 2)
            {
                Console.WriteLine($"Outline để mặc định: {Settings.FontOutline}");
                fontOutline = Settings.FontOutline;
            }

            Console.Write("Marquee (Dòng chữ chạy trên đầu video): ");

            var marquee = Console.ReadLine() ?? string.Empty;

            string path;
            do
            {
                Console.Write("Đường dẫn thư mục chứa mkv: ");
                path = Console.ReadLine() ?? string.Empty;
            } while (!Path.Exists(path));

            #endregion

            #region Process


            string[] mkvFiles = Directory.GetFiles(path, "*.mkv", SearchOption.AllDirectories);
            string[] aviFiles = Directory.GetFiles(path, "*.avi", SearchOption.AllDirectories);

            string[] files = [.. mkvFiles, .. aviFiles];

            Console.WriteLine($"Chuẩn bị xử lý {files.Length} files...");
            var stopWatch = new Stopwatch();

            foreach (var file in files)
            {
                try
                {
                    var settings = new Settings()
                    {
                        FontName = fontName,
                        FontSize = fontSize,
                        Logo = logo,
                        Bitrate = bitRate,
                        FontOutline = fontOutline
                    };

                    stopWatch.Restart();
                    var (outputPath, subtitleTempPath) = await EncodeVideoWithNVencC(file, settings, marquee, enableCmd, enableQuery);

                    stopWatch.Stop();
                    if (CheckAvailableSize(file, outputPath))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;

                        Console.WriteLine($"Encode thành công: {outputPath}");

                        Console.WriteLine($"Thời gian encode: {stopWatch.Elapsed.Minutes}:{stopWatch.Elapsed.Seconds}");
                        Console.WriteLine();
                        if (deleteAfterEncode)
                        {
                            File.Delete(file);
                            Console.WriteLine($"Xoá thành công {file}");
                        }
                        Console.ResetColor();
                    }
                    else
                    {

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Encode thất bại - {file}");
                        Console.ResetColor();
                    }

                    if (File.Exists(subtitleTempPath) && deleteAfterEncode)
                    {
                        File.Delete(subtitleTempPath);
                    }
                }
                catch (Exception e)
                {

                    Console.WriteLine($"Lỗi {e}");
                }
                finally
                {
                    await Task.Delay(1000);
                }
            }

            #endregion

            Console.WriteLine("Hoàn thành!");
            Console.ReadLine();
        }

        private static async Task<(string, string)> EncodeVideoWithNVencC(string inputFile, Settings settings, string marquee,
            bool enableCmd, bool enableQuery)
        {
            var subtitlePath = await ExportSubtitle2(inputFile,settings, marquee, enableCmd, enableQuery);

            var outputFile = Path.ChangeExtension(inputFile, ".mp4");

            var argumentsBuilder = new StringBuilder();

            argumentsBuilder.Append($"--avhw -c avc ");

            argumentsBuilder.Append($"-i \"{inputFile}\" ");

            argumentsBuilder.Append(
                $"--vpp-subburn filename=\"{subtitlePath}\",charcode=utf-8 ");
            argumentsBuilder.Append($"-b {settings.Bitrate}k ");

            //argumentsBuilder.Append($"--vpp-overlay file=\"{settings.Logo}\" ");

            argumentsBuilder.Append("--audio-codec libmp3lame ");

            argumentsBuilder.Append($"-o \"{outputFile}\" ");

            string arguments = argumentsBuilder.ToString();

            if (enableQuery)
            {
                Console.WriteLine($"--> NVencC argument: {arguments}");
            }

            var nVencCPath = "NVencC.exe";
            var startInfo = new ProcessStartInfo
            {
                FileName = nVencCPath,
                Arguments = arguments,
                UseShellExecute = enableCmd,
                CreateNoWindow = !enableCmd
            };


            using var process = new Process();
            process.StartInfo = startInfo;
            process.Start();

            await process.WaitForExitAsync();



            return (outputFile, subtitlePath);
        }

        private static async Task<string> ExportSubtitle2(string inputFile,Settings settings,string marquee, bool enableCmd, bool enableQuery)
        {
            var argumentBuilder = new StringBuilder();

            var tempPath = Settings.TempFolder;
            var randomSubtitlePath = Path.ChangeExtension(Path.GetRandomFileName(), ".srt");
            randomSubtitlePath = Path.Combine(tempPath, randomSubtitlePath);

            argumentBuilder.Append($"tracks \"{inputFile}\" ");
            argumentBuilder.Append($"2:\"{randomSubtitlePath}\"");


            var arguments = argumentBuilder.ToString();

            if (enableQuery)
            {
                Console.WriteLine($" --> MkvExtract argument: {arguments}");
            }

            var mkvExtractPath = Path.Combine(BasePath, "Tools", "mkvextract.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = mkvExtractPath,
                Arguments = arguments,
                UseShellExecute = enableCmd,
                CreateNoWindow = !enableCmd
            };


            using var process = new Process();
            process.StartInfo = startInfo;
            process.Start();
            await process.WaitForExitAsync();

            var outputSubtitlePath = Path.ChangeExtension(randomSubtitlePath, ".ass");

            if (await IsAssFormat(randomSubtitlePath))
            {
                File.Move(randomSubtitlePath, outputSubtitlePath);
                randomSubtitlePath = outputSubtitlePath;
            }

            var subtitle = Subtitle.Parse(randomSubtitlePath);
            subtitle.RemoveEmptyLines();

            var outputSubtitleContent = new AdvancedSubStationAlpha().ToText(subtitle, outputSubtitlePath);

            await File.WriteAllTextAsync(outputSubtitlePath, outputSubtitleContent);
            UpdateAssStyle(outputSubtitlePath, settings,"animew.org", marquee);

            return outputSubtitlePath;
        }

        private static async Task<(string, string)> EncodeVideoWithFFmpeg(string inputFile, Settings settings, string marquee, bool enableCmd, bool enableQuery)
        {
            var subtitlePath = await ExportSubtitle(inputFile, enableCmd, enableQuery);


            var outputFile = Path.ChangeExtension(inputFile, ".mp4");

            if (File.Exists(outputFile) && File.Exists(inputFile) && CheckAvailableSize(inputFile, outputFile))
            {
                Console.WriteLine("File đã xử lý xong");
                return (outputFile, subtitlePath);
            }

            var argumentsBuilder = new StringBuilder();

            argumentsBuilder.Append($"-i \"{inputFile}\" ");

            argumentsBuilder.Append($"-i \"{settings.Logo}\" ");

            argumentsBuilder.Append("-filter_complex \"");
            argumentsBuilder.Append("[0:v][1:v] overlay=x=main_w-overlay_w-(main_w*0.01):y=main_h*0.01, ");
            argumentsBuilder.Append($"subtitles='{EscapeString(DoubleSlash(subtitlePath))}'" +
                                    $":force_style='Fontname={settings.FontName},FontSize={settings.FontSize},Outline={settings.FontOutline}'\" ");

            argumentsBuilder.Append($"-c:v h264_nvenc -b:v {settings.Bitrate}k -c:a mp3 ");

            //argumentsBuilder.Append($"-c:v h264_nvenc -c:a mp3 ");

            argumentsBuilder.Append($"\"{outputFile}\"");
            argumentsBuilder.Append(" -y"); //Force encoding


            string arguments = argumentsBuilder.ToString();

            if (enableQuery)
            {
                Console.WriteLine($"--> FFMpeg argument: {arguments}");
            }

            var ffmpegPath = Path.Combine(BasePath, "Tools", "ffmpeg.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = enableCmd,
                CreateNoWindow = !enableCmd
            };


            using var process = new Process();
            process.StartInfo = startInfo;
            process.Start();

            await process.WaitForExitAsync();

            return (outputFile, subtitlePath);
        }

        private static async Task<string> ExportSubtitle(string inputFile, bool enableCmd, bool enableQuery)
        {
            var argumentBuilder = new StringBuilder();

            var tempPath = Settings.TempFolder;
            var randomFile = Path.ChangeExtension(Path.GetRandomFileName(), ".srt");
            randomFile = Path.Combine(tempPath, randomFile);

            argumentBuilder.Append($"tracks \"{inputFile}\" ");
            argumentBuilder.Append($"2:\"{randomFile}\"");


            var arguments = argumentBuilder.ToString();

            if (enableQuery)
            {
                Console.WriteLine($" --> MkvExtract argument: {arguments}");
            }

            var mkvExtractPath = Path.Combine(BasePath, "Tools", "mkvextract.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = mkvExtractPath,
                Arguments = arguments,
                UseShellExecute = enableCmd,
                CreateNoWindow = !enableCmd
            };


            using var process = new Process();
            process.StartInfo = startInfo;
            process.Start();
            await process.WaitForExitAsync();

            return randomFile;
        }


        public static async Task<bool> IsAssFormat(string filePath)
        {
            var fileContent = await File.ReadAllTextAsync(filePath);

            bool hasAssFormat = fileContent.Contains("[Script Info]") && fileContent.Contains("[V4+ Styles]") && fileContent.Contains("[Events]");

            return hasAssFormat;
        }
        public static void UpdateAssStyle(string filePath,Settings settings,string webSite, string marquee)
        {
            var newStyle1 = $"Style: Default,{settings.FontName},{settings.FontSize},&H00FFFFFF,&H00000000,&H00000000,&H00000000,-1,0,0,0,100,100,0,0,1,{settings.FontOutline},0,2,10,10,10,1";

            var newStyle2 = $"Style: Marquee,{settings.FontName},{settings.FontSize * 60 / 100},&H00FFFFFF,&H000000FF,&H00FFC900,&H00000000,-1,0,0,0,100,100,0,0,3,1.5,0,8,10,10,4,1";
            var newStyle3 = $"Style: Logo-Font,Bowlby One SC,{settings.FontSize * 90 / 100},&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0.75,0,1,0.5,0.2,2,10,10,10,1";

            var styles = $"{newStyle1}\n{newStyle2}\n{newStyle3}\n";

            var newEvents = $$"""
                              Dialogue: 0,0:00:00.00,5:00:00.00,Logo-Font,,0,0,0,,{\pos(343.6,35.733)}{{webSite.ToUpper()}}
                              Dialogue: 0,0:00:00.00,0:01:00.00,Marquee,,0,0,0,Banner;50;0;50[delay;left to right;fadeawaywidth;],{{marquee}}
                              Dialogue: 0,0:11:00.00,0:12:00.00,Marquee,,0,0,0,Banner;50;0;50[delay;left to right;fadeawaywidth;],{{marquee}}
                              
                              """;

            var fileContent = File.ReadAllText(filePath);

            int startIndex = fileContent.IndexOf("[V4+ Styles]", StringComparison.Ordinal);
            int endIndex = fileContent.IndexOf("[Events]", StringComparison.Ordinal);

            if (startIndex >= 0 && endIndex >= 0)
            {
                int defaultStyleIndex = fileContent.IndexOf("Style: Default", startIndex, endIndex - startIndex, StringComparison.Ordinal);

                if (defaultStyleIndex >= 0)
                {
                    // Chỉnh sửa thông tin style "Default"
                   
                    fileContent = fileContent.Remove(defaultStyleIndex, fileContent.IndexOf("\n", defaultStyleIndex, StringComparison.Ordinal) - defaultStyleIndex);
                    fileContent = fileContent.Insert(defaultStyleIndex, styles);
                }
            }

            var beginEventIndex =
                fileContent.IndexOf("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text", StringComparison.Ordinal);

            if (beginEventIndex >= 0)
            {
                var endOfFormatIndex = fileContent.IndexOf('\n', beginEventIndex);

                if (endOfFormatIndex >= 0)
                {
                    fileContent = fileContent.Insert(endOfFormatIndex + 1, newEvents);
                }
            }

            File.WriteAllText(filePath, fileContent);
        }

        public static async Task<bool> IsSrtFormat(string filePath)
        {
            var fileContent = await File.ReadAllTextAsync(filePath);

            bool startsWithNumber = char.IsDigit(fileContent[0]);
            bool containsNewline = fileContent.Contains("\n");

            return startsWithNumber && containsNewline;
        }
        private static string EscapeString(string input)
        {
            return input
                .Replace(":", @"\:")
                .Replace("'", @"''");
        }

        private static string DoubleSlash(string input)
        {
            return input.Replace(@"\", @"\\");
        }

        private static bool CheckAvailableSize(string inputFile, string outputFile)
        {
            var inputInfo = new FileInfo(inputFile);
            var outputInfo = new FileInfo(outputFile);
            return inputInfo.Length / 3 < outputInfo.Length;
        }

    }
}
