using System;
using System.IO;
using System.Windows.Forms;
using Lumivolt.Security;
namespace BunjangAuto
{
    public static class LicenseFiles
    {
        public static bool CopyRequest(out string request, out string message)
        {
            request = "";
            try { request = TpmDevice.CreateRequest(); }
            catch (Exception ex) { message = TpmDevice.Error(ex); return false; }
            try { Clipboard.SetText(request); message = "복사했습니다. 판매자에게 붙여 넣어 보내세요."; return true; }
            catch { message = "자동 복사에 실패했습니다. 표시된 등록코드를 직접 복사해 주세요."; return false; }
        }
        public static string PasteKey()
        {
            return LicenseText.Normalize(Clipboard.GetText(), "LV3");
        }
        public static void ExportRequest(IWin32Window owner)
        {
            using (var d = new SaveFileDialog { Filter = "EXP 등록코드|*.lvrequest", FileName = "EXP-등록코드.lvrequest" })
            {
                if (d.ShowDialog(owner) != DialogResult.OK) return;
                try { File.WriteAllText(d.FileName, TpmDevice.CreateRequest(), LicenseProtocol.Utf8); FluentBox.Show(owner, "등록 요청 파일을 저장했습니다. 관리자에게 전달해 주세요.", "라이선스"); }
                catch (Exception ex) { FluentBox.Show(owner, ex is System.Security.Cryptography.CryptographicException ? TpmDevice.Error(ex) : "등록 요청을 저장하지 못했습니다. 저장 위치와 TPM 상태를 확인해 주세요.", "라이선스"); }
            }
        }
        public static string ImportLicense(IWin32Window owner)
        {
            using (var d = new OpenFileDialog { Filter = "EXP 인증키|*.lvlicense;*.expkey|텍스트 파일|*.txt" })
            {
                if (d.ShowDialog(owner) != DialogResult.OK) return null;
                try { return LicenseProtocol.ReadFile(d.FileName).Trim(); }
                catch { FluentBox.Show(owner, "라이선스 파일을 읽을 수 없습니다.", "라이선스"); return null; }
            }
        }
    }
}
