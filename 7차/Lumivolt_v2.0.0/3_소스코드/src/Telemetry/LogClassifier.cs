using System;
using System.Text.RegularExpressions;

namespace BunjangAuto
{
    /// <summary>
    /// 기존 Log.Write("문구") 호출을 구조화된 이벤트(Level/Module/Action/Result)로 바꾼다.
    /// 기존 Bot/Runner 코드의 로그 문구는 한 글자도 바꾸지 않고 여기서 문구를 보고 분류한다.
    /// </summary>
    public static class LogClassifier
    {
        public static LogEntry Classify(string msg)
        {
            var e = new LogEntry();
            string m = (msg ?? "").Trim();

            // ---- 레벨 (일반 문구에서 추정)
            if (m.Contains("예외:")) e.Level = "ERROR";
            else if (m.Contains("→ 실패") || m.Contains("크롬 시작 실패") || m.StartsWith("오류:") || m.Contains("] 오류:") || m.Contains("저장 실패") || m.Contains("zip 생성 실패")) e.Level = "ERROR";
            else if (m.Contains("[주의]") || m.Contains("[경고]") || m.Contains("로그인을 확인하지 못했습니다") || m.Contains("로그인을 취소")) e.Level = "WARN";

            // ---- 모듈/액션
            if (m.StartsWith("[등록]")) { e.Module = "ProductUpload"; e.Action = "product.register.start"; }
            else if (m.Contains("→ 등록 완료")) { e.Module = "ProductUpload"; e.Action = "product.register.success"; e.Result = "OK"; }
            else if (m.Contains("→ 실패"))
            {
                e.Module = "ProductUpload"; e.Action = "product.register.fail"; e.Result = "FAIL";
                if (m.Contains("이미지") || m.Contains("사진")) { e.Action = "image.upload.fail"; }
                else if (m.Contains("카테고리")) { e.Action = "category.fail"; }
            }
            else if (m.StartsWith("===== 작업 시작")) { e.Module = "Automation"; e.Action = "run.begin"; }
            else if (m.StartsWith("===== 작업 완료")) { e.Module = "Automation"; e.Action = "run.end"; e.Result = "OK"; }
            else if (m.Contains("사용자가 작업을 중지")) { e.Module = "Automation"; e.Action = "run.stop"; }
            else if (m.Contains("로그인에 실패해 작업을 중단")) { e.Module = "Login"; e.Action = "login.check"; e.Result = "FAIL"; e.Level = "ERROR"; }
            else if (m.Contains("이미 로그인되어") || m.Contains("로그인 확인됨") || m.Contains("로그인된 상태로 보고")) { e.Module = "Login"; e.Action = "login.check"; e.Result = "OK"; }
            else if (m.Contains("로그인을 확인하지 못했") || m.Contains("로그인을 취소")) { e.Module = "Login"; e.Action = "login.check"; e.Result = "FAIL"; }
            else if (m.Contains("로그인")) { e.Module = "Login"; e.Action = "login"; }
            else if (m.Contains("크롬 시작 실패")) { e.Module = "Chrome"; e.Action = "chrome.launch"; e.Result = "FAIL"; }
            else if (m.Contains("크롬 브라우저를 여는 중") || m.Contains("호환 옵션으로 다시") || m.Contains("새 임시 프로필")) { e.Module = "Chrome"; e.Action = "chrome.launch"; }
            else if (m.Contains("크롬 데이터 폴더")) { e.Module = "Chrome"; e.Action = "chrome.profile"; }
            else if (m.Contains("chromedriver") || m.Contains("ChromeDriver") || m.Contains("드라이버")) { e.Module = "ChromeDriver"; e.Action = "driver.ready"; }
            else if (m.StartsWith("[시간대별 등록]")) { e.Module = "Schedule"; e.Action = "schedule.start"; }
            else if (m.StartsWith("[1개씩 등록] 다음 등록 예정")) { e.Module = "Schedule"; e.Action = "schedule.tick"; }
            else if (m.StartsWith("[1개씩 등록] 모두 등록해 종료")) { e.Module = "Schedule"; e.Action = "schedule.end"; e.Result = "OK"; }
            else if (m.StartsWith("[반복]")) { e.Module = "Schedule"; e.Action = "schedule.repeat"; }
            else if (m.StartsWith("CSV") && m.Contains("불러왔습니다")) { e.Module = "Csv"; e.Action = "csv.import"; e.Result = "OK"; }
            else if (m.StartsWith("CSV 저장")) { e.Module = "Csv"; e.Action = "csv.export"; e.Result = "OK"; }
            else if (m.StartsWith("양식 저장")) { e.Module = "Csv"; e.Action = "csv.template"; e.Result = "OK"; }
            else if (m.Contains("예외:")) { e.Module = "App"; e.Action = "exception"; e.Result = "FAIL"; }
            else if (m.Contains("화면 캡처")) { e.Module = "Selenium"; e.Action = "screenshot"; }
            else if (m.Contains("카테고리")) { e.Module = "Category"; e.Action = "category.info"; }
            else if (m.Contains("프로그램을 시작했습니다")) { e.Module = "App"; e.Action = "app.ready"; }
            else if (m.Contains("[주의]") && m.Contains("설정 파일")) { e.Module = "Storage"; e.Action = "storage.settings.unreadable"; }
            else if (m.Contains("[주의]") && m.Contains("상품 목록 파일")) { e.Module = "Storage"; e.Action = "storage.products.unreadable"; }

            if (e.Result == "" && e.Level == "ERROR") e.Result = "FAIL";
            if (e.Action == "exception") e.ExcType = ExtractExcType(m);
            return e;
        }

        private static string ExtractExcType(string m)
        {
            var r = Regex.Match(m, @"\b([A-Z][A-Za-z0-9_]*(?:\.[A-Z][A-Za-z0-9_]*)*Exception)\b");
            return r.Success ? r.Groups[1].Value : "";
        }
    }
}
