using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using Dapper;
using Xabe.FFmpeg;

namespace FFMPEGEncoder
{
    internal class Program
    {
        static async Task Main()
        {
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

            string logo = "Images/logo.png";

            const int bitRate = 1500;

            string path;
            do
            {
                Console.Write("Đường dẫn: ");
                path = Console.ReadLine() ?? "";
            } while (!Path.Exists(path));

            var files = Directory.GetFiles(path, "*.mkv", SearchOption.AllDirectories);

            Console.WriteLine($"Chuẩn bị xử lý {files.Length} files...");

            FFmpeg.SetExecutablesPath("Tools");


            foreach (var file in files)
            {
                try
                {
                    var outputPath = await EncodeVideo(file, logo, fontName, fontSize, bitRate);
                    if (CheckAvailableSize(file, outputPath))
                    {
                        Console.WriteLine($"Encode thành công: {file}");
                        File.Delete(file);
                    }
                    else
                    {

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Encode thất bại - {file}");
                        Console.ResetColor();
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

            Console.WriteLine("Hoàn thành!");
            Console.ReadLine();
        }

        static async Task<List<Episode>> GetMkvFiles()
        {

            var conn = new SqlConnection("Data Source=TalonEzio;Initial Catalog=BiliBiliDownloader;Integrated Security=True;TrustServerCertificate=True");

            return (await conn.QueryAsync<Episode>("Select * from Episodes where (Encoded is null or encoded = 0) and Done = 1")).ToList();
        }

        static async Task UpdateEncodePath(int episodeId, string encodePath, bool done)
        {
            var doneInt = done ? 1 : 0;

            var conn = new SqlConnection("Data Source=TalonEzio;Initial Catalog=BiliBiliDownloader;Integrated Security=True;TrustServerCertificate=True");
            await conn.OpenAsync();
            var result = await conn.ExecuteReaderAsync("Update episodes set Done=@doneInt, EncodePath = @encodePath,Encoded = @doneInt,Path = NULL" +
                                          " where Id = @episodeId",
                new
                {
                    encodePath,
                    episodeId,
                    doneInt
                });
            await conn.CloseAsync();
        }

        private static async Task<string> EncodeVideo(string inputFile, string logo, string fontName, int fontSize, int bitRate)
        {

            var subtitlePath = inputFile.Contains("'") ? await ExportSubtitle(inputFile) : inputFile;

            
            var outputFile = Path.ChangeExtension(inputFile, ".mp4");

            if (File.Exists(outputFile) && File.Exists(inputFile) && CheckAvailableSize(inputFile, outputFile))
            {
                Console.WriteLine("File đã xử lý xong");
                return outputFile;
            }

            var argumentsBuilder = new StringBuilder();
            argumentsBuilder.Append($"-i \"{inputFile}\" ");
            argumentsBuilder.Append($"-i \"{logo}\" ");
            argumentsBuilder.Append("-filter_complex \"");
            argumentsBuilder.Append("[0:v][1:v] overlay=x=main_w-overlay_w-(main_w*0.01):y=main_h*0.01, ");
            argumentsBuilder.Append($"subtitles='{EscapeString(DoubleSlash(subtitlePath))}':force_style='Fontname={fontName},FontSize={fontSize}'\" ");
            argumentsBuilder.Append($"-c:v h264_nvenc -b:v {bitRate}k -c:a copy ");
            argumentsBuilder.Append($"\"{outputFile}\"");
            argumentsBuilder.Append(" -y");
            
            string arguments = argumentsBuilder.ToString();

            var startInfo = new ProcessStartInfo
            {
                FileName = @"Tools\ffmpeg",
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            using var process = new Process();
            process.StartInfo = startInfo;
            process.Start();

            await process.WaitForExitAsync();

            File.Delete(subtitlePath);
            return outputFile;
        }

        private static async Task<string> ExportSubtitle(string inputFile)
        {
            var argumentBuilder = new StringBuilder();

            var randomFile = Path.GetRandomFileName();
            randomFile = Path.ChangeExtension(randomFile, ".srt");

            argumentBuilder.Append($"tracks \"{inputFile}\" ");
            argumentBuilder.Append($"2:\"{randomFile}\"");


            var arguments = argumentBuilder.ToString();

            var startInfo = new ProcessStartInfo
            {
                FileName = @"Tools\mkvextract",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
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
                //.Replace(":", @"\:")
                .Replace("'", @"''");
        }

        private static string DoubleSlash(string input)
        {
            return input.Replace(@"\", @"\\");
        }
    }
}
