using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Threading;
namespace BunjangAuto {
    public static class ServerClock {
        static readonly object Gate=new object();
        static readonly Stopwatch Elapsed=Stopwatch.StartNew();
        static DateTime anchor; static long sampledAt; static bool valid, busy; static long attemptedAt=-600000;
        public static string LastError = "";
        public static bool Fresh { get { lock(Gate) return valid && Elapsed.ElapsedMilliseconds-sampledAt < 1800000; } }
        public static DateTime UtcNow { get { lock(Gate) return valid ? anchor.AddMilliseconds(Elapsed.ElapsedMilliseconds-sampledAt) : DateTime.UtcNow; } }
        public static DateTime KoreaNow { get { return DateTime.SpecifyKind(UtcNow.AddHours(9),DateTimeKind.Unspecified); } }
        public static string Status { get { return Fresh ? "번개장터 서버 시각 동기화 · " + KoreaNow.ToString("HH:mm:ss") : "서버 시각 동기화 필요 · 등록은 동기화 후 시작됩니다"; } }
        public static void RefreshAsync() { lock(Gate) { if(busy || Elapsed.ElapsedMilliseconds-attemptedAt<60000) return; busy=true; attemptedAt=Elapsed.ElapsedMilliseconds; } ThreadPool.QueueUserWorkItem(delegate { try { Fetch(); LastError=""; } catch(Exception ex) { LastError=ex.Message; } finally { lock(Gate) busy=false; } }); }
        public static void RequireFresh() { if(!Fresh) { RefreshAsync(); throw new InvalidOperationException("번개장터 서버 시각을 확인 중입니다. 인터넷 연결 후 잠시 뒤 다시 시작해 주세요."); } }
        static void Fetch() {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            long started=Elapsed.ElapsedMilliseconds;
            var req=(HttpWebRequest)WebRequest.Create("https://m.bunjang.co.kr/?exp_clock="+Guid.NewGuid().ToString("N"));
            req.Method="HEAD"; req.Timeout=10000; req.ReadWriteTimeout=10000; req.AllowAutoRedirect=false; req.UserAgent="EXP/2.2";
            req.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            using(var response=(HttpWebResponse)req.GetResponse()) {
                if(response.StatusCode!=HttpStatusCode.OK) throw new InvalidOperationException("서버 시각 응답 오류");
                DateTime date; if(!DateTime.TryParseExact(response.Headers["Date"],"r",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out date)) throw new InvalidOperationException("서버 시각 누락");
                long age=0; var ageText=response.Headers["Age"]; if(ageText!=null && (!long.TryParse(ageText,out age) || age<0 || age>5)) throw new InvalidOperationException("캐시된 서버 시각");
                long end=Elapsed.ElapsedMilliseconds; if(end-started>5000) throw new InvalidOperationException("서버 시각 응답 지연");
                date=date.AddSeconds(age).AddMilliseconds((end-started)/2.0);
                lock(Gate) {
                    if(valid && Math.Abs((date-anchor.AddMilliseconds(end-sampledAt)).TotalMinutes)>5) throw new InvalidOperationException("서버 시각이 급격히 달라졌습니다.");
                    anchor=date; sampledAt=end; valid=true;
                }
            }
        }
    }
}
