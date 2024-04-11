using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FFMPEGEncoder
{
    internal class Program
    {
        private static readonly string BasePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()!.Location)!;
        static async Task Main()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(BasePath)
                .AddJsonFile("appsettings.json")
                .Build();

            bool enableCmd = Convert.ToBoolean(config[nameof(enableCmd)]);
            bool deleteAfterEncode = Convert.ToBoolean(config[nameof(deleteAfterEncode)]);

            Console.InputEncoding = Console.OutputEncoding = Encoding.Unicode;

            string defaultFontName = "Arial";

            Console.Write($"Tên font (mặc định {defaultFontName}): ");
            var fontName = Console.ReadLine();

            if (string.IsNullOrEmpty(fontName)) fontName = defaultFontName;

            int defaultFontSize = 17;
            Console.Write($"Kích cỡ font (mặc định {defaultFontSize}): ");


            var isNumber = int.TryParse(Console.ReadLine(), out var fontSize);

            if (!isNumber || fontSize <= 0)
            {
                Console.WriteLine($"Kích cỡ quá nhỏ hoặc nhập sai, để mặc định : {defaultFontSize}");
                fontSize = defaultFontSize;
            }

            string logo = Path.Combine(BasePath, "Images", "Logo.png");

            int bitRate;

            bool checkBitrate;
            do
            {
                Console.Write("Bitrate (1000 <= x <= 5000): ");

                checkBitrate = int.TryParse(Console.ReadLine(), out bitRate);
            } while (!checkBitrate && bitRate < 1000 || bitRate > 5000);

            string path;
            do
            {
                Console.Write("Đường dẫn: ");
                path = Console.ReadLine() ?? "";
            } while (!Path.Exists(path));

            var files = Directory.GetFiles(path, "*.mkv", SearchOption.AllDirectories);

            Console.WriteLine($"Chuẩn bị xử lý {files.Length} files...");
            
            foreach (var file in files)
            {
                try
                {
                    var (outputPath,subtitleTempPath) = await EncodeVideo(file, logo, fontName, fontSize, bitRate, enableCmd);
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
                    if(File.Exists(subtitleTempPath)) File.Delete(subtitleTempPath);
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

            Console.WriteLine("Hoàn thành!");
            Console.ReadLine();
        }


        private static async Task<(string,string)> EncodeVideo(string inputFile, string logo, string fontName, int fontSize, int bitRate,bool enableCmd)
        {

            var subtitlePath = await ExportSubtitle(inputFile, enableCmd);


            var outputFile = Path.ChangeExtension(inputFile, ".mp4");

            if (File.Exists(outputFile) && File.Exists(inputFile) && CheckAvailableSize(inputFile, outputFile))
            {
                Console.WriteLine("File đã xử lý xong");
                return (outputFile,subtitlePath);
            }

            var argumentsBuilder = new StringBuilder();
            argumentsBuilder.Append($"-i \"{inputFile}\" ");
            argumentsBuilder.Append($"-i \"{logo}\" ");
            argumentsBuilder.Append("-filter_complex \"");
            argumentsBuilder.Append("[0:v][1:v] overlay=x=main_w-overlay_w-(main_w*0.01):y=main_h*0.01, ");
            argumentsBuilder.Append($"subtitles='{EscapeString(DoubleSlash(subtitlePath))}':force_style='Fontname={fontName},FontSize={fontSize}'\" ");
            argumentsBuilder.Append($"-c:v h264_nvenc -b:v {bitRate}k -c:a aac ");
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

        private static async Task<string> ExportSubtitle(string inputFile,bool enableCmd)
        {
            var argumentBuilder = new StringBuilder();

            var tempPath = Path.GetTempPath();
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
