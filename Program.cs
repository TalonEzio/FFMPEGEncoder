using System.Diagnostics;
using System.Text;

namespace FFMPEGEncoder
{
    internal class Program
    {
        static async Task Main()
        {
            // Đặt mã hóa đầu vào và đầu ra của Console thành Unicode
            Console.InputEncoding = Console.OutputEncoding = Encoding.Unicode;

            // Đường dẫn đến thư mục chứa các file MKV
            Console.Write("Thư mục file: ");

            string directory = Console.ReadLine() ?? string.Empty;

            // Đường dẫn đến logo
            string logo = @"D:\BiliBili\logo-720.png";
            string fontName = "Cascadia Code";

            int bitrate = 1500;
            // Kiểm tra xem thư mục output có tồn tại không, nếu không thì tạo mới
            string outputDirectory = Path.Combine(directory, "output");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Lặp qua các file trong thư mục input
            string[] mkvFiles = Directory.GetFiles(directory, "*.mkv");

            Console.WriteLine("Đang xử lý...");

            foreach (string inputFile in mkvFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(inputFile);
                var outputFile = Path.Combine(outputDirectory, fileName + ".mp4");

                StringBuilder argumentsBuilder = new StringBuilder();
                argumentsBuilder.AppendFormat("-i \"{0}\" ", DoubleSlat(inputFile));
                argumentsBuilder.AppendFormat("-i \"{0}\" ", DoubleSlat(logo));
                argumentsBuilder.Append("-filter_complex \"");
                argumentsBuilder.Append("[0:v][1:v] overlay=x=main_w-overlay_w-(main_w*0.01):y=main_h*0.01, ");
                argumentsBuilder.Append($"subtitles='{EscapeString(DoubleSlat(inputFile))}':force_style='Fontname={fontName}'\" ");
                argumentsBuilder.Append($"-c:v h264_nvenc -b:v {bitrate}k -c:a copy ");
                argumentsBuilder.Append($"\"{DoubleSlat(outputFile)}\"");
                argumentsBuilder.Append(" -y");//Force


                string arguments = argumentsBuilder.ToString();

                var startInfo = new ProcessStartInfo
                {
                    FileName = @"Tools\ffmpeg",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                using var process = new Process();
                process.StartInfo = startInfo;
                process.Start();

                await process.WaitForExitAsync();
                //process.WaitForExit();

                Console.WriteLine($"Xử lý hoàn tất,output: {outputFile}");
            }

            Console.WriteLine("Hoàn thành!");
            Console.ReadLine();
        }

        static string EscapeString(string input)
        {
            return input
                .Replace(":", @"\:");
        }

        static string DoubleSlat(string input)
        {
            return input.Replace(@"\", @"\\");
        }
    }
}
