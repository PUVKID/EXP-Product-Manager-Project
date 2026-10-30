using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// 운영 데이터와 분리한 파일에서 교체 API의 실패 조건만 관측합니다.
class IoReplaceProbe
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        int errors = 0;
        foreach (string directory in args)
        {
            Directory.CreateDirectory(directory);
            foreach (int delay in new[] { 0, 5 })
            {
                string target = Path.Combine(directory, "probe-" + delay + ".dat");
                string temporary = target + ".tmp";
                File.WriteAllText(target, "초기", Encoding.UTF8);
                for (int i = 0; i < 300; i++)
                {
                    byte[] data = Encoding.UTF8.GetBytes("검사 " + i);
                    try
                    {
                        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                        { stream.Write(data, 0, data.Length); stream.Flush(true); }
                        File.Replace(temporary, target, null);
                        if (delay > 0) Thread.Sleep(delay);
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        Console.WriteLine("실패 directory=" + directory + " delay=" + delay + " index=" + i + " HRESULT=" + Marshal.GetHRForException(ex).ToString("X8") + " " + ex);
                        break;
                    }
                }
                Console.WriteLine("완료 directory=" + directory + " delay=" + delay);
            }
        }
        Console.WriteLine("오류 합계=" + errors);
        return errors == 0 ? 0 : 1;
    }
}
