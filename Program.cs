using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

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
        public string TempFolder { get; set; } = "";
        public string Logo { get; set; } = "";
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

            if (string.IsNullOrEmpty(Settings.TempFolder))
                Settings.TempFolder = Path.GetTempPath();

            bool enableCmd = Convert.ToBoolean(config[nameof(enableCmd)]);

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
            else
            {
                Console.WriteLine($"Nhớ cài đặt font \"{fontName}\" trước khi chạy nhé!");
            }

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

            Console.Write("Outline (0 <= Outline <= 2 , số thực được): ");

            var checkFontOutline = double.TryParse(Console.ReadLine(), out var fontOutline);
            if (!checkFontOutline || fontOutline < 0 || fontOutline > 2)
            {
                Console.WriteLine($"Outline để mặc định: {Settings.FontOutline}");
                fontOutline = Settings.FontOutline;
            }

            string path;
            do
            {
                Console.Write("Đường dẫn thư mục chứa mkv: ");
                path = Console.ReadLine() ?? "";
            } while (!Path.Exists(path));



            #endregion

            #region Process

            var files = Directory.GetFiles(path, "*.mkv", SearchOption.AllDirectories);

            Console.WriteLine($"Chuẩn bị xử lý {files.Length} files...");

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
                    var (outputPath, subtitleTempPath) = await EncodeVideo(file, settings, enableCmd);
                    if (CheckAvailableSize(file, outputPath))
                    {
                        Console.WriteLine($"Encode thành công: {file}");
                        if (deleteAfterEncode)
                        {
                            Console.WriteLine($"Xoá thành công {file}");
                        }
                    }
                    else
                    {

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Encode thất bại - {file}");
                        Console.ResetColor();
                    }
                    if (File.Exists(subtitleTempPath)) File.Delete(subtitleTempPath);
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
        private static async Task<(string, string)> EncodeVideo(string inputFile, Settings settings, bool enableCmd)
        {
            var subtitlePath = await ExportSubtitle(inputFile, enableCmd);

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
            argumentsBuilder.Append($"-c:v h264_nvenc -b:v {settings.Bitrate}k -c:a aac ");
            argumentsBuilder.Append($"\"{outputFile}\"");
            argumentsBuilder.Append(" -y"); //Force encoding


            string arguments = argumentsBuilder.ToString();

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

            File.Delete(subtitlePath);
            return (outputFile, subtitlePath);
        }

        private static async Task<string> ExportSubtitle(string inputFile, bool enableCmd)
        {
            var argumentBuilder = new StringBuilder();

            var tempPath = Settings.TempFolder;
            var randomFile = Path.ChangeExtension(Path.GetRandomFileName(), ".srt");
            randomFile = Path.Combine(tempPath, randomFile);

            argumentBuilder.Append($"tracks \"{inputFile}\" ");
            argumentBuilder.Append($"2:\"{randomFile}\"");


            var arguments = argumentBuilder.ToString();

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

        private static bool CheckAvailableSize(string inputFile, string outputFile)
        {
            var inputInfo = new FileInfo(inputFile);
            var outputInfo = new FileInfo(outputFile);
            return inputInfo.Length / 3 < outputInfo.Length;
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
    }
}
